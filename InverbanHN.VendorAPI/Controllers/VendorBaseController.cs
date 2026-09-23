using System;
using System.Data;
using System.Security.Claims;
using System.Threading.Tasks;
using Dapper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace InverbanHN.VendorAPI.Controllers
{
    public abstract class VendorBaseController : ControllerBase
    {
        /// <summary>
        /// Regla Arquitectónica #1: Extrae obligatoriamente el Store_ID desde los Claims del JWT.
        /// Soporta "StoreId", "Store_ID", o fallback por "owner_user_id".
        /// </summary>
        protected int GetStoreId()
        {
            var storeIdClaim = User.FindFirst("StoreId")?.Value 
                               ?? User.FindFirst("Store_ID")?.Value 
                               ?? User.FindFirst("store_id")?.Value;

            if (!string.IsNullOrEmpty(storeIdClaim) && int.TryParse(storeIdClaim, out var storeId))
            {
                return storeId;
            }

            throw new UnauthorizedAccessException("Claim obligatorio 'StoreId' no fue encontrado o es inválido en el token JWT del comercio.");
        }

        /// <summary>
        /// Extrae el ID del Usuario autenticado para trazabilidad de auditoría.
        /// </summary>
        protected int GetUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                              ?? User.FindFirst("sub")?.Value 
                              ?? User.FindFirst("UserId")?.Value;

            if (!string.IsNullOrEmpty(userIdClaim) && int.TryParse(userIdClaim, out var userId))
            {
                return userId;
            }

            return 0; // Sistema / Desconocido
        }

        /// <summary>
        /// Regla Arquitectónica #2: Trazabilidad (Auditoría).
        /// Ejecuta EXEC sp_set_session_context 'UsuarioID', @UserId antes de operaciones de escritura.
        /// </summary>
        protected async Task SetAuditContextAsync(IDbConnection connection)
        {
            int userId = GetUserId();
            var sql = "EXEC sp_set_session_context @key = N'UsuarioID', @value = @UserId;";
            await connection.ExecuteAsync(sql, new { UserId = userId });
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

            // Manejo de errores específicos de SQL (ej. Errores personalizados con THROW 50060 o llaves duplicadas)
            if (ex.Number == 50060)
            {
                problemDetails.Title = "Error de Configuración SAR / Impuestos";
                problemDetails.Status = StatusCodes.Status400BadRequest;
                return BadRequest(problemDetails);
            }
            if (ex.Number == 2627 || ex.Number == 2601) // Violación de índice único
            {
                problemDetails.Title = "Registro Duplicado";
                problemDetails.Status = StatusCodes.Status409Conflict;
                problemDetails.Detail = "El recurso con esta llave única ya existe en el sistema.";
                return Conflict(problemDetails);
            }

            return StatusCode(StatusCodes.Status500InternalServerError, problemDetails);
        }
    }
}
