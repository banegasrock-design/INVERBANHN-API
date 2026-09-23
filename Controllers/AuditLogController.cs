using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace INVERBANHN.Controllers;

[ApiController]
[Route("api/admin/audit-logs")]
[Authorize(AuthenticationSchemes = "Bearer,ApiKey,AzureAd", Roles = "SuperAdmin")]
public class AuditLogController : ControllerBase
{
    private readonly IConfiguration _config;

    public AuditLogController(IConfiguration config)
    {
        _config = config;
    }

    [HttpGet]
    public async Task<IActionResult> GetAuditLogs(
        [FromQuery] string? tableName,
        [FromQuery] string? operation,
        [FromQuery] string? operatorUserId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 50;
        if (pageSize > 100) pageSize = 100;

        using var connection = new Microsoft.Data.SqlClient.SqlConnection(
            _config.GetConnectionString("DefaultConnection")
        );

        string whereClause = "1=1";
        var parameters = new DynamicParameters();
        parameters.Add("@Offset", (page - 1) * pageSize);
        parameters.Add("@PageSize", pageSize);

        if (!string.IsNullOrWhiteSpace(tableName))
        {
            whereClause += " AND Table_Name = @TableName";
            parameters.Add("@TableName", tableName);
        }

        if (!string.IsNullOrWhiteSpace(operation))
        {
            whereClause += " AND Operation_Type = @Operation";
            parameters.Add("@Operation", operation);
        }

        if (!string.IsNullOrWhiteSpace(operatorUserId))
        {
            whereClause += " AND Operator_User_ID = @OperatorUserId";
            parameters.Add("@OperatorUserId", operatorUserId);
        }

        string sql = $@"
            SELECT 
                Audit_Log_ID AS Audit_ID, 
                Created_At AS Event_Time, 
                Table_Name, 
                Operation_Type AS Operation, 
                Operator_User_ID, 
                Before_State_Json AS Old_Values, 
                After_State_Json AS New_Values
            FROM [Core].[Audit_Logs]
            WHERE {whereClause}
            ORDER BY Created_At DESC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";

        string countSql = $@"
            SELECT COUNT(1)
            FROM [Core].[Audit_Logs]
            WHERE {whereClause}";

        try
        {
            var logs = await connection.QueryAsync(sql, parameters);
            var totalRecords = await connection.ExecuteScalarAsync<int>(countSql, parameters);

            return Ok(new
            {
                TotalRecords = totalRecords,
                Page = page,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling(totalRecords / (double)pageSize),
                Data = logs
            });
        }
        catch (Microsoft.Data.SqlClient.SqlException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Error al consultar la bitácora de auditoría",
                Detail = ex.Message,
                Status = 400
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ProblemDetails
            {
                Title = "Error interno del servidor",
                Detail = ex.Message,
                Status = 500
            });
        }
    }
}
