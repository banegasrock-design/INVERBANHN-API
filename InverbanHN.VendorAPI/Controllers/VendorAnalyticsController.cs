using System;
using System.Threading.Tasks;
using Dapper;
using InverbanHN.Shared.Data;
using InverbanHN.VendorAPI.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace InverbanHN.VendorAPI.Controllers
{
    [ApiController]
    [Route("api/vendor/analytics")]
    [Authorize(Roles = "StoreAdmin")]
    public class VendorAnalyticsController : VendorBaseController
    {
        private readonly DapperContext _dapperContext;

        public VendorAnalyticsController(DapperContext dapperContext)
        {
            _dapperContext = dapperContext;
        }

        /// <summary>
        /// GET /api/vendor/analytics/summary
        /// Consulta agregada (SUM) de ventas brutas (LPS), comisiones de plataforma y retenciones de ISR (1%).
        /// Filtrada obligatoriamente por Store_ID.
        /// </summary>
        [HttpGet("summary")]
        public async Task<IActionResult> GetAnalyticsSummary([FromQuery] DateTime? startDate = null, [FromQuery] DateTime? endDate = null)
        {
            try
            {
                int storeId = GetStoreId();
                using var connection = _dapperContext.CreateConnection();

                // Consulta agregada contra SubOrders y cálculo de comisiones e ISR (1%)
                var sql = @"
                    SELECT 
                        @StoreId AS StoreId,
                        ISNULL(SUM(TotalAmount), 0.00) AS GrossSalesLps,
                        ISNULL(SUM(TotalAmount * 0.05), 0.00) AS PlatformCommissionsLps, -- 5% Comisión estándar plataforma
                        ISNULL(SUM(TotalAmount * 0.01), 0.00) AS IsrWithholdingsLps,     -- 1% Retención ISR Honduras
                        ISNULL(SUM(TotalAmount * (1 - 0.05 - 0.01)), 0.00) AS NetSalesLps,
                        COUNT(1) AS TotalCompletedOrders
                    FROM [dbo].[SubOrders]
                    WHERE StoreId = @StoreId
                      AND (Status = 'Entregado' OR Status = 'Confirmado' OR Status = 'Completado')
                      AND (@StartDate IS NULL OR CreatedAt >= @StartDate)
                      AND (@EndDate IS NULL OR CreatedAt <= @EndDate)";

                var summary = await connection.QuerySingleOrDefaultAsync<AnalyticsSummaryResponseDto>(sql, new
                {
                    StoreId = storeId,
                    StartDate = startDate,
                    EndDate = endDate
                });

                return Ok(summary ?? new AnalyticsSummaryResponseDto { StoreId = storeId });
            }
            catch (SqlException ex)
            {
                return HandleSqlException(ex, "GetAnalyticsSummary");
            }
            catch (Exception ex)
            {
                return BadRequest(new ProblemDetails { Title = "Error al calcular analíticas", Detail = ex.Message });
            }
        }
    }
}
