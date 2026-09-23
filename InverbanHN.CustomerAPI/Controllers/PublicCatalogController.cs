using System;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using InverbanHN.CustomerAPI.DTOs;
using InverbanHN.Shared.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace InverbanHN.CustomerAPI.Controllers
{
    [ApiController]
    [Route("api/public/catalog")]
    [AllowAnonymous]
    public class PublicCatalogController : CustomerBaseController
    {
        private readonly DapperContext _dapperContext;

        public PublicCatalogController(DapperContext dapperContext)
        {
            _dapperContext = dapperContext;
        }

        /// <summary>
        /// GET /api/public/catalog/search
        /// Búsqueda paginada de productos activos en el Marketplace.
        /// Filtros opcionales: Categoría, Rango de Precio y búsqueda LIKE.
        /// Retorna BasePrice y OfferPrice (si existe oferta vigente).
        /// </summary>
        [HttpGet("search")]
        public async Task<IActionResult> SearchCatalog(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 12,
            [FromQuery] string? category = null,
            [FromQuery] decimal? minPrice = null,
            [FromQuery] decimal? maxPrice = null,
            [FromQuery] string? search = null)
        {
            if (pageNumber < 1) pageNumber = 1;
            if (pageSize < 1) pageSize = 12;
            if (pageSize > 50) pageSize = 50;

            try
            {
                int offset = (pageNumber - 1) * pageSize;
                using var connection = _dapperContext.CreateConnection();

                var countSql = @"
                    SELECT COUNT(1) 
                    FROM [Catalog].[Products] P
                    INNER JOIN [Core].[Stores] S ON P.Store_ID = S.Store_ID
                    WHERE ISNULL(P.Is_Active, 1) = 1
                      AND ISNULL(S.Is_Active, 1) = 1
                      AND (@Category IS NULL OR P.Product_Type = @Category OR P.SKU LIKE '%' + @Category + '%')
                      AND (@MinPrice IS NULL OR P.Price >= @MinPrice)
                      AND (@MaxPrice IS NULL OR P.Price <= @MaxPrice)
                      AND (@Search IS NULL OR P.Name LIKE '%' + @Search + '%' OR P.Description LIKE '%' + @Search + '%' OR P.SKU LIKE '%' + @Search + '%')";

                var itemsSql = @"
                    SELECT 
                        P.Product_ID AS ProductId,
                        P.Store_ID AS StoreId,
                        S.Store_Name AS StoreName,
                        P.SKU,
                        P.Name,
                        P.Product_Type AS Category,
                        P.Price AS BasePrice,
                        (
                            SELECT TOP 1 Offer_Price 
                            FROM [Catalog].[Product_Offers] O 
                            WHERE O.Product_ID = P.Product_ID 
                              AND ISNULL(O.Is_Active, 1) = 1 
                              AND GETUTCDATE() BETWEEN O.Start_Date AND O.End_Date
                            ORDER BY Offer_Price ASC
                        ) AS OfferPrice,
                        P.Stock,
                        ISNULL(P.Product_Type, 'Producto') AS ProductType,
                        ISNULL(P.Is_Active, 1) AS IsActive
                    FROM [Catalog].[Products] P
                    INNER JOIN [Core].[Stores] S ON P.Store_ID = S.Store_ID
                    WHERE ISNULL(P.Is_Active, 1) = 1
                      AND ISNULL(S.Is_Active, 1) = 1
                      AND (@Category IS NULL OR P.Product_Type = @Category OR P.SKU LIKE '%' + @Category + '%')
                      AND (@MinPrice IS NULL OR P.Price >= @MinPrice)
                      AND (@MaxPrice IS NULL OR P.Price <= @MaxPrice)
                      AND (@Search IS NULL OR P.Name LIKE '%' + @Search + '%' OR P.Description LIKE '%' + @Search + '%' OR P.SKU LIKE '%' + @Search + '%')
                    ORDER BY P.Product_ID DESC
                    OFFSET @Offset ROWS
                    FETCH NEXT @PageSize ROWS ONLY";

                var totalCount = await connection.ExecuteScalarAsync<int>(countSql, new
                {
                    Category = category,
                    MinPrice = minPrice,
                    MaxPrice = maxPrice,
                    Search = search
                });

                var items = await connection.QueryAsync<PublicProductListItemDto>(itemsSql, new
                {
                    Category = category,
                    MinPrice = minPrice,
                    MaxPrice = maxPrice,
                    Search = search,
                    Offset = offset,
                    PageSize = pageSize
                });

                return Ok(new PagedResultDto<PublicProductListItemDto>
                {
                    Items = items,
                    TotalCount = totalCount,
                    PageNumber = pageNumber,
                    PageSize = pageSize
                });
            }
            catch (SqlException ex)
            {
                return HandleSqlException(ex, "SearchCatalog");
            }
            catch (Exception ex)
            {
                return BadRequest(new ProblemDetails { Title = "Error en búsqueda de catálogo", Detail = ex.Message });
            }
        }

        /// <summary>
        /// GET /api/public/catalog/product/{id}
        /// Detalle completo de un producto en el Marketplace, incluyendo nombre y logo de la tienda proveedora.
        /// </summary>
        [HttpGet("product/{id}")]
        public async Task<IActionResult> GetProductDetail(int id)
        {
            try
            {
                using var connection = _dapperContext.CreateConnection();

                var sql = @"
                    SELECT 
                        P.Product_ID AS ProductId,
                        P.Store_ID AS StoreId,
                        S.Store_Name AS StoreName,
                        COALESCE(S.Logo_Url, S.Store_Logo) AS StoreLogo,
                        P.SKU,
                        P.Name,
                        P.Description,
                        P.Product_Type AS Category,
                        P.Price AS BasePrice,
                        (
                            SELECT TOP 1 Offer_Price 
                            FROM [Catalog].[Product_Offers] O 
                            WHERE O.Product_ID = P.Product_ID 
                              AND ISNULL(O.Is_Active, 1) = 1 
                              AND GETUTCDATE() BETWEEN O.Start_Date AND O.End_Date
                            ORDER BY Offer_Price ASC
                        ) AS OfferPrice,
                        P.Stock,
                        ISNULL(P.Product_Type, 'Producto') AS ProductType,
                        P.Weight_Kg AS WeightKg,
                        ISNULL(P.Is_Active, 1) AS IsActive
                    FROM [Catalog].[Products] P
                    INNER JOIN [Core].[Stores] S ON P.Store_ID = S.Store_ID
                    WHERE P.Product_ID = @ProductId AND ISNULL(P.Is_Active, 1) = 1";

                var product = await connection.QuerySingleOrDefaultAsync<PublicProductDetailDto>(sql, new { ProductId = id });

                if (product == null)
                {
                    return NotFound(new ProblemDetails
                    {
                        Title = "Producto no encontrado",
                        Status = StatusCodes.Status404NotFound,
                        Detail = $"El producto con ID {id} no existe o no se encuentra activo."
                    });
                }

                return Ok(product);
            }
            catch (SqlException ex)
            {
                return HandleSqlException(ex, "GetProductDetail");
            }
            catch (Exception ex)
            {
                return BadRequest(new ProblemDetails { Title = "Error al consultar detalle del producto", Detail = ex.Message });
            }
        }
    }
}
