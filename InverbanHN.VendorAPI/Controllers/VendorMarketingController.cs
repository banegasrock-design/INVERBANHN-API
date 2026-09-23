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
    [Route("api/vendor/marketing")]
    [Authorize(Roles = "StoreAdmin")]
    public class VendorMarketingController : VendorBaseController
    {
        private readonly DapperContext _dapperContext;

        public VendorMarketingController(DapperContext dapperContext)
        {
            _dapperContext = dapperContext;
        }

        /// <summary>
        /// POST /api/vendor/marketing/offers
        /// Inserta una oferta en [Catalog].[Product_Offers] validando Start_Date y End_Date.
        /// Auditoría: sp_set_session_context 'UsuarioID'.
        /// </summary>
        [HttpPost("offers")]
        public async Task<IActionResult> CreateProductOffer([FromBody] CreateProductOfferRequestDto request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            if (request.EndDate <= request.StartDate)
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Rango de fechas inválido",
                    Detail = "La fecha final (End_Date) debe ser posterior a la fecha inicial (Start_Date)."
                });
            }

            try
            {
                int storeId = GetStoreId();
                using var connection = _dapperContext.CreateConnection();

                // Verificar que el producto pertenece al Store_ID
                var productExists = await connection.ExecuteScalarAsync<bool>(
                    "SELECT COUNT(1) FROM [Catalog].[Products] WHERE Product_ID = @ProductId AND Store_ID = @StoreId",
                    new { request.ProductId, StoreId = storeId });

                if (!productExists)
                {
                    return NotFound(new ProblemDetails
                    {
                        Title = "Producto no encontrado",
                        Detail = $"El producto {request.ProductId} no existe o no pertenece a la tienda {storeId}."
                    });
                }

                // Auditoría antes de escribir
                await SetAuditContextAsync(connection);

                var sql = @"
                    INSERT INTO [Catalog].[Product_Offers]
                        (Product_ID, Store_ID, Offer_Price, Start_Date, End_Date, Is_Active)
                    VALUES
                        (@ProductId, @StoreId, @OfferPrice, @StartDate, @EndDate, 1);
                    SELECT CAST(SCOPE_IDENTITY() AS INT);";

                int newOfferId = await connection.ExecuteScalarAsync<int>(sql, new
                {
                    request.ProductId,
                    StoreId = storeId,
                    request.OfferPrice,
                    request.StartDate,
                    request.EndDate
                });

                return Created($"/api/vendor/marketing/offers/{newOfferId}", new
                {
                    Message = "Oferta de producto registrada exitosamente",
                    OfferId = newOfferId,
                    request.ProductId,
                    request.OfferPrice,
                    request.StartDate,
                    request.EndDate
                });
            }
            catch (SqlException ex)
            {
                return HandleSqlException(ex, "CreateProductOffer");
            }
            catch (Exception ex)
            {
                return BadRequest(new ProblemDetails { Title = "Error al crear oferta", Detail = ex.Message });
            }
        }

        /// <summary>
        /// GET /api/vendor/marketing/coupons
        /// Lista todos los cupones pertenecientes al Store_ID.
        /// Devuelve: Code, Discount_Type ('Fixed' o 'Percentage'), Discount_Value, Min_Purchase_LPS, Start_Date, End_Date, Usage_Limit y Is_Active.
        /// </summary>
        [HttpGet("coupons")]
        public async Task<IActionResult> GetCoupons()
        {
            try
            {
                int storeId = GetStoreId();
                using var connection = _dapperContext.CreateConnection();

                var sql = @"
                    SELECT 
                        Coupon_ID AS CouponId,
                        Store_ID AS StoreId,
                        Code,
                        ISNULL(Discount_Type, 'Fixed') AS DiscountType,
                        Discount_Value AS DiscountValue,
                        ISNULL(Min_Purchase_LPS, 0.00) AS MinPurchaseLps,
                        Start_Date AS StartDate,
                        End_Date AS EndDate,
                        Expiry_Date AS ExpiryDate,
                        Usage_Limit AS UsageLimit,
                        ISNULL(Is_Stackable, 0) AS IsStackable,
                        ISNULL(Is_Active, 1) AS IsActive
                    FROM [Catalog].[Coupons]
                    WHERE Store_ID = @StoreId
                    ORDER BY Coupon_ID DESC";

                var coupons = await connection.QueryAsync<CouponResponseDto>(sql, new { StoreId = storeId });
                return Ok(coupons);
            }
            catch (SqlException ex)
            {
                return HandleSqlException(ex, "GetCoupons");
            }
            catch (Exception ex)
            {
                return BadRequest(new ProblemDetails { Title = "Error al obtener cupones", Detail = ex.Message });
            }
        }

        /// <summary>
        /// POST /api/vendor/marketing/coupons
        /// Crea un nuevo cupón en [Catalog].[Coupons].
        /// REGLA DE NEGOCIO: Valida que el código (ej: 'OFERTA50') no exista actualmente en estado ACTIVO (Is_Active = 1) para ese mismo Store_ID.
        /// Auditoría: sp_set_session_context 'UsuarioID'.
        /// </summary>
        [HttpPost("coupons")]
        public async Task<IActionResult> CreateCoupon([FromBody] CreateCouponRequestDto request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var typeLower = request.DiscountType.ToLower();
            if (typeLower != "fixed" && typeLower != "percentage" && typeLower != "porcentaje" && typeLower != "fijo")
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Tipo de descuento inválido",
                    Detail = "El tipo de descuento debe ser 'Fixed' ('Fijo') o 'Percentage' ('Porcentaje')."
                });
            }

            try
            {
                int storeId = GetStoreId();
                string cleanCode = request.Code.Trim().ToUpper();
                using var connection = _dapperContext.CreateConnection();

                // Validar si el código ya existe activo para esta tienda
                var codeExists = await connection.ExecuteScalarAsync<bool>(@"
                    SELECT CASE WHEN EXISTS (
                        SELECT 1 
                        FROM [Catalog].[Coupons] 
                        WHERE Store_ID = @StoreId 
                          AND UPPER(Code) = @Code 
                          AND ISNULL(Is_Active, 1) = 1
                    ) THEN 1 ELSE 0 END",
                    new { StoreId = storeId, Code = cleanCode });

                if (codeExists)
                {
                    return BadRequest(new ProblemDetails
                    {
                        Title = "Código de Cupón Duplicado",
                        Status = StatusCodes.Status400BadRequest,
                        Detail = $"El código de cupón '{cleanCode}' ya existe y se encuentra activo para su tienda."
                    });
                }

                // Auditoría antes de escribir
                await SetAuditContextAsync(connection);

                var normalizedType = (typeLower.Contains("percent") || typeLower.Contains("porcent")) ? "Percentage" : "Fixed";
                DateTime? expiryToSave = request.EndDate ?? request.ExpiryDate;

                var sql = @"
                    INSERT INTO [Catalog].[Coupons]
                        (Store_ID, Code, Discount_Type, Discount_Value, Min_Purchase_LPS, Start_Date, End_Date, Expiry_Date, Usage_Limit, Is_Stackable, Is_Active)
                    VALUES
                        (@StoreId, @Code, @DiscountType, @DiscountValue, @MinPurchaseLps, @StartDate, @EndDate, @ExpiryDate, @UsageLimit, @IsStackable, 1);
                    SELECT CAST(SCOPE_IDENTITY() AS INT);";

                int newCouponId = await connection.ExecuteScalarAsync<int>(sql, new
                {
                    StoreId = storeId,
                    Code = cleanCode,
                    DiscountType = normalizedType,
                    request.DiscountValue,
                    request.MinPurchaseLps,
                    request.StartDate,
                    request.EndDate,
                    ExpiryDate = expiryToSave,
                    request.UsageLimit,
                    request.IsStackable
                });

                return Created($"/api/vendor/marketing/coupons/{newCouponId}", new
                {
                    Message = "Cupón creado exitosamente",
                    CouponId = newCouponId,
                    Code = cleanCode,
                    DiscountType = normalizedType,
                    request.DiscountValue,
                    request.MinPurchaseLps,
                    request.StartDate,
                    request.EndDate,
                    request.UsageLimit
                });
            }
            catch (SqlException ex)
            {
                return HandleSqlException(ex, "CreateCoupon");
            }
            catch (Exception ex)
            {
                return BadRequest(new ProblemDetails { Title = "Error al crear cupón", Detail = ex.Message });
            }
        }

        /// <summary>
        /// PUT /api/vendor/marketing/coupons/{id}
        /// Edita las fechas de vigencia, límite de uso o valor del descuento de un cupón existente.
        /// Condición SQL obligatoria: "WHERE Coupon_ID = @Id AND Store_ID = @StoreId".
        /// Auditoría: sp_set_session_context 'UsuarioID'.
        /// </summary>
        [HttpPut("coupons/{id}")]
        public async Task<IActionResult> UpdateCoupon(int id, [FromBody] UpdateCouponRequestDto request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            try
            {
                int storeId = GetStoreId();
                using var connection = _dapperContext.CreateConnection();

                // Auditoría antes de escribir
                await SetAuditContextAsync(connection);

                DateTime? expiryToSave = request.EndDate ?? request.ExpiryDate;

                var sql = @"
                    UPDATE [Catalog].[Coupons]
                    SET Discount_Value = @DiscountValue,
                        Min_Purchase_LPS = @MinPurchaseLps,
                        Start_Date = @StartDate,
                        End_Date = @EndDate,
                        Expiry_Date = @ExpiryDate,
                        Usage_Limit = @UsageLimit,
                        Is_Stackable = @IsStackable
                    WHERE Coupon_ID = @CouponId AND Store_ID = @StoreId";

                int rowsAffected = await connection.ExecuteAsync(sql, new
                {
                    request.DiscountValue,
                    request.MinPurchaseLps,
                    request.StartDate,
                    request.EndDate,
                    ExpiryDate = expiryToSave,
                    request.UsageLimit,
                    request.IsStackable,
                    CouponId = id,
                    StoreId = storeId
                });

                if (rowsAffected == 0)
                {
                    return NotFound(new ProblemDetails
                    {
                        Title = "Cupón no encontrado",
                        Status = StatusCodes.Status404NotFound,
                        Detail = $"El cupón ID {id} no existe o no pertenece a la tienda {storeId}."
                    });
                }

                return Ok(new { Message = "Cupón actualizado exitosamente", CouponId = id });
            }
            catch (SqlException ex)
            {
                return HandleSqlException(ex, "UpdateCoupon");
            }
            catch (Exception ex)
            {
                return BadRequest(new ProblemDetails { Title = "Error al actualizar cupón", Detail = ex.Message });
            }
        }

        /// <summary>
        /// DELETE /api/vendor/marketing/coupons/{id} (Soft Delete)
        /// Realiza el Soft Delete marcando Is_Active = 0.
        /// Condición SQL obligatoria: "WHERE Coupon_ID = @Id AND Store_ID = @StoreId".
        /// Auditoría: sp_set_session_context 'UsuarioID'.
        /// </summary>
        [HttpDelete("coupons/{id}")]
        public async Task<IActionResult> SoftDeleteCoupon(int id)
        {
            try
            {
                int storeId = GetStoreId();
                using var connection = _dapperContext.CreateConnection();

                // Auditoría antes de escribir
                await SetAuditContextAsync(connection);

                var sql = @"
                    UPDATE [Catalog].[Coupons]
                    SET Is_Active = 0
                    WHERE Coupon_ID = @CouponId AND Store_ID = @StoreId";

                int rowsAffected = await connection.ExecuteAsync(sql, new
                {
                    CouponId = id,
                    StoreId = storeId
                });

                if (rowsAffected == 0)
                {
                    return NotFound(new ProblemDetails
                    {
                        Title = "Cupón no encontrado",
                        Status = StatusCodes.Status404NotFound,
                        Detail = $"El cupón ID {id} no existe o no pertenece a la tienda {storeId}."
                    });
                }

                return NoContent(); // HTTP 204 No Content
            }
            catch (SqlException ex)
            {
                return HandleSqlException(ex, "SoftDeleteCoupon");
            }
            catch (Exception ex)
            {
                return BadRequest(new ProblemDetails { Title = "Error al desactivar cupón", Detail = ex.Message });
            }
        }
    }
}
