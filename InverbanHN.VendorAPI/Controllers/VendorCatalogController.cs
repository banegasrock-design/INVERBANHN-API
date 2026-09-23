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
    [Route("api/vendor/catalog")]
    [Authorize(Roles = "StoreAdmin")]
    public class VendorCatalogController : VendorBaseController
    {
        private readonly DapperContext _dapperContext;

        public VendorCatalogController(DapperContext dapperContext)
        {
            _dapperContext = dapperContext;
        }

        /// <summary>
        /// GET /api/vendor/catalog/items
        /// Lista los artículos del comercio usando paginación eficiente (OFFSET / FETCH NEXT).
        /// Filtrado obligatorio por Store_ID del JWT.
        /// </summary>
        [HttpGet("items")]
        public async Task<IActionResult> GetCatalogItems(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? search = null)
        {
            if (pageNumber < 1) pageNumber = 1;
            if (pageSize < 1) pageSize = 10;
            if (pageSize > 100) pageSize = 100;

            try
            {
                int storeId = GetStoreId();
                int offset = (pageNumber - 1) * pageSize;
                using var connection = _dapperContext.CreateConnection();

                var countSql = @"
                    SELECT COUNT(1) 
                    FROM [Catalog].[Products]
                    WHERE Store_ID = @StoreId
                      AND (@Search IS NULL OR Name LIKE '%' + @Search + '%' OR SKU LIKE '%' + @Search + '%')";

                var itemsSql = @"
                    SELECT 
                        Product_ID AS ProductId,
                        Store_ID AS StoreId,
                        SKU,
                        Name,
                        Description,
                        Price,
                        Stock,
                        ISNULL(Product_Type, 'Producto') AS ProductType,
                        Weight_Kg AS WeightKg,
                        Length_Cm AS LengthCm,
                        Width_Cm AS WidthCm,
                        Height_Cm AS HeightCm,
                        ISNULL(Is_Active, 1) AS IsActive,
                        ISNULL(Created_At, GETUTCDATE()) AS CreatedAt
                    FROM [Catalog].[Products]
                    WHERE Store_ID = @StoreId
                      AND (@Search IS NULL OR Name LIKE '%' + @Search + '%' OR SKU LIKE '%' + @Search + '%')
                    ORDER BY Product_ID DESC
                    OFFSET @Offset ROWS
                    FETCH NEXT @PageSize ROWS ONLY";

                var totalCount = await connection.ExecuteScalarAsync<int>(countSql, new { StoreId = storeId, Search = search });
                var items = await connection.QueryAsync<CatalogItemResponseDto>(itemsSql, new
                {
                    StoreId = storeId,
                    Search = search,
                    Offset = offset,
                    PageSize = pageSize
                });

                var pagedResult = new PagedResultDto<CatalogItemResponseDto>
                {
                    Items = items,
                    TotalCount = totalCount,
                    PageNumber = pageNumber,
                    PageSize = pageSize
                };

                return Ok(pagedResult);
            }
            catch (SqlException ex)
            {
                return HandleSqlException(ex, "GetCatalogItems");
            }
            catch (Exception ex)
            {
                return BadRequest(new ProblemDetails { Title = "Error al listar catálogo", Detail = ex.Message });
            }
        }

        /// <summary>
        /// POST /api/vendor/catalog/items
        /// Crea un nuevo producto/servicio en el catálogo.
        /// REGLAS:
        /// 1. Extrae Store_ID y User_ID desde los Claims del JWT.
        /// 2. Valida duplicado de SKU para la misma tienda.
        /// 3. Ejecuta sp_set_session_context 'UsuarioID' para auditoría.
        /// 4. Soporta tipos: Producto (Tangible), Servicio, Digital. Si es Servicio, stock y dimensiones quedan NULL.
        /// </summary>
        [HttpPost("items")]
        public async Task<IActionResult> CreateCatalogItem([FromBody] CreateUpdateCatalogItemRequestDto request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            try
            {
                int storeId = GetStoreId();
                using var connection = _dapperContext.CreateConnection();

                // Validación de SKU único por Store_ID
                var skuExists = await connection.ExecuteScalarAsync<bool>(@"
                    SELECT CASE WHEN EXISTS (
                        SELECT 1 FROM [Catalog].[Products] 
                        WHERE Store_ID = @StoreId AND SKU = @SKU
                    ) THEN 1 ELSE 0 END",
                    new { StoreId = storeId, SKU = request.SKU.Trim() });

                if (skuExists)
                {
                    return BadRequest(new ProblemDetails
                    {
                        Title = "SKU Duplicado",
                        Status = StatusCodes.Status400BadRequest,
                        Detail = $"El SKU '{request.SKU}' ya se encuentra registrado en el catálogo de su tienda."
                    });
                }

                // Auditoría: sp_set_session_context 'UsuarioID'
                await SetAuditContextAsync(connection);

                bool isService = string.Equals(request.ProductType, "Servicio", StringComparison.OrdinalIgnoreCase);

                int? stockToInsert = isService ? null : request.Stock;
                decimal? weightToInsert = isService ? null : request.WeightKg;
                decimal? lengthToInsert = isService ? null : request.LengthCm;
                decimal? widthToInsert = isService ? null : request.WidthCm;
                decimal? heightToInsert = isService ? null : request.HeightCm;

                var sql = @"
                    INSERT INTO [Catalog].[Products]
                        (Store_ID, SKU, Name, Description, Price, Stock, Product_Type, Weight_Kg, Length_Cm, Width_Cm, Height_Cm, Is_Active, Created_At)
                    VALUES
                        (@StoreId, @SKU, @Name, @Description, @Price, @Stock, @ProductType, @WeightKg, @LengthCm, @WidthCm, @HeightCm, @IsActive, GETUTCDATE());
                    SELECT CAST(SCOPE_IDENTITY() AS INT);";

                int newId = await connection.ExecuteScalarAsync<int>(sql, new
                {
                    StoreId = storeId,
                    SKU = request.SKU.Trim(),
                    request.Name,
                    request.Description,
                    request.Price,
                    Stock = stockToInsert,
                    ProductType = request.ProductType,
                    WeightKg = weightToInsert,
                    LengthCm = lengthToInsert,
                    WidthCm = widthToInsert,
                    HeightCm = heightToInsert,
                    request.IsActive
                });

                return Created($"/api/vendor/catalog/items/{newId}", new
                {
                    Message = "Producto creado exitosamente",
                    ProductId = newId,
                    StoreId = storeId,
                    SKU = request.SKU.Trim()
                });
            }
            catch (SqlException ex)
            {
                return HandleSqlException(ex, "CreateCatalogItem");
            }
            catch (Exception ex)
            {
                return BadRequest(new ProblemDetails { Title = "Error al crear producto", Detail = ex.Message });
            }
        }

        /// <summary>
        /// PUT /api/vendor/catalog/items/{id}
        /// Edita un artículo existente.
        /// REGLAS:
        /// 1. Garantiza filtro obligatorio "WHERE Product_ID = @Id AND Store_ID = @StoreId" para evitar accesos cruzados.
        /// 2. Ejecuta sp_set_session_context 'UsuarioID' antes de actualizar.
        /// </summary>
        [HttpPut("items/{id}")]
        public async Task<IActionResult> UpdateCatalogItem(int id, [FromBody] CreateUpdateCatalogItemRequestDto request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            try
            {
                int storeId = GetStoreId();
                using var connection = _dapperContext.CreateConnection();

                // Auditoría: sp_set_session_context 'UsuarioID'
                await SetAuditContextAsync(connection);

                bool isService = string.Equals(request.ProductType, "Servicio", StringComparison.OrdinalIgnoreCase);

                int? stockToInsert = isService ? null : request.Stock;
                decimal? weightToInsert = isService ? null : request.WeightKg;
                decimal? lengthToInsert = isService ? null : request.LengthCm;
                decimal? widthToInsert = isService ? null : request.WidthCm;
                decimal? heightToInsert = isService ? null : request.HeightCm;

                var sql = @"
                    UPDATE [Catalog].[Products]
                    SET SKU = @SKU,
                        Name = @Name,
                        Description = @Description,
                        Price = @Price,
                        Stock = @Stock,
                        Product_Type = @ProductType,
                        Weight_Kg = @WeightKg,
                        Length_Cm = @LengthCm,
                        Width_Cm = @WidthCm,
                        Height_Cm = @HeightCm,
                        Is_Active = @IsActive
                    WHERE Product_ID = @ProductId AND Store_ID = @StoreId";

                int rowsAffected = await connection.ExecuteAsync(sql, new
                {
                    SKU = request.SKU.Trim(),
                    request.Name,
                    request.Description,
                    request.Price,
                    Stock = stockToInsert,
                    ProductType = request.ProductType,
                    WeightKg = weightToInsert,
                    LengthCm = lengthToInsert,
                    WidthCm = widthToInsert,
                    HeightCm = heightToInsert,
                    request.IsActive,
                    ProductId = id,
                    StoreId = storeId
                });

                if (rowsAffected == 0)
                {
                    return NotFound(new ProblemDetails
                    {
                        Title = "Producto no encontrado o denegado",
                        Status = StatusCodes.Status404NotFound,
                        Detail = $"El producto ID {id} no existe o pertenece a otro comercio."
                    });
                }

                return Ok(new { Message = "Producto actualizado exitosamente", ProductId = id });
            }
            catch (SqlException ex)
            {
                return HandleSqlException(ex, "UpdateCatalogItem");
            }
            catch (Exception ex)
            {
                return BadRequest(new ProblemDetails { Title = "Error al actualizar producto", Detail = ex.Message });
            }
        }

        /// <summary>
        /// DELETE /api/vendor/catalog/items/{id} (Soft Delete)
        /// Desactiva la publicación del producto marcando Is_Active = 0.
        /// REGLAS:
        /// 1. NO ejecuta DELETE FROM para preservar el historial de órdenes pasadas.
        /// 2. Filtro obligatorio "WHERE Product_ID = @Id AND Store_ID = @StoreId".
        /// 3. Retorna 204 No Content si tuvo éxito, o 404 si no existe/no pertenece a la tienda.
        /// </summary>
        [HttpDelete("items/{id}")]
        public async Task<IActionResult> SoftDeleteCatalogItem(int id)
        {
            try
            {
                int storeId = GetStoreId();
                using var connection = _dapperContext.CreateConnection();

                // Auditoría: sp_set_session_context 'UsuarioID'
                await SetAuditContextAsync(connection);

                var sql = @"
                    UPDATE [Catalog].[Products]
                    SET Is_Active = 0
                    WHERE Product_ID = @ProductId AND Store_ID = @StoreId";

                int rowsAffected = await connection.ExecuteAsync(sql, new
                {
                    ProductId = id,
                    StoreId = storeId
                });

                if (rowsAffected == 0)
                {
                    return NotFound(new ProblemDetails
                    {
                        Title = "Producto no encontrado",
                        Status = StatusCodes.Status404NotFound,
                        Detail = $"El producto ID {id} no existe o no pertenece a su tienda."
                    });
                }

                return NoContent(); // HTTP 204 No Content
            }
            catch (SqlException ex)
            {
                return HandleSqlException(ex, "SoftDeleteCatalogItem");
            }
            catch (Exception ex)
            {
                return BadRequest(new ProblemDetails { Title = "Error al desactivar producto", Detail = ex.Message });
            }
        }

        /// <summary>
        /// PATCH /api/vendor/catalog/stock
        /// Ajuste rápido de inventario físico.
        /// </summary>
        [HttpPatch("stock")]
        public async Task<IActionResult> AdjustStock([FromBody] AdjustStockRequestDto request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            try
            {
                int storeId = GetStoreId();
                using var connection = _dapperContext.CreateConnection();

                // Auditoría: sp_set_session_context 'UsuarioID'
                await SetAuditContextAsync(connection);

                var sql = @"
                    UPDATE [Catalog].[Products]
                    SET Stock = @NewStock
                    WHERE Product_ID = @ProductId 
                      AND Store_ID = @StoreId 
                      AND (Product_Type IS NULL OR Product_Type = 'Producto' OR Product_Type = 'Tangible')";

                int rowsAffected = await connection.ExecuteAsync(sql, new
                {
                    request.NewStock,
                    request.ProductId,
                    StoreId = storeId
                });

                if (rowsAffected == 0)
                {
                    return NotFound(new ProblemDetails
                    {
                        Title = "Ajuste de inventario fallido",
                        Detail = "El producto no existe, pertenece a otra tienda o es de tipo Servicio."
                    });
                }

                return Ok(new
                {
                    Message = "Stock actualizado correctamente",
                    ProductId = request.ProductId,
                    NewStock = request.NewStock
                });
            }
            catch (SqlException ex)
            {
                return HandleSqlException(ex, "AdjustStock");
            }
            catch (Exception ex)
            {
                return BadRequest(new ProblemDetails { Title = "Error al ajustar stock", Detail = ex.Message });
            }
        }
    }
}
