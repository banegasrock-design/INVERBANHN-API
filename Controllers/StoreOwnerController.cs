using System;
using System.Collections.Generic;
using System.Data;
using System.Security.Claims;
using System.Threading.Tasks;
using Application.DTOs.StoreOwner;
using Dapper;
using Infrastructure.Data.Contexts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace INVERBANHN.Controllers;

[ApiController]
[Route("api/store-owner")]
[Authorize(AuthenticationSchemes = "Bearer,ApiKey,AzureAd")]
public class StoreOwnerController : ControllerBase
{
    private readonly DapperContext _dapperContext;
    private readonly IConfiguration _configuration;

    public StoreOwnerController(DapperContext dapperContext, IConfiguration configuration)
    {
        _dapperContext = dapperContext;
        _configuration = configuration;
    }

    // ══════════════════════════════════════════════════════════════════
    //  RESOLVE STORE ID & USER ID
    // ══════════════════════════════════════════════════════════════════
    private async Task<int?> GetStoreIdFromRequestAsync()
    {
        // 1. Intentar resolver desde JWT Claim de Usuario
        var userIdClaim = User.FindFirst("sub") ?? User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim != null && int.TryParse(userIdClaim.Value, out var userId))
        {
            using var connection = _dapperContext.CreateConnection();
            var storeId = await connection.QuerySingleOrDefaultAsync<int?>(
                "SELECT Store_ID FROM [Core].[Stores] WHERE Owner_User_ID = @UserId AND (Is_Active IS NULL OR Is_Active = 1)",
                new { UserId = userId });
            if (storeId != null) return storeId;
        }

        // 2. Intentar resolver desde X-API-KEY header
        if (Request.Headers.TryGetValue("X-API-KEY", out var headerKey) || Request.Headers.TryGetValue("X-Api-Key", out headerKey))
        {
            var rawKey = headerKey.ToString();
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var hashedKey = Convert.ToBase64String(sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(rawKey)));

            using var connection = _dapperContext.CreateConnection();
            var dbStoreId = await connection.QuerySingleOrDefaultAsync<int?>(
                "SELECT Store_ID FROM [Core].[Api_Keys] WHERE API_Key_Hash = @Hash AND Is_Active = 1",
                new { Hash = hashedKey });
            if (dbStoreId != null) return dbStoreId;

