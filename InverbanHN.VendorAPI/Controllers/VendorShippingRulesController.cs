using System;
using System.Collections.Generic;
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
    [Route("api/vendor/shipping-rules")]
    public class VendorShippingRulesController : VendorBaseController
    {
        private readonly DapperContext _dapperContext;

        public VendorShippingRulesController(DapperContext dapperContext)
        {
            _dapperContext = dapperContext;
        }

        /// <summary>
        /// GET /api/vendor/shipping-rules
        /// Lista las reglas de envío configuradas por el comercio.
        /// Protegido por [Authorize(Roles = "StoreAdmin,StoreOperator")].
        /// </summary>
        [HttpGet]
        [Authorize(Roles = "StoreAdmin,StoreOperator")]
        public async Task<IActionResult> GetShippingRules()
        {
            try
            {
                int storeId = GetStoreId();
                using var connection = _dapperContext.CreateConnection();

                var sql = @"
                    SELECT 
                        Rule_ID AS RuleId,
                        Store_ID AS StoreId,
                        Zone_Name AS ZoneName,
                        Standard_Cost_LPS AS StandardCostLps,
                        Free_Shipping_Min_LPS AS FreeShippingMinLps,
                        Estimated_Days AS EstimatedDays,
                        Is_Active AS IsActive
                    FROM [Sales].[Shipping_Rules]
                    WHERE Store_ID = @StoreId
                    ORDER BY Rule_ID DESC";

                var rules = await connection.QueryAsync<ShippingRuleResponseDto>(sql, new { StoreId = storeId });
                return Ok(rules);
            }
            catch (SqlException ex)
            {
                return HandleSqlException(ex, "GetShippingRules");
            }
            catch (Exception ex)
            {
                return BadRequest(new ProblemDetails { Title = "Error al obtener reglas de envío", Detail = ex.Message });
            }
        }

        /// <summary>
        /// POST /api/vendor/shipping-rules
        /// Crea una nueva regla de tarifa/envío para la tienda.
        /// Auditoría: sp_set_session_context 'UsuarioID'.
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "StoreAdmin")]
        public async Task<IActionResult> CreateShippingRule([FromBody] CreateUpdateShippingRuleRequestDto request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            try
            {
                int storeId = GetStoreId();
                using var connection = _dapperContext.CreateConnection();

                // Auditoría antes de escribir
                await SetAuditContextAsync(connection);

                var sql = @"
                    INSERT INTO [Sales].[Shipping_Rules]
                        (Store_ID, Zone_Name, Standard_Cost_LPS, Free_Shipping_Min_LPS, Estimated_Days, Is_Active, Created_At)
                    VALUES
                        (@StoreId, @ZoneName, @StandardCostLps, @FreeShippingMinLps, @EstimatedDays, @IsActive, GETUTCDATE());
                    SELECT CAST(SCOPE_IDENTITY() AS INT);";

                int newRuleId = await connection.ExecuteScalarAsync<int>(sql, new
                {
                    StoreId = storeId,
                    ZoneName = request.ZoneName.Trim(),
                    request.StandardCostLps,
                    request.FreeShippingMinLps,
                    request.EstimatedDays,
                    request.IsActive
                });

                return Created($"/api/vendor/shipping-rules/{newRuleId}", new
                {
                    Message = "Regla de envío creada exitosamente.",
                    RuleId = newRuleId,
                    StoreId = storeId,
                    ZoneName = request.ZoneName.Trim(),
                    request.StandardCostLps,
                    request.FreeShippingMinLps
                });
            }
            catch (SqlException ex)
            {
                return HandleSqlException(ex, "CreateShippingRule");
            }
            catch (Exception ex)
            {
                return BadRequest(new ProblemDetails { Title = "Error al crear regla de envío", Detail = ex.Message });
            }
        }

        /// <summary>
        /// PUT /api/vendor/shipping-rules/{id}
        /// Actualiza una regla de envío existente.
        /// </summary>
        [HttpPut("{id}")]
        [Authorize(Roles = "StoreAdmin")]
        public async Task<IActionResult> UpdateShippingRule(int id, [FromBody] CreateUpdateShippingRuleRequestDto request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            try
            {
                int storeId = GetStoreId();
                using var connection = _dapperContext.CreateConnection();

                // Auditoría antes de escribir
                await SetAuditContextAsync(connection);

                var sql = @"
                    UPDATE [Sales].[Shipping_Rules]
                    SET Zone_Name = @ZoneName,
                        Standard_Cost_LPS = @StandardCostLps,
                        Free_Shipping_Min_LPS = @FreeShippingMinLps,
                        Estimated_Days = @EstimatedDays,
                        Is_Active = @IsActive
                    WHERE Rule_ID = @RuleId AND Store_ID = @StoreId";

                int rowsAffected = await connection.ExecuteAsync(sql, new
                {
                    ZoneName = request.ZoneName.Trim(),
                    request.StandardCostLps,
                    request.FreeShippingMinLps,
                    request.EstimatedDays,
                    request.IsActive,
                    RuleId = id,
                    StoreId = storeId
                });

                if (rowsAffected == 0)
                {
                    return NotFound(new ProblemDetails
                    {
                        Title = "Regla de envío no encontrada",
                        Detail = $"La regla ID {id} no existe o no pertenece a la tienda {storeId}."
                    });
                }

                return Ok(new { Message = "Regla de envío actualizada exitosamente", RuleId = id });
            }
            catch (SqlException ex)
            {
                return HandleSqlException(ex, "UpdateShippingRule");
            }
            catch (Exception ex)
            {
                return BadRequest(new ProblemDetails { Title = "Error al actualizar regla de envío", Detail = ex.Message });
            }
        }

        /// <summary>
        /// DELETE /api/vendor/shipping-rules/{id} (Soft Delete)
        /// Desactiva una regla de envío marcando Is_Active = 0.
        /// </summary>
        [HttpDelete("{id}")]
        [Authorize(Roles = "StoreAdmin")]
        public async Task<IActionResult> SoftDeleteShippingRule(int id)
        {
            try
            {
                int storeId = GetStoreId();
                using var connection = _dapperContext.CreateConnection();

                // Auditoría antes de escribir
                await SetAuditContextAsync(connection);

                var sql = @"
                    UPDATE [Sales].[Shipping_Rules]
                    SET Is_Active = 0
                    WHERE Rule_ID = @RuleId AND Store_ID = @StoreId";

                int rowsAffected = await connection.ExecuteAsync(sql, new { RuleId = id, StoreId = storeId });

                if (rowsAffected == 0)
                {
                    return NotFound(new ProblemDetails
                    {
                        Title = "Regla de envío no encontrada",
                        Detail = $"La regla ID {id} no existe o no pertenece a la tienda {storeId}."
                    });
                }

                return NoContent();
            }
            catch (SqlException ex)
            {
                return HandleSqlException(ex, "SoftDeleteShippingRule");
            }
            catch (Exception ex)
            {
                return BadRequest(new ProblemDetails { Title = "Error al desactivar regla de envío", Detail = ex.Message });
            }
        }

        /// <summary>
        /// POST /api/vendor/shipping-rules/calculate (PÚBLICO / CHECKOUT)
        /// Endpoint consultado automáticamente durante el Checkout para obtener el costo exacto de envío.
        /// Lógica:
        /// 1. Busca la regla activa por Store_ID y Zone_Name (o fallback a zona 'Nacional').
        /// 2. Si CartSubtotalLps >= FreeShippingMinLps, el costo final es L. 0.00 (Envío Gratis).
        /// 3. De lo contrario, se cobra la StandardCostLps.
        /// </summary>
        [HttpPost("calculate")]
        [AllowAnonymous]
        public async Task<IActionResult> CalculateShipping([FromBody] CalculateShippingRequestDto request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            try
            {
                using var connection = _dapperContext.CreateConnection();

                string searchZone = string.IsNullOrWhiteSpace(request.ZoneName) ? "Nacional" : request.ZoneName.Trim();

                // Busca regla por zona específica o fallback por zona 'Nacional'
                var sql = @"
                    SELECT TOP 1
                        Rule_ID AS RuleId,
                        Store_ID AS StoreId,
                        Zone_Name AS ZoneName,
                        Standard_Cost_LPS AS StandardCostLps,
                        Free_Shipping_Min_LPS AS FreeShippingMinLps,
                        Estimated_Days AS EstimatedDays,
                        Is_Active AS IsActive
                    FROM [Sales].[Shipping_Rules]
                    WHERE Store_ID = @StoreId 
                      AND Is_Active = 1
                      AND (Zone_Name = @SearchZone OR Zone_Name = 'Nacional')
                    ORDER BY CASE WHEN Zone_Name = @SearchZone THEN 1 ELSE 2 END ASC";

                var rule = await connection.QueryFirstOrDefaultAsync<ShippingRuleResponseDto>(sql, new
                {
                    StoreId = request.StoreId,
                    SearchZone = searchZone
                });

                // Fallback por defecto si la tienda no ha configurado ninguna regla personalizada aún
                decimal standardCost = rule?.StandardCostLps ?? 120.00m;
                decimal? freeShippingMin = rule?.FreeShippingMinLps;
                int estimatedDays = rule?.EstimatedDays ?? 2;
                string matchedZone = rule?.ZoneName ?? "Nacional (Estándar)";

                bool isFreeShipping = freeShippingMin.HasValue && request.CartSubtotalLps >= freeShippingMin.Value;
                decimal finalCost = isFreeShipping ? 0.00m : standardCost;

                return Ok(new CalculateShippingResponseDto
                {
                    StoreId = request.StoreId,
                    ZoneName = matchedZone,
                    FinalShippingCostLps = finalCost,
                    IsFreeShipping = isFreeShipping,
                    FreeShippingMinLps = freeShippingMin,
                    StandardCostLps = standardCost,
                    EstimatedDays = estimatedDays
                });
            }
            catch (SqlException ex)
            {
                return HandleSqlException(ex, "CalculateShipping");
            }
            catch (Exception ex)
            {
                return BadRequest(new ProblemDetails { Title = "Error al calcular tarifa de envío", Detail = ex.Message });
            }
        }
    }
}
