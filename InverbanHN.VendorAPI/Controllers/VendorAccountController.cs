using System;
using System.Threading.Tasks;
using Dapper;
using InverbanHN.Shared.Data;
using InverbanHN.VendorAPI.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace InverbanHN.VendorAPI.Controllers
{
    [ApiController]
    [Route("api/vendor/account")]
    [Authorize(Roles = "StoreAdmin,StoreOperator")]
    public class VendorAccountController : VendorBaseController
    {
        private readonly DapperContext _dapperContext;
        private readonly InverbanHN.Shared.Services.IEmailService _emailService;

        public VendorAccountController(DapperContext dapperContext, InverbanHN.Shared.Services.IEmailService emailService)
        {
            _dapperContext = dapperContext;
            _emailService = emailService;
        }

        /// <summary>
        /// POST /api/vendor/account/change-password
        /// Permite al encargado de tienda cambiar su contraseña personal.
        /// Dispara la Plantilla 3 de Alerta de Seguridad por correo electrónico.
        /// </summary>
        [HttpPost("change-password")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequestDto request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            try
            {
                int userId = GetUserId();
                if (userId <= 0)
                {
                    return Unauthorized(new ProblemDetails
                    {
                        Title = "No Autorizado",
                        Status = StatusCodes.Status401Unauthorized,
                        Detail = "El ID del usuario autenticado no se pudo determinar a partir del token."
                    });
                }

                using var connection = _dapperContext.CreateConnection();

                // Consulta el Password_Hash actual del usuario y su email
                var userRecord = await connection.QuerySingleOrDefaultAsync<(int UserId, string PasswordHash, string? Email, string? FullName)>(@"
                    SELECT 
                        COALESCE(User_ID, Id) AS UserId,
                        Password_Hash AS PasswordHash,
                        Email,
                        COALESCE(Full_Name, Nombre, 'Administrador de Tienda') AS FullName
                    FROM [Core].[Users]
                    WHERE User_ID = @UserId OR Id = @UserId",
                    new { UserId = userId });

                if (string.IsNullOrEmpty(userRecord.PasswordHash))
                {
                    return NotFound(new ProblemDetails
                    {
                        Title = "Usuario no encontrado",
                        Status = StatusCodes.Status404NotFound,
                        Detail = $"No se encontró el registro de usuario activo para el ID {userId}."
                    });
                }

                // Verificación de Contraseña Actual
                var hasher = new PasswordHasher<object>();
                var verifyResult = hasher.VerifyHashedPassword(this, userRecord.PasswordHash, request.CurrentPassword);

                bool isMatch = verifyResult == PasswordVerificationResult.Success || verifyResult == PasswordVerificationResult.SuccessRehashNeeded;

                if (!isMatch)
                {
                    return BadRequest(new ProblemDetails
                    {
                        Title = "Contraseña Incorrecta",
                        Status = StatusCodes.Status400BadRequest,
                        Detail = "La contraseña actual ingresada es incorrecta."
                    });
                }

                // Generar nuevo Hash seguro
                string newPasswordHash = hasher.HashPassword(this, request.NewPassword);

                // Auditoría: sp_set_session_context 'UsuarioID'
                await SetAuditContextAsync(connection);

                var updateSql = @"
                    UPDATE [Core].[Users]
                    SET Password_Hash = @NewPasswordHash,
                        Updated_At = GETUTCDATE()
                    WHERE User_ID = @UserId OR Id = @UserId";

                await connection.ExecuteAsync(updateSql, new
                {
                    NewPasswordHash = newPasswordHash,
                    UserId = userId
                });

                // Disparo asíncrono de la Alerta de Seguridad por Correo (Plantilla 3)
                if (!string.IsNullOrEmpty(userRecord.Email))
                {
                    var securityEmailHtml = InverbanHN.Shared.Templates.EmailTemplateFactory.GetSecurityAlertTemplate(
                        userRecord.FullName ?? "Usuario",
                        "Cambio de Contraseña de Cuenta de Comercio");

                    _ = Task.Run(() => _emailService.SendEmailAsync(
                        userRecord.Email,
                        "🔒 Alerta de Seguridad: Cambio de Contraseña",
                        securityEmailHtml));
                }

                return Ok(new
                {
                    Message = "Contraseña actualizada correctamente.",
                    UpdatedAt = DateTime.UtcNow
                });
            }
            catch (SqlException ex)
            {
                return HandleSqlException(ex, "ChangePassword");
            }
            catch (Exception ex)
            {
                return BadRequest(new ProblemDetails { Title = "Error al cambiar contraseña", Detail = ex.Message });
            }
        }
    }
}
