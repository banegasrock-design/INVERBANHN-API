using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Dapper;
using InverbanHN.Shared.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InverbanHN.VendorAPI.Controllers
{
    [ApiController]
    [Route("api/vendor/dashboard")]
    [Authorize(Roles = "StoreAdmin,StoreOwner,SuperAdmin")]
    public class VendorDashboardController : VendorBaseController
    {
        private readonly DapperContext _dapperContext;

        public VendorDashboardController(DapperContext dapperContext)
        {
            _dapperContext = dapperContext;
        }

        [HttpGet("summary")]
        public async Task<IActionResult> GetDashboardSummary()
        {
            int storeId = GetStoreId();
            using var connection = _dapperContext.CreateConnection();

            var totalProducts = await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(1) FROM [Catalog].[Products] WHERE Store_ID = @StoreId", new { StoreId = storeId });

            var totalOrders = await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(1) FROM [dbo].[SubOrders] WHERE StoreId = @StoreId", new { StoreId = storeId });

            var storeInfo = await connection.QuerySingleOrDefaultAsync(
                "SELECT Store_ID AS StoreId, Store_Name AS StoreName, Store_Slug AS StoreSlug, Inventory_Mode AS InventoryMode, Billing_Type AS BillingType FROM [Core].[Stores] WHERE Store_ID = @StoreId",
                new { StoreId = storeId });

            return Ok(new
            {
                Store = storeInfo,
                TotalProducts = totalProducts,
                TotalOrders = totalOrders,
                StoreId = storeId
            });
        }
    }
}
