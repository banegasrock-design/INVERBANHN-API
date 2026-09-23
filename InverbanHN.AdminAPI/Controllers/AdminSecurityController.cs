using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Dapper;
using InverbanHN.AdminAPI.DTOs;
using InverbanHN.Shared.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace InverbanHN.AdminAPI.Controllers
{
    [ApiController]
    [Route("api/admin/security")]
    [Authorize(Roles = "SuperAdmin")]
    public class AdminSecurityController : AdminBaseController
    {
        private readonly DapperContext _dapperContext;

        public AdminSecurityController(DapperContext dapperContext)
        {
            _dapperContext = dapperContext;
        }

        /// <summary>
        /// GET /api/admin/security/apikeys
        /// Lista las API Keys activas en el sistema sin exponer el Hash.
        /// </summary>
        [HttpGet("apikeys")]
        public async Task<IActionResult> GetApiKeys()
        {
            try
            {
                using var connection = _dapperContext.CreateConnection();

                var sql = @"
                    SELECT 
                        Key_ID AS KeyId,
                        App_Name AS AppName,
                        Prefix,
                        ISNULL(Is_Active, 1) AS IsActive,
                        ISNULL(Created_At, GETUTCDATE()) AS CreatedAt
                    FROM [Core].[Api_Keys]
                    ORDER BY Key_ID DESC";

                var keys = await connection.QueryAsync<ApiKeyListItemDto>(sql);
                return Ok(keys);
            }
            catch (SqlException ex)
            {
                return HandleSqlException(ex, "GetApiKeys");
            }
            catch (Exception ex)
            {
                return BadRequest(new ProblemDetails { Title = "Error al obtener API Keys", Detail = ex.Message });
            }
        }

        /// <summary>
        /// POST /api/admin/security/apikeys/generate
        /// Genera una llave limpia en texto plano con el prefijo 'inv_live_...', guarda el Hash SHA-256 en la BD,
        /// y devuelve la llave limpia UNA SOLA VEZ en la respuesta HTTP.
        /// Auditoría: sp_set_session_context 'UsuarioID'.
        /// </summary>
        [HttpPost("apikeys/generate")]
        public async Task<IActionResult> GenerateApiKey([FromBody] GenerateApiKeyRequestDto request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            try
            {
                using var connection = _dapperContext.CreateConnection();

                // 1. Generar token aleatorio seguro
                var randomBytes = new byte[24];
                using (var rng = RandomNumberGenerator.Create())
                {
                    rng.GetBytes(randomBytes);
                }

                string secretString = Convert.ToBase64String(randomBytes).Replace("+", "").Replace("/", "").Replace("=", "");
                string plainTextApiKey = $"inv_live_{secretString}";
                string prefix = plainTextApiKey[..12]; // inv_live_xxxx

                // 2. Generar Hash SHA-256 de la API Key limpia
                using var sha256 = SHA256.Create();
                byte[] hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(plainTextApiKey));
                string keyHash = Convert.ToHexString(hashBytes);

                // Auditoría antes de escribir
                await SetAuditContextAsync(connection);

                var sql = @"
                    INSERT INTO [Core].[Api_Keys]
                        (App_Name, Key_Hash, Prefix, Is_Active, Created_At)
                    VALUES
                        (@AppName, @KeyHash, @Prefix, 1, GETUTCDATE());
                    SELECT CAST(SCOPE_IDENTITY() AS INT);";

                int newKeyId = await connection.ExecuteScalarAsync<int>(sql, new
                {
                    AppName = request.AppName.Trim(),
                    KeyHash = keyHash,
                    Prefix = prefix
                });

                await WriteAuditLogAsync(connection, "Core.Api_Keys", "GENERATE_KEY", $"Nueva API Key generada para App: '{request.AppName}'. Prefix: {prefix}.");

                return Created($"/api/admin/security/apikeys", new GenerateApiKeyResponseDto
                {
                    KeyId = newKeyId,
                    AppName = request.AppName.Trim(),
                    PlainTextApiKey = plainTextApiKey,
                    Prefix = prefix,
                    CreatedAt = DateTime.UtcNow
                });
            }
            catch (SqlException ex)
            {
                return HandleSqlException(ex, "GenerateApiKey");
            }
            catch (Exception ex)
            {
                return BadRequest(new ProblemDetails { Title = "Error al generar API Key", Detail = ex.Message });
            }
        }

        /// <summary>
        /// GET /api/admin/security/audit-logs
        /// Lee la tabla [Core].[Audit_Logs] con paginación y filtros opcionales por Table_Name o Operator_User_ID.
        /// </summary>
        [HttpGet("audit-logs")]
        public async Task<IActionResult> GetAuditLogs(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 15,
            [FromQuery] string? tableName = null,
            [FromQuery] int? operatorUserId = null)
        {
            if (pageNumber < 1) pageNumber = 1;
            if (pageSize < 1) pageSize = 15;
            if (pageSize > 100) pageSize = 100;

            try
            {
                int offset = (pageNumber - 1) * pageSize;
                using var connection = _dapperContext.CreateConnection();

                var countSql = @"
                    SELECT COUNT(1) 
                    FROM [Core].[Audit_Logs]
                    WHERE (@TableName IS NULL OR Table_Name = @TableName)
                      AND (@OperatorUserId IS NULL OR Operator_User_ID = @OperatorUserId)";

                var logsSql = @"
                    SELECT 
                        Log_ID AS LogId,
                        Table_Name AS TableName,
                        Action,
                        Operator_User_ID AS OperatorUserId,
                        Details,
                        Created_At AS CreatedAt
                    FROM [Core].[Audit_Logs]
                    WHERE (@TableName IS NULL OR Table_Name = @TableName)
                      AND (@OperatorUserId IS NULL OR Operator_User_ID = @OperatorUserId)
                    ORDER BY Log_ID DESC
                    OFFSET @Offset ROWS
                    FETCH NEXT @PageSize ROWS ONLY";

                var totalCount = await connection.ExecuteScalarAsync<int>(countSql, new { TableName = tableName, OperatorUserId = operatorUserId });
                var logs = await connection.QueryAsync<AuditLogItemDto>(logsSql, new
                {
                    TableName = tableName,
                    OperatorUserId = operatorUserId,
                    Offset = offset,
                    PageSize = pageSize
                });

                return Ok(new PagedResultDto<AuditLogItemDto>
                {
                    Items = logs,
                    TotalCount = totalCount,
                    PageNumber = pageNumber,
                    PageSize = pageSize
                });
            }
            catch (SqlException ex)
            {
                return HandleSqlException(ex, "GetAuditLogs");
            }
            catch (Exception ex)
            {
                return BadRequest(new ProblemDetails { Title = "Error al consultar logs de auditoría", Detail = ex.Message });
            }
        }
    }
}
