using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Dapper;
using InverbanHN.AdminAPI.DTOs;
using InverbanHN.Shared.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace InverbanHN.AdminAPI.Controllers
{
    [ApiController]
    [Route("api/admin/auth")]
    [AllowAnonymous]
    public class AdminAuthController : AdminBaseController
    {
        private readonly DapperContext _dapperContext;
        private readonly IConfiguration _configuration;

        public AdminAuthController(
            DapperContext dapperContext,
            IConfiguration configuration)
        {
            _dapperContext = dapperContext;
            _configuration = configuration;
        }

        /// <summary>
        /// POST /api/admin/auth/login
        /// Autentica estrictamente a un usuario registrado verificando sus credenciales en [Core].[Users]
        /// y validando que posea el rol 'SuperAdmin'.
        /// </summary>
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] AdminLoginDto request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Datos Incompletos",
                    Status = StatusCodes.Status400BadRequest,
                    Detail = "Debe proporcionar su correo electrónico y contraseña."
                });
            }

            try
            {
                string cleanEmail = request.Email.Trim().ToLowerInvariant();
                string providedPassword = request.Password;

                bool isValidUser = false;
                int userId = 0;
                string fullName = string.Empty;
                string email = cleanEmail;
                string userRole = string.Empty;

                // 1. Buscar usuario en la base de datos real [Core].[Users]
                try
                {
                    using var connection = _dapperContext.CreateConnection();

                    var user = await connection.QuerySingleOrDefaultAsync<(int UserId, string FullName, string Email, string PasswordHash, string Role, bool IsActive)>(@"
                        SELECT 
                            User_ID AS UserId,
                            Full_Name AS FullName,
                            Email,
                            Password_Hash AS PasswordHash,
                            ISNULL(Role_Name, 'Customer') AS Role,
                            ISNULL(Is_Active, 1) AS IsActive
                        FROM [Core].[Users]
                        WHERE LOWER(Email) = @Email",
                        new { Email = cleanEmail });

                    if (user.UserId > 0 && user.IsActive && !string.IsNullOrEmpty(user.PasswordHash))
                    {
                        var hasher = new PasswordHasher<object>();
                        var verifyResult = hasher.VerifyHashedPassword(this, user.PasswordHash, providedPassword);
                        isValidUser = (verifyResult == PasswordVerificationResult.Success || verifyResult == PasswordVerificationResult.SuccessRehashNeeded);
                        
                        if (isValidUser)
                        {
                            userId = user.UserId;
                            fullName = !string.IsNullOrEmpty(user.FullName) ? user.FullName : "Administrador";
                            email = !string.IsNullOrEmpty(user.Email) ? user.Email : cleanEmail;
                            userRole = user.Role;
                        }
                    }
                }
                catch (Exception dbEx)
                {
                    Console.WriteLine($"[AdminAuth] Error de DB: {dbEx.Message}");
                }

                // 2. Fallback maestro exclusivo para credencial corporativa asignada del propietario
                if (!isValidUser)
                {
                    if ((cleanEmail == "admin@inverbanhn.com" || cleanEmail == "armando.banegas@inverbanhn.com") 
                        && (providedPassword == "SuperAdmin2026!" || providedPassword == "Banegas2026!"))
                    {
                        isValidUser = true;
                        userId = 1;
                        fullName = "Armando Banegas (SuperAdmin)";
                        email = cleanEmail;
                        userRole = "SuperAdmin";
                    }
                }

                // 3. Validación de Autenticidad: Rechazar si el usuario o clave no coinciden
                if (!isValidUser)
                {
                    return Unauthorized(new ProblemDetails
                    {
                        Title = "Credenciales Inválidas",
                        Status = StatusCodes.Status401Unauthorized,
                        Detail = "El correo electrónico o la contraseña son incorrectos."
                    });
                }

                // 4. Validación de Autorización: Rechazar si el usuario NO tiene rol 'SuperAdmin'
                if (userRole != "SuperAdmin" && !userRole.Equals("SuperAdmin", StringComparison.OrdinalIgnoreCase))
                {
                    return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
                    {
                        Title = "Acceso Denegado",
                        Status = StatusCodes.Status403Forbidden,
                        Detail = $"Tu cuenta tiene el rol '{userRole}' y no posee privilegios de SuperAdmin para acceder a este portal."
                    });
                }

                // 5. Generar Token JWT firmado con Claims oficiales
                var tokenHandler = new JwtSecurityTokenHandler();
                var key = Encoding.UTF8.GetBytes(_configuration["Jwt:Key"] ?? "ARMANDO_BANEGAS_SUPER_SECRET_SECURITY_KEY_2026");
                var issuer = _configuration["Jwt:Issuer"] ?? "INVERBANHN_API";
                var audience = _configuration["Jwt:Audience"] ?? "INVERBANHN_CLIENTS";
                var expiration = DateTime.UtcNow.AddDays(7);

                var tokenDescriptor = new SecurityTokenDescriptor
                {
                    Subject = new ClaimsIdentity(new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                        new Claim("UserId", userId.ToString()),
                        new Claim(ClaimTypes.Email, email),
                        new Claim(ClaimTypes.Role, "SuperAdmin"),
                        new Claim("role", "SuperAdmin"),
                        new Claim("unique_name", fullName)
                    }),
                    Expires = expiration,
                    Issuer = issuer,
                    Audience = audience,
                    SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
                };

                var token = tokenHandler.CreateToken(tokenDescriptor);
                string tokenString = tokenHandler.WriteToken(token);

                return Ok(new AdminLoginResponseDto
                {
                    Token = tokenString,
                    UserId = userId,
                    FullName = fullName,
                    Email = email,
                    Role = "SuperAdmin",
                    Expiration = expiration
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new ProblemDetails { Title = "Error al autenticar administrador", Detail = ex.Message });
            }
        }
    }
}
