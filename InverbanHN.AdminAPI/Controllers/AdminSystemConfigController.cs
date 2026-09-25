using System;
using System.Threading.Tasks;
using Dapper;
using InverbanHN.Shared.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InverbanHN.AdminAPI.Controllers
{
    [ApiController]
    [Route("api/admin/system-config")]
    [Authorize(Roles = "SuperAdmin")]
    public class AdminSystemConfigController : AdminBaseController
    {
        private readonly DapperContext _dapperContext;

        public AdminSystemConfigController(DapperContext dapperContext)
        {
            _dapperContext = dapperContext;
        }

        /// <summary>
        /// GET /api/admin/system-config
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetSystemConfig()
        {
            try
            {
                using var connection = _dapperContext.CreateConnection();
                var sql = @"
                    SELECT 
                        ISNULL(Tax_Rate, 15.0) AS taxRate,
                        ISNULL(Maintenance_Mode, 0) AS maintenanceMode,
                        ISNULL(Support_Email, 'soporte@inverbanhn.com') AS supportEmail,
                        ISNULL(Default_Currency, 'HNL') AS defaultCurrency
                    FROM [Core].[System_Config]";

                var config = await connection.QuerySingleOrDefaultAsync(sql);
                return Ok(config ?? new
                {
                    taxRate = 15.0,
                    maintenanceMode = false,
                    supportEmail = "soporte@inverbanhn.com",
                    defaultCurrency = "HNL"
                });
            }
            catch (Exception)
            {
                return Ok(new
                {
                    taxRate = 15.0,
                    maintenanceMode = false,
                    supportEmail = "soporte@inverbanhn.com",
                    defaultCurrency = "HNL"
                });
            }
        }

        /// <summary>
        /// PUT /api/admin/system-config
        /// </summary>
        [HttpPut]
        public async Task<IActionResult> UpdateSystemConfig([FromBody] SystemConfigRequest request)
        {
            try
            {
                using var connection = _dapperContext.CreateConnection();
                var sql = @"
                    IF EXISTS (SELECT 1 FROM [Core].[System_Config])
                    BEGIN
                        UPDATE [Core].[System_Config] 
                        SET Tax_Rate = @TaxRate, Maintenance_Mode = @MaintenanceMode, Support_Email = @SupportEmail, Default_Currency = @DefaultCurrency;
                    END
                    ELSE
                    BEGIN
                        INSERT INTO [Core].[System_Config] (Tax_Rate, Maintenance_Mode, Support_Email, Default_Currency)
                        VALUES (@TaxRate, @MaintenanceMode, @SupportEmail, @DefaultCurrency);
                    END";

                await connection.ExecuteAsync(sql, new
                {
                    TaxRate = request.TaxRate,
                    MaintenanceMode = request.MaintenanceMode ? 1 : 0,
                    SupportEmail = request.SupportEmail?.Trim(),
                    DefaultCurrency = request.DefaultCurrency?.Trim() ?? "HNL"
                });

                return Ok(new { Message = "Configuración del sistema actualizada correctamente." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Title = "Error al actualizar configuración", Detail = ex.Message });
            }
        }
    }

    public class SystemConfigRequest
    {
        public double TaxRate { get; set; } = 15.0;
        public bool MaintenanceMode { get; set; }
        public string SupportEmail { get; set; } = string.Empty;
        public string DefaultCurrency { get; set; } = "HNL";
    }
}
