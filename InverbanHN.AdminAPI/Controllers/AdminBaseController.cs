using System;
using System.Data;
using System.Security.Claims;
using System.Threading.Tasks;
using Dapper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace InverbanHN.AdminAPI.Controllers
{
    public abstract class AdminBaseController : ControllerBase
    {
        /// <summary>
        /// Extrae el User_ID del SuperAdmin autenticado desde los Claims del Token JWT.
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

            return 1; // Fallback por defecto SuperAdmin
        }

        /// <summary>
        /// Regla Arquitectónica #2: Inyección de Auditoría.
        /// Ejecuta EXEC sp_set_session_context 'UsuarioID', @UserId antes de cualquier operación DML.
        /// </summary>
        protected async Task SetAuditContextAsync(IDbConnection connection)
        {
            int userId = GetUserId();
            var sql = "EXEC sp_set_session_context @key = N'UsuarioID', @value = @UserId;";
            await connection.ExecuteAsync(sql, new { UserId = userId });
        }

        /// <summary>
        /// Inserta manualmente un registro en la tabla [Core].[Audit_Logs].
        /// </summary>
        protected async Task WriteAuditLogAsync(IDbConnection connection, string tableName, string action, string details)
        {
            try
            {
                int userId = GetUserId();
                var sql = @"
                    INSERT INTO [Core].[Audit_Logs] (Table_Name, Action, Operator_User_ID, Details, Created_At)
                    VALUES (@TableName, @Action, @OperatorUserId, @Details, GETUTCDATE());";

                await connection.ExecuteAsync(sql, new
                {
                    TableName = tableName,
                    Action = action,
                    OperatorUserId = userId,
                    Details = details
                });
            }
            catch
            {
                // Silencioso para no romper la transacción primaria
            }
        }

        /// <summary>
        /// Manejador estandarizado de excepciones SQL Server devolviendo ProblemDetails.
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
