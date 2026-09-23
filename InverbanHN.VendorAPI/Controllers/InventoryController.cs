using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Dapper;
using InverbanHN.Shared.Data;
using InverbanHN.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InverbanHN.VendorAPI.Controllers
{
    [ApiController]
    [Route("api/vendor/inventory")]
    [Authorize(Roles = "StoreAdmin,StoreOwner,SuperAdmin")]
    public class InventoryController : VendorBaseController
    {
        private readonly DapperContext _dapperContext;

        public InventoryController(DapperContext dapperContext)
        {
            _dapperContext = dapperContext;
        }

        [HttpGet("products")]
        public async Task<IActionResult> GetMyStoreProducts()
        {
            int storeId = GetStoreId();
            using var connection = _dapperContext.CreateConnection();
            var sql = @"
                SELECT 
                    p.Product_ID AS ProductId,
                    p.Store_ID AS StoreId,
                    p.SKU,
                    p.Name,
                    p.Description,
                    p.Category_ID AS CategoryId,
                    p.Brand,
                    p.Weight_Kg AS WeightKg,
                    p.Status_Name AS StatusName,
                    p.Created_At AS CreatedAt,
                    COALESCE(v.Price_Amount, 250.00) AS Price
                FROM [Catalog].[Products] p
                LEFT JOIN [Logistics].[Product_Variants] v ON p.Product_ID = v.Product_ID
                WHERE p.Store_ID = @StoreId
                ORDER BY p.Created_At DESC";

            var products = await connection.QueryAsync(sql, new { StoreId = storeId });
            return Ok(products);
        }

        [HttpPost("products")]
        public async Task<IActionResult> CreateProduct([FromBody] CreateProductRequest request)
        {
            int storeId = GetStoreId();
            using var connection = _dapperContext.CreateConnection();

            await SetAuditContextAsync(connection);

            var insertSql = @"
                INSERT INTO [Catalog].[Products] 
                    (Store_ID, SKU, Name, Description, Category_ID, Brand, Weight_Kg, Width_Cm, Height_Cm, Length_Cm, Status_Name, Created_At)
                VALUES 
                    (@StoreId, @Sku, @Name, @Description, @CategoryId, @Brand, @WeightKg, @WidthCm, @HeightCm, @LengthCm, 'Activo', GETUTCDATE());
                SELECT CAST(SCOPE_IDENTITY() AS INT);";

            var productId = await connection.ExecuteScalarAsync<int>(insertSql, new
            {
                StoreId = storeId,
                Sku = request.Sku,
                Name = request.Name,
                Description = request.Description,
                CategoryId = request.CategoryId > 0 ? request.CategoryId : 1,
                Brand = request.Brand,
                WeightKg = request.WeightKg,
                WidthCm = request.WidthCm,
                HeightCm = request.HeightCm,
                LengthCm = request.LengthCm
            });

            // Insert variant price
            var variantSql = @"
                INSERT INTO [Logistics].[Product_Variants] (Product_ID, SKU, Variant_Name, Variant_Description, Price_Amount, Offer_Type)
                VALUES (@ProductId, @Sku, @Name, @Description, @Price, 'Sin Oferta');";

            await connection.ExecuteAsync(variantSql, new
            {
                ProductId = productId,
                Sku = request.Sku,
                Name = request.Name,
                Description = request.Description,
                Price = request.Price
            });

            return CreatedAtAction(nameof(GetMyStoreProducts), new { }, new { ProductId = productId, StoreId = storeId, request.Name, request.Price });
        }

        [HttpDelete("products/{productId}")]
        public async Task<IActionResult> DeactivateProduct(int productId)
        {
            int storeId = GetStoreId();
            using var connection = _dapperContext.CreateConnection();

            await SetAuditContextAsync(connection);

            var rows = await connection.ExecuteAsync(
                "UPDATE [Catalog].[Products] SET Status_Name = 'Inactivo' WHERE Product_ID = @ProductId AND Store_ID = @StoreId",
                new { ProductId = productId, StoreId = storeId });

            if (rows == 0) return NotFound(new { Error = "Producto no encontrado o no pertenece a tu tienda." });
            return NoContent();
        }
    }
}
