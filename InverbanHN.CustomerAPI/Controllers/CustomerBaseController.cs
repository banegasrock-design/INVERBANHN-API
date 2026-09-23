using System;
using System.Data;
using System.Security.Claims;
using System.Threading.Tasks;
using Dapper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace InverbanHN.CustomerAPI.Controllers
{
    public abstract class CustomerBaseController : ControllerBase
    {
        /// <summary>
        /// Extrae el ID del Usuario (Comprador) autenticado a partir de los Claims del JWT.
        /// </summary>
        protected int GetUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                              ?? User.FindFirst("sub")?.Value 
                              ?? User.FindFirst("UserId")?.Value 
                              ?? User.FindFirst("user_id")?.Value;

            if (!string.IsNullOrEmpty(userIdClaim) && int.TryParse(userIdClaim, out var userId))
            {
                return userId;
            }

            throw new UnauthorizedAccessException("Claim obligatorio 'UserId' no fue encontrado o es inválido en el token del cliente.");
        }

        /// <summary>
        /// Inyecta el contexto de auditoría sp_set_session_context 'UsuarioID'.
        /// </summary>
        protected async Task SetAuditContextAsync(IDbConnection connection)
        {
            try
            {
                int userId = GetUserId();
                var sql = "EXEC sp_set_session_context @key = N'UsuarioID', @value = @UserId;";
                await connection.ExecuteAsync(sql, new { UserId = userId });
            }
            catch
            {
                // Fallback silencioso si es una llamada pública sin token
            }
        }

        /// <summary>
        /// Manejador estandarizado de excepciones SQL Server devolviendo un ProblemDetails estructurado.
        /// </summary>
        protected IActionResult HandleSqlException(SqlException ex, string operation)
        {
            var problemDetails = new ProblemDetails
            {
                Title = $"Error de Base de Datos en {operation}",
                Status = StatusCodes.Status500InternalServerError,
                Detail = ex.Message,
                Instance = HttpContext.Request.Path
            };

            problemDetails.Extensions["errorCode"] = ex.Number;
            problemDetails.Extensions["serverError"] = true;

            return StatusCode(StatusCodes.Status500InternalServerError, problemDetails);
        }
    }
}
