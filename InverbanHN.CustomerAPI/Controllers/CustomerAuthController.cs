using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Dapper;
using InverbanHN.CustomerAPI.DTOs;
using InverbanHN.Shared.Data;
using InverbanHN.Shared.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace InverbanHN.CustomerAPI.Controllers
{
    [ApiController]
    [Route("api/customer/auth")]
    [AllowAnonymous]
    [EnableRateLimiting("AuthRateLimit")]
    public class CustomerAuthController : CustomerBaseController
    {
        private readonly DapperContext _dapperContext;
        private readonly IConfiguration _configuration;
        private readonly IEmailService _emailService;

        public CustomerAuthController(
            DapperContext dapperContext,
            IConfiguration configuration,
            IEmailService emailService)
        {
            _dapperContext = dapperContext;
            _configuration = configuration;
            _emailService = emailService;
        }

        /// <summary>
        /// POST /api/customer/auth/register
        /// Registro de nuevos compradores. Hashea contraseña con PasswordHasher/BCrypt y asigna rol 'Customer'.
        /// </summary>
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterCustomerRequestDto request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            try
            {
                string cleanEmail = request.Email.Trim().ToLowerInvariant();
                using var connection = _dapperContext.CreateConnection();

                // 1. Validar que el email no exista en [Core].[Users]
                var emailExists = await connection.ExecuteScalarAsync<bool>(@"
                    SELECT CASE WHEN EXISTS (
                        SELECT 1 FROM [Core].[Users] WHERE LOWER(Email) = @Email
                    ) THEN 1 ELSE 0 END",
                    new { Email = cleanEmail });

                if (emailExists)
                {
                    return BadRequest(new ProblemDetails
                    {
                        Title = "Correo Ya Registrado",
                        Status = StatusCodes.Status400BadRequest,
                        Detail = $"El correo '{cleanEmail}' ya se encuentra asociado a una cuenta de usuario."
                    });
                }

                // 2. Hashear la contraseña con PasswordHasher
                var hasher = new PasswordHasher<object>();
                string passwordHash = hasher.HashPassword(this, request.Password);

                // Auditoría transaccional
                await SetAuditContextAsync(connection);

                var insertSql = @"
                    INSERT INTO [Core].[Users]
                        (Full_Name, Nombre, Email, Phone, Telefono, Password_Hash, Role, Is_Active, Created_At)
                    VALUES
                        (@FullName, @FullName, @Email, @Phone, @Phone, @PasswordHash, 'Customer', 1, GETUTCDATE());
                    SELECT CAST(SCOPE_IDENTITY() AS INT);";

                int newUserId = await connection.ExecuteScalarAsync<int>(insertSql, new
                {
                    FullName = request.FullName.Trim(),
                    Email = cleanEmail,
                    Phone = request.Phone?.Trim(),
                    PasswordHash = passwordHash
                });

                // Auto-generar depósito de bienvenida opcional o inicializar cuenta
                return Created($"/api/customer/profile", new
                {
                    Message = "Registro completado con éxito. Ya puede iniciar sesión.",
                    UserId = newUserId,
                    Email = cleanEmail,
                    Role = "Customer"
                });
            }
            catch (SqlException ex)
            {
                return HandleSqlException(ex, "Register");
            }
            catch (Exception ex)
            {
                return BadRequest(new ProblemDetails { Title = "Error al registrar cliente", Detail = ex.Message });
            }
        }

        /// <summary>
        /// POST /api/customer/auth/login
        /// Autenticación de cliente. Retorna Token JWT con claims UserID y Role = 'Customer'.
        /// </summary>
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] CustomerLoginRequestDto request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            try
            {
                string cleanEmail = request.Email.Trim().ToLowerInvariant();
                using var connection = _dapperContext.CreateConnection();

                var user = await connection.QuerySingleOrDefaultAsync<(int UserId, string FullName, string Email, string PasswordHash, string Role, bool IsActive)>(@"
                    SELECT 
                        COALESCE(User_ID, Id) AS UserId,
                        COALESCE(Full_Name, Nombre, 'Cliente InverbanHN') AS FullName,
                        Email,
                        Password_Hash AS PasswordHash,
                        ISNULL(Role, 'Customer') AS Role,
                        ISNULL(Is_Active, 1) AS IsActive
                    FROM [Core].[Users]
                    WHERE LOWER(Email) = @Email",
                    new { Email = cleanEmail });

                if (user.UserId <= 0 || !user.IsActive)
                {
                    return BadRequest(new ProblemDetails
                    {
                        Title = "Credenciales Inválidas",
                        Status = StatusCodes.Status400BadRequest,
                        Detail = "El correo electrónico o la contraseña ingresados son incorrectos."
                    });
                }

                // Validar Hash
                var hasher = new PasswordHasher<object>();
                var verifyResult = hasher.VerifyHashedPassword(this, user.PasswordHash, request.Password);

                bool isMatch = verifyResult == PasswordVerificationResult.Success || verifyResult == PasswordVerificationResult.SuccessRehashNeeded;

                if (!isMatch)
                {
                    return BadRequest(new ProblemDetails
                    {
                        Title = "Credenciales Inválidas",
                        Status = StatusCodes.Status400BadRequest,
                        Detail = "El correo electrónico o la contraseña ingresados son incorrectos."
                    });
                }

                // Generar JWT Token
                var tokenHandler = new JwtSecurityTokenHandler();
                var key = Encoding.UTF8.GetBytes(_configuration["Jwt:Key"] ?? "ARMANDO_BANEGAS_SUPER_SECRET_SECURITY_KEY_2026");
                var issuer = _configuration["Jwt:Issuer"] ?? "INVERBANHN_API";
                var audience = _configuration["Jwt:Audience"] ?? "INVERBANHN_CLIENTS";
                var expiration = DateTime.UtcNow.AddDays(7);

                var tokenDescriptor = new SecurityTokenDescriptor
                {
                    Subject = new ClaimsIdentity(new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                        new Claim("UserId", user.UserId.ToString()),
                        new Claim(ClaimTypes.Email, user.Email),
                        new Claim(ClaimTypes.Role, string.Equals(user.Role, "SuperAdmin", StringComparison.OrdinalIgnoreCase) ? "SuperAdmin" : "Customer")
                    }),
                    Expires = expiration,
                    Issuer = issuer,
                    Audience = audience,
                    SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
                };

                var token = tokenHandler.CreateToken(tokenDescriptor);
                string tokenString = tokenHandler.WriteToken(token);

                return Ok(new CustomerLoginResponseDto
                {
                    UserId = user.UserId,
                    FullName = user.FullName,
                    Email = user.Email,
                    Role = user.Role,
                    Token = tokenString,
                    Expiration = expiration
                });
            }
            catch (SqlException ex)
            {
                return HandleSqlException(ex, "Login");
            }
            catch (Exception ex)
            {
                return BadRequest(new ProblemDetails { Title = "Error al iniciar sesión", Detail = ex.Message });
            }
        }

        /// <summary>
        /// POST /api/customer/auth/forgot-password
        /// Genera token de recuperación de contraseña y dispara IEmailService.
        /// </summary>
        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequestDto request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            try
            {
                string cleanEmail = request.Email.Trim().ToLowerInvariant();
                using var connection = _dapperContext.CreateConnection();

                var user = await connection.QuerySingleOrDefaultAsync<(int UserId, string FullName, string Email)>(@"
                    SELECT COALESCE(User_ID, Id) AS UserId, COALESCE(Full_Name, Nombre, 'Cliente') AS FullName, Email 
                    FROM [Core].[Users] WHERE LOWER(Email) = @Email",
                    new { Email = cleanEmail });

                if (user.UserId > 0)
                {
                    string resetToken = Guid.NewGuid().ToString("N");
                    string resetLink = $"https://inverbanhn.com/reset-password?token={resetToken}&email={Uri.EscapeDataString(cleanEmail)}";

                    var emailHtml = $@"
                        <div style='font-family: Arial, sans-serif; padding: 20px;'>
                            <h2 style='color: #E20074;'>Recuperación de Contraseña - InverbanHN</h2>
                            <p>Hola <strong>{user.FullName}</strong>,</p>
                            <p>Hemos recibido una solicitud para restablecer la contraseña de tu cuenta.</p>
                            <p style='margin: 25px 0;'>
                                <a href='{resetLink}' style='background-color: #E20074; color: white; padding: 12px 25px; text-decoration: none; border-radius: 6px; font-weight: bold;'>Restablecer mi Contraseña</a>
                            </p>
                            <p style='color: #666; font-size: 12px;'>Si no solicitaste este cambio, puedes ignorar este correo de forma segura.</p>
                        </div>";

                    _ = Task.Run(() => _emailService.SendEmailAsync(cleanEmail, "Restablecer Contraseña - InverbanHN", emailHtml));
                }

                return Ok(new
                {
                    Message = "Si el correo electrónico existe en nuestra plataforma, recibirá las instrucciones para restablecer su contraseña."
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new ProblemDetails { Title = "Error al procesar recuperación", Detail = ex.Message });
            }
        }
    }
}
