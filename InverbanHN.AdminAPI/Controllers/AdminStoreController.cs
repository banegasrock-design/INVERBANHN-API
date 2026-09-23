using System;
using System.Threading.Tasks;
using Dapper;
using InverbanHN.Shared.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InverbanHN.AdminAPI.Controllers;

[ApiController]
[Route("api/admin/stores")]
[Authorize(AuthenticationSchemes = "Bearer,ApiKey", Roles = "SuperAdmin")]
public class AdminStoreController : ControllerBase
{
    private readonly DapperContext _dapperContext;

    public AdminStoreController(DapperContext dapperContext)
    {
        _dapperContext = dapperContext;
    }

    [HttpGet]
    public async Task<IActionResult> GetAllStores()
    {
        using var connection = _dapperContext.CreateConnection();
        var sql = @"
            SELECT 
                Store_ID AS StoreId,
                Store_Name AS StoreName,
                Store_Slug AS StoreSlug,
                Tax_ID_RTN AS TaxIdRtn,
                Inventory_Mode AS InventoryMode,
                Billing_Type AS BillingType,
                Owner_User_ID AS OwnerUserId,
                Is_Active AS IsActive,
                Created_At AS CreatedAt
            FROM [Core].[Stores]
            ORDER BY Store_ID DESC";

        var stores = await connection.QueryAsync(sql);
        return Ok(stores);
    }

    [HttpPost]
    public async Task<IActionResult> CreateStore([FromBody] CreateStoreAdminRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.StoreName))
            return BadRequest(new { Error = "El nombre de la tienda es obligatorio." });

        using var connection = _dapperContext.CreateConnection();
        var sql = @"
            INSERT INTO [Core].[Stores] 
                (Store_Name, Store_Slug, Tax_ID_RTN, Inventory_Mode, Billing_Type, Store_Type, Owner_User_ID, Is_Active, Created_At)
            VALUES 
                (@StoreName, @StoreSlug, @TaxIdRtn, @InventoryMode, @BillingType, 'Marketplace', @OwnerUserId, 1, GETUTCDATE());
            SELECT SCOPE_IDENTITY();";

        var slug = request.StoreName.ToLower().Replace(" ", "-");
        var storeId = await connection.ExecuteScalarAsync<int>(sql, new
        {
            StoreName = request.StoreName,
            StoreSlug = slug,
            TaxIdRtn = request.TaxIdRtn ?? "08019999000000",
            InventoryMode = request.InventoryMode ?? "Ecommerce",
            BillingType = request.BillingType ?? "Managed",
            OwnerUserId = request.OwnerUserId > 0 ? request.OwnerUserId : (int?)null
        });

        return Ok(new { Message = "Tienda creada exitosamente por SuperAdmin", StoreId = storeId, request.StoreName });
    }

    [HttpPut("{storeId}/toggle-status")]
    public async Task<IActionResult> ToggleStoreStatus(int storeId, [FromQuery] bool isActive)
    {
        using var connection = _dapperContext.CreateConnection();
        var rows = await connection.ExecuteAsync(
            "UPDATE [Core].[Stores] SET Is_Active = @IsActive WHERE Store_ID = @StoreId",
            new { IsActive = isActive, StoreId = storeId });

        if (rows == 0) return NotFound(new { Error = "Tienda no encontrada." });
        return Ok(new { Message = "Estado de tienda actualizado.", StoreId = storeId, IsActive = isActive });
    }
}

public class CreateStoreAdminRequest
{
    public string StoreName { get; set; } = string.Empty;
    public string? TaxIdRtn { get; set; }
    public string? InventoryMode { get; set; } = "Ecommerce";
    public string? BillingType { get; set; } = "Managed";
    public int OwnerUserId { get; set; }
}
