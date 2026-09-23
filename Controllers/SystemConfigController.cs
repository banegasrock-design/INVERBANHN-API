using Application.DTOs.SystemConfig;
using Dapper;
using Infrastructure.Data.Contexts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace INVERBANHN.Controllers;

[Authorize(Roles = "SuperAdmin")]
[ApiController]
[Route("api/admin/system-config")]
public class SystemConfigController : ControllerBase
{
    private readonly DapperContext _dapperContext;

    public SystemConfigController(DapperContext dapperContext)
    {
        _dapperContext = dapperContext;
    }

    /// <summary>
    /// Obtiene la configuración global del sistema.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(SystemConfigDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetConfig()
    {
        using var connection = _dapperContext.CreateConnection();
        var sql = "SELECT Setting_Key AS [Key], Setting_Value AS [Value] FROM [Core].[System_Settings]";
        
        try
        {
            var settings = await connection.QueryAsync(sql);
            var dict = settings.ToDictionary(row => (string)row.Key, row => (string)row.Value);

            var config = new SystemConfigDto
            {
                TaxRate = dict.TryGetValue("Tax_Rate", out var tr) ? decimal.Parse(tr) : 15.0m,
                MaintenanceMode = dict.TryGetValue("Maintenance_Mode", out var mm) && bool.Parse(mm),
                SupportEmail = dict.TryGetValue("Support_Email", out var se) ? se : "soporte@inverbanhn.com",
                DefaultCurrency = dict.TryGetValue("Default_Currency", out var dc) ? dc : "HNL"
            };

            return Ok(config);
        }
        catch (Microsoft.Data.SqlClient.SqlException ex) when (ex.Number == 208) // Invalid object name
        {
            // Fallback si la tabla no ha sido creada aún, devuelve defaults
            return Ok(new SystemConfigDto { TaxRate = 15.0m, MaintenanceMode = false, SupportEmail = "soporte@inverbanhn.com", DefaultCurrency = "HNL" });
        }
    }

    /// <summary>
    /// Actualiza la configuración global del sistema.
    /// </summary>
    [HttpPut]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateConfig([FromBody] SystemConfigDto request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        using var connection = _dapperContext.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        try
        {
            // Set Audit Context
            var userName = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "Admin_User";
            await connection.ExecuteAsync("EXEC sp_set_session_context @key = N'user_id', @value = @UserId;", new { UserId = userName }, transaction);

            // Upsert Logic (Merge) - En SQL Server se hace combinando UPDATE e INSERT o un MERGE
            var sqlMerge = @"
                MERGE INTO [Core].[System_Settings] AS target
                USING (VALUES 
                    ('Tax_Rate', @TaxRate), 
                    ('Maintenance_Mode', @MaintenanceMode),
                    ('Support_Email', @SupportEmail),
                    ('Default_Currency', @DefaultCurrency)
                ) AS source ([Key], [Value])
                ON target.Setting_Key = source.[Key]
                WHEN MATCHED THEN 
                    UPDATE SET Setting_Value = source.[Value], Updated_At = GETUTCDATE()
                WHEN NOT MATCHED THEN
                    INSERT (Setting_Key, Setting_Value, Updated_At) 
                    VALUES (source.[Key], source.[Value], GETUTCDATE());
            ";

            await connection.ExecuteAsync(sqlMerge, new
            {
                TaxRate = request.TaxRate.ToString(System.Globalization.CultureInfo.InvariantCulture),
                MaintenanceMode = request.MaintenanceMode.ToString(),
                SupportEmail = request.SupportEmail,
                DefaultCurrency = request.DefaultCurrency
            }, transaction);

            transaction.Commit();
            return Ok(new { Message = "Configuración del sistema actualizada." });
        }
        catch (Microsoft.Data.SqlClient.SqlException ex) when (ex.Number == 208) // Invalid object name
        {
            // Si la tabla no existe, intentamos crearla al vuelo para evitar bloqueos
            transaction.Rollback();
            return await TryCreateTableAndRetry(request);
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            return StatusCode(500, new { Message = "Error al actualizar configuración.", Detail = ex.Message });
        }
    }

    private async Task<IActionResult> TryCreateTableAndRetry(SystemConfigDto request)
    {
        using var connection = _dapperContext.CreateConnection();
        connection.Open();

        var createTableSql = @"
            IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[Core].[System_Settings]') AND type in (N'U'))
            BEGIN
                CREATE TABLE [Core].[System_Settings](
                    [Setting_Key] [nvarchar](100) NOT NULL PRIMARY KEY,
                    [Setting_Value] [nvarchar](max) NULL,
                    [Updated_At] [datetime2](7) NOT NULL DEFAULT (getutcdate())
                )
            END
        ";

        try
        {
            await connection.ExecuteAsync(createTableSql);
            // Intentar de nuevo la lógica
            return await UpdateConfig(request);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "No se pudo crear la tabla System_Settings.", Detail = ex.Message });
        }
    }
}