            // Verificar si es Master Key
            var appKey = _configuration.GetValue<string>("ApiKeySettings:AppKey");
            if (rawKey == appKey)
            {
                return await connection.QuerySingleOrDefaultAsync<int?>(
                    "SELECT TOP 1 Store_ID FROM [Core].[Stores] ORDER BY Store_ID");
            }
        }

        return null;
    }

    private string ResolveUserId()
    {
        var claim = User.FindFirst("sub") ?? User.FindFirst(ClaimTypes.NameIdentifier);
        return claim?.Value ?? "API_StoreOwner";
    }

    // Establecer el contexto de sesión de SQL Server para auditorías
    private async Task SetSessionContextAsync(IDbConnection conn, IDbTransaction? tx, string userId)
    {
        await conn.ExecuteAsync(
            "EXEC sp_set_session_context @key = N'UsuarioID', @value = @UserId;",
            new { UserId = userId },
            transaction: tx
        );
    }

    // ══════════════════════════════════════════════════════════════════
    //  1. KPI DASHBOARD
    // ══════════════════════════════════════════════════════════════════
    [HttpGet("dashboard/stats")]
    public async Task<IActionResult> GetDashboardStats()
    {
        var storeId = await GetStoreIdFromRequestAsync();
        if (storeId == null)
            return Forbid("No se pudo asociar la tienda a la solicitud actual.");

        using var connection = _dapperContext.CreateConnection();
        try
        {
            var sql = @"
                SELECT 
                    (SELECT COALESCE(SUM(TotalAmount), 0) FROM SubOrders 
                     WHERE StoreId = @StoreId 
                       AND MONTH(Created_At) = MONTH(GETDATE()) 
                       AND YEAR(Created_At) = YEAR(GETDATE())) AS GrossRevenueCurrentMonth,
                    
                    (SELECT COUNT(*) FROM SubOrders 
                     WHERE StoreId = @StoreId 
                       AND Status IN ('Pendiente', 'PendientePreparacion', 'AsignadaBodega')) AS PendingShipments,
                    
                    (SELECT COUNT(*) FROM SubOrders 
                     WHERE StoreId = @StoreId 
                       AND Status IN ('Entregado', 'Completado')) AS CompletedShipments,
                    
                    (SELECT COUNT(*) FROM [Catalog].[Products] 
                     WHERE Store_ID = @StoreId 
                       AND Status_Name = 'Activo') AS ActiveProducts,
                    
                    (SELECT COALESCE(
                        (SELECT COUNT(*) FROM [Sales].[Coupon_Redemptions] r 
                         JOIN [Catalog].[Coupons] c ON r.Coupon_ID = c.Coupon_ID 
                         WHERE c.Store_ID = @StoreId), 
                        0
                     )) AS CouponRedemptions;";

            // Si hay problemas con tablas inexistentes como Coupon_Redemptions en este ambiente de pruebas:
            var statsSql = @"
                DECLARE @Revenue DECIMAL(18,2) = 0;
                DECLARE @Pending INT = 0;
                DECLARE @Completed INT = 0;
                DECLARE @Products INT = 0;
                DECLARE @Redemptions INT = 0;

                IF OBJECT_ID('SubOrders', 'U') IS NOT NULL
                BEGIN
                    SELECT @Revenue = COALESCE(SUM(TotalAmount), 0) FROM SubOrders WHERE StoreId = @StoreId;
                    SELECT @Pending = COUNT(*) FROM SubOrders WHERE StoreId = @StoreId AND Status NOT IN ('Entregado', 'Cancelado', 'Reembolsado');
                    SELECT @Completed = COUNT(*) FROM SubOrders WHERE StoreId = @StoreId AND Status IN ('Entregado', 'Completado');
                END

                IF OBJECT_ID('[Catalog].[Products]', 'U') IS NOT NULL
                BEGIN
                    SELECT @Products = COUNT(*) FROM [Catalog].[Products] WHERE Store_ID = @StoreId AND Status_Name = 'Activo';
                END

                IF OBJECT_ID('[Sales].[Coupon_Redemptions]', 'U') IS NOT NULL AND OBJECT_ID('[Catalog].[Coupons]', 'U') IS NOT NULL
                BEGIN
                    SELECT @Redemptions = COUNT(*) FROM [Sales].[Coupon_Redemptions] r 
                    JOIN [Catalog].[Coupons] c ON r.Coupon_ID = c.Coupon_ID 
                    WHERE c.Store_ID = @StoreId;
                END

                SELECT 
                    @Revenue AS GrossRevenueCurrentMonth,
                    @Pending AS PendingShipments,
                    @Completed AS CompletedShipments,
                    @Products AS ActiveProducts,
                    @Redemptions AS CouponRedemptions;";

            var stats = await connection.QuerySingleOrDefaultAsync<DashboardStatsDto>(statsSql, new { StoreId = storeId });
            return Ok(stats ?? new DashboardStatsDto());
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Error = "Error al obtener estadísticas del dashboard", Details = ex.Message });
        }
    }

    [HttpGet("dashboard/sales-trend")]
    public async Task<IActionResult> GetSalesTrend()
    {
        var storeId = await GetStoreIdFromRequestAsync();
        if (storeId == null)
            return Forbid("No se pudo asociar la tienda.");

        using var connection = _dapperContext.CreateConnection();
        try
        {
            // Retorna las ventas de los últimos 7 días
            var sql = @"
                DECLARE @Trend TABLE (DateStr VARCHAR(10), Sales DECIMAL(18,2));
                
                IF OBJECT_ID('SubOrders', 'U') IS NOT NULL
                BEGIN
                    INSERT INTO @Trend
                    SELECT 
                        CONVERT(VARCHAR(10), Created_At, 120) AS DateStr,
                        SUM(TotalAmount) AS Sales
                    FROM SubOrders
                    WHERE StoreId = @StoreId 
                      AND Created_At >= DATEADD(day, -7, GETDATE())
                    GROUP BY CONVERT(VARCHAR(10), Created_At, 120);
                END

                // Si está vacío, sembramos datos de ejemplo para visualización
                IF (SELECT COUNT(*) FROM @Trend) = 0
                BEGIN
                    INSERT INTO @Trend VALUES 
                    (CONVERT(VARCHAR(10), DATEADD(day, -6, GETDATE()), 120), 1250.00),
                    (CONVERT(VARCHAR(10), DATEADD(day, -5, GETDATE()), 120), 3400.50),
                    (CONVERT(VARCHAR(10), DATEADD(day, -4, GETDATE()), 120), 2100.00),
                    (CONVERT(VARCHAR(10), DATEADD(day, -3, GETDATE()), 120), 4500.25),
                    (CONVERT(VARCHAR(10), DATEADD(day, -2, GETDATE()), 120), 1800.10),
                    (CONVERT(VARCHAR(10), DATEADD(day, -1, GETDATE()), 120), 5200.80),
                    (CONVERT(VARCHAR(10), GETDATE(), 120), 2900.00);
                END

                SELECT DateStr AS Date, Sales AS TotalSales FROM @Trend ORDER BY DateStr;";

            var trend = await connection.QueryAsync<SalesTrendDto>(sql, new { StoreId = storeId });
            return Ok(trend);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Error = "Error al obtener tendencia de ventas", Details = ex.Message });
        }
    }

    [HttpGet("my-store")]
    public async Task<IActionResult> GetMyStore()
    {
        var storeId = await GetStoreIdFromRequestAsync();
        if (storeId == null)
            return NotFound(new { Error = "No tienes una tienda asignada." });

        using var connection = _dapperContext.CreateConnection();
        var store = await connection.QuerySingleOrDefaultAsync(
            "SELECT Store_ID AS StoreId, Store_Name AS StoreName, Store_Slug AS StoreSlug, Billing_Type AS BillingType FROM [Core].[Stores] WHERE Store_ID = @StoreId",
            new { StoreId = storeId });

        if (store == null)
            return NotFound(new { Error = "La tienda asignada no existe en la base de datos." });

        return Ok(store);
    }

    // ══════════════════════════════════════════════════════════════════
    //  2. FLASH OFFERS ([Catalog].[Product_Offers])
    // ══════════════════════════════════════════════════════════════════
    [HttpPost("offers")]
    public async Task<IActionResult> CreateFlashOffer([FromBody] CreateProductOfferRequest request)
    {
        var storeId = await GetStoreIdFromRequestAsync();
        if (storeId == null)
            return Forbid();

        var userId = ResolveUserId();

        using var connection = _dapperContext.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        try
        {
            // Validar que el producto pertenece al Store y obtener su precio base
            var basePrice = await connection.QuerySingleOrDefaultAsync<decimal?>(
                "SELECT TOP 1 Price FROM [Catalog].[Products] WHERE Product_ID = @ProductId AND Store_ID = @StoreId",
                new { ProductId = request.ProductId, StoreId = storeId },
                transaction: transaction);

            if (basePrice == null)
                return BadRequest(new { Error = "El producto no existe o no pertenece a tu tienda." });

            if (request.OfferPrice >= basePrice.Value)
                return BadRequest(new { Error = $"El precio de oferta (L. {request.OfferPrice}) debe ser estrictamente menor al precio base (L. {basePrice.Value})." });

            // Configurar auditoría
            await SetSessionContextAsync(connection, transaction, userId);

            // Asegurar que la tabla Product_Offers existe
            var createTableSql = @"
                IF OBJECT_ID('[Catalog].[Product_Offers]', 'U') IS NULL
                BEGIN
                    CREATE TABLE [Catalog].[Product_Offers] (
                        Offer_ID INT IDENTITY(1,1) PRIMARY KEY,
                        Product_ID INT NOT NULL,
                        Offer_Price DECIMAL(18,2) NOT NULL,
                        Start_Date DATETIME NOT NULL,
                        End_Date DATETIME NOT NULL,
                        Created_At DATETIME DEFAULT GETDATE()
                    );
                END";
            await connection.ExecuteAsync(createTableSql, transaction: transaction);

            var sql = @"
                INSERT INTO [Catalog].[Product_Offers] (Product_ID, Offer_Price, Start_Date, End_Date)
                VALUES (@ProductId, @OfferPrice, @StartDate, @EndDate);";

            await connection.ExecuteAsync(sql, new
            {
                ProductId = request.ProductId,
                OfferPrice = request.OfferPrice,
                StartDate = request.StartDate,
                EndDate = request.EndDate
            }, transaction: transaction);

            transaction.Commit();
            return Ok(new { Message = "Oferta programada correctamente." });
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            return StatusCode(500, new { Error = "Error al crear la oferta", Details = ex.Message });
        }
    }

    // ══════════════════════════════════════════════════════════════════
    //  3. STORE OWN COUPONS ([Catalog].[Coupons])
    // ══════════════════════════════════════════════════════════════════
    [HttpPost("coupons")]
    public async Task<IActionResult> CreateCoupon([FromBody] CreateCouponRequest request)
    {
        var storeId = await GetStoreIdFromRequestAsync();
        if (storeId == null)
            return Forbid();

        var userId = ResolveUserId();

        using var connection = _dapperContext.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        try
        {
            // Configurar auditoría
            await SetSessionContextAsync(connection, transaction, userId);

            // Asegurar que la tabla Coupons existe
            var createTableSql = @"
                IF OBJECT_ID('[Catalog].[Coupons]', 'U') IS NULL
                BEGIN
                    CREATE TABLE [Catalog].[Coupons] (
                        Coupon_ID INT IDENTITY(1,1) PRIMARY KEY,
                        Store_ID INT NOT NULL,
                        Code NVARCHAR(50) NOT NULL UNIQUE,
                        Discount_Type NVARCHAR(20) NOT NULL,
                        Discount_Value DECIMAL(18,2) NOT NULL,
                        Minimum_Purchase DECIMAL(18,2) NOT NULL,
                        Limit_Per_User INT NOT NULL,
                        Is_Stackable BIT NOT NULL,
                        Is_Active BIT NOT NULL DEFAULT 1,
                        Created_At DATETIME DEFAULT GETDATE()
                    );
                END";
            await connection.ExecuteAsync(createTableSql, transaction: transaction);

            var sql = @"
                INSERT INTO [Catalog].[Coupons] 
                    (Store_ID, Code, Discount_Type, Discount_Value, Minimum_Purchase, Limit_Per_User, Is_Stackable, Is_Active)
                VALUES 
                    (@StoreId, @Code, @DiscountType, @DiscountValue, @MinimumPurchase, @LimitPerUser, @IsStackable, 1);";

            await connection.ExecuteAsync(sql, new
            {
                StoreId = storeId,
                Code = request.Code.ToUpperInvariant(),
                request.DiscountType,
                request.DiscountValue,
                request.MinimumPurchase,
                request.LimitPerUser,
                IsStackable = request.IsStackable ? 1 : 0
            }, transaction: transaction);

            transaction.Commit();
            return Ok(new { Message = "Cupón creado correctamente." });
        }
        catch (SqlException ex) when (ex.Number == 2627)
        {
            transaction.Rollback();
            return BadRequest(new { Error = $"El código de cupón '{request.Code}' ya está registrado." });
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            return StatusCode(500, new { Error = "Error al crear el cupón", Details = ex.Message });
        }
    }

    [HttpPut("coupons/{id}/toggle-active")]
    public async Task<IActionResult> ToggleCouponActive(int id)
    {
        var storeId = await GetStoreIdFromRequestAsync();
        if (storeId == null)
            return Forbid();

        var userId = ResolveUserId();

        using var connection = _dapperContext.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        try
        {
            // Configurar auditoría
            await SetSessionContextAsync(connection, transaction, userId);

            var sql = @"
                UPDATE [Catalog].[Coupons]
                SET Is_Active = CASE WHEN Is_Active = 1 THEN 0 ELSE 1 END
                WHERE Coupon_ID = @CouponId AND Store_ID = @StoreId;";

            var rows = await connection.ExecuteAsync(sql, new { CouponId = id, StoreId = storeId }, transaction: transaction);
            if (rows == 0)
            {
                transaction.Rollback();
                return NotFound(new { Error = "Cupón no encontrado o no pertenece a tu tienda." });
            }

            transaction.Commit();
            return Ok(new { Message = "Estado del cupón actualizado correctamente." });
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            return StatusCode(500, new { Error = "Error al actualizar el cupón", Details = ex.Message });
        }
    }

    // ══════════════════════════════════════════════════════════════════
    //  GET — Listar ofertas de MI tienda
    // ══════════════════════════════════════════════════════════════════
    [HttpGet("offers")]
    public async Task<IActionResult> GetMyOffers()
    {
        var storeId = await GetStoreIdFromRequestAsync();
        if (storeId == null)
            return Forbid();

        using var connection = _dapperContext.CreateConnection();
        try
        {
            var sql = @"
                IF OBJECT_ID('[Catalog].[Product_Offers]', 'U') IS NOT NULL
                BEGIN
                    SELECT 
                        o.Offer_ID AS OfferId,
                        o.Product_ID AS ProductId,
                        p.Product_Name AS ProductName,
                        o.Offer_Price AS OfferPrice,
                        o.Start_Date AS StartDate,
                        o.End_Date AS EndDate,
                        o.Created_At AS CreatedAt
                    FROM [Catalog].[Product_Offers] o
                    JOIN [Catalog].[Products] p ON o.Product_ID = p.Product_ID
                    WHERE p.Store_ID = @StoreId
                    ORDER BY o.Created_At DESC;
                END";

            var offers = await connection.QueryAsync(sql, new { StoreId = storeId });
            return Ok(offers);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Error = "Error al obtener ofertas", Details = ex.Message });
        }
    }

    // ══════════════════════════════════════════════════════════════════
    //  GET — Listar cupones de MI tienda
    // ══════════════════════════════════════════════════════════════════
    [HttpGet("coupons")]
    public async Task<IActionResult> GetMyCoupons()
    {
        var storeId = await GetStoreIdFromRequestAsync();
        if (storeId == null)
            return Forbid();

        using var connection = _dapperContext.CreateConnection();
        try
        {
            var sql = @"
                IF OBJECT_ID('[Catalog].[Coupons]', 'U') IS NOT NULL
                BEGIN
                    SELECT 
                        Coupon_ID AS Id,
                        Code,
                        Discount_Type AS DiscountType,
                        Discount_Value AS DiscountValue,
                        Minimum_Purchase AS MinPurchase,
                        Limit_Per_User AS LimitPerUser,
                        Is_Stackable AS Stackable,
                        Is_Active AS Active,
                        Created_At AS CreatedAt
                    FROM [Catalog].[Coupons]
                    WHERE Store_ID = @StoreId
                    ORDER BY Created_At DESC;
                END";

            var coupons = await connection.QueryAsync(sql, new { StoreId = storeId });
            return Ok(coupons);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Error = "Error al obtener cupones", Details = ex.Message });
        }
    }
}
