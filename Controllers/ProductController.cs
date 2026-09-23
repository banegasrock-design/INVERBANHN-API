using System.Data;
using System.Threading.Tasks;
using Application.DTOs.Product;
using Application.Interfaces;
using Dapper;
using Infrastructure.Data.Contexts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace INVERBANHN.Controllers;

/// <summary>
/// Gestión de Productos del Marketplace.
/// El Store_ID se inyecta automáticamente desde el JWT — los vendedores solo ven y gestionan sus propios productos.
/// </summary>
[ApiController]
[Route("api/products")]
[Authorize(AuthenticationSchemes = "Bearer,ApiKey,AzureAd")]
public class ProductController : ControllerBase
{
    private readonly IProductService _productService;
    private readonly DapperContext _dapperContext;

    public ProductController(IProductService productService, DapperContext dapperContext)
    {
        _productService = productService;
        _dapperContext = dapperContext;
    }

    // ══════════════════════════════════════════════════════════════════
    //  HELPER: Extrae el Store_ID de la tienda del usuario autenticado.
    //  Busca en [Core].[Stores] por Owner_User_ID (claim del JWT).
    //  Si el usuario no tiene tienda, devuelve null.
    // ══════════════════════════════════════════════════════════════════
    private async Task<int?> ResolveStoreIdFromJwtAsync()
    {
        // El claim "sub" del JWT contiene el User_ID del vendedor autenticado.
        var userIdClaim = User.FindFirst("sub")
                       ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);

        if (userIdClaim != null && int.TryParse(userIdClaim.Value, out var userId))
        {
            using var connection = _dapperContext.CreateConnection();
            var storeId = await connection.QuerySingleOrDefaultAsync<int?>(
                "SELECT Store_ID FROM [Core].[Stores] WHERE Owner_User_ID = @UserId AND (Is_Active IS NULL OR Is_Active = 1)",
                new { UserId = userId });

            if (storeId != null) return storeId;
        }

        // Soporte para autenticación por API Key (SuperAdmin_ApiKey)
        if (User.Identity?.AuthenticationType == "ApiKey" || userIdClaim?.Value == "SuperAdmin_ApiKey")
        {
            using var connection = _dapperContext.CreateConnection();
            // Retornar tienda 2 por defecto si existe, o la primera activa
            var storeId = await connection.QuerySingleOrDefaultAsync<int?>(
                "SELECT TOP 1 Store_ID FROM [Core].[Stores] WHERE Store_ID = 2 OR (Is_Active IS NULL OR Is_Active = 1) ORDER BY CASE WHEN Store_ID = 2 THEN 0 ELSE 1 END");
            return storeId;
        }

        return null;
    }

    // ══════════════════════════════════════════════════════════════════
    //  GET — Listar mis productos (filtrado automático por Store_ID del JWT)
    // ══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Retorna todos los productos activos de la tienda del vendedor autenticado.
    /// El Store_ID se inyecta automáticamente desde el token JWT. No requiere parámetros.
    /// </summary>
    [HttpGet("my")]
    [ProducesResponseType(typeof(IEnumerable<ProductDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetMyProducts()
    {
        var storeId = await ResolveStoreIdFromJwtAsync();
        if (storeId is null)
            return Forbid(); // Token válido pero el usuario no tiene una tienda asignada

        var result = await _productService.GetMyProductsAsync(storeId.Value);
        return result.IsSuccess ? Ok(result.Data) : BadRequest(new { Error = result.Error });
    }

    // ══════════════════════════════════════════════════════════════════
    //  POST — Crear producto (Store_ID viene del JWT, no del body)
    // ══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Crea un nuevo producto en la tienda del vendedor autenticado.
    /// El campo Store_ID es inyectado automáticamente — el vendedor no puede subir
    /// productos a nombre de otra tienda.
    /// Las dimensiones (peso, largo, ancho, alto) son obligatorias para el motor
    /// de cotización de envíos con CAEX y Cargo Expreso.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateProduct([FromBody] CreateProductRequest request)
    {
        // FluentValidation ya verificó dimensiones, SKU y categoría antes de llegar aquí.

        var storeId = await ResolveStoreIdFromJwtAsync();
        if (storeId is null)
            return Forbid();

        var result = await _productService.CreateProductAsync(storeId.Value, request);
        if (!result.IsSuccess)
            return BadRequest(new { Error = result.Error });

        return CreatedAtAction(nameof(GetMyProducts), new { }, result.Data);
    }

    // ══════════════════════════════════════════════════════════════════
    //  PUT — Actualizar producto (solo productos de la propia tienda)
    // ══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Actualiza un producto existente. El servicio verifica que el producto
    /// pertenezca a la tienda del vendedor autenticado antes de modificarlo.
    /// </summary>
    [HttpPut("{productId}")]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpdateProduct(int productId, [FromBody] CreateProductRequest request)
    {
        var storeId = await ResolveStoreIdFromJwtAsync();
        if (storeId is null)
            return Forbid();

        var result = await _productService.UpdateProductAsync(storeId.Value, productId, request);
        return result.IsSuccess ? Ok(result.Data) : BadRequest(new { Error = result.Error });
    }

    // ══════════════════════════════════════════════════════════════════
    //  DELETE — Desactivar producto (soft delete)
    // ══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Desactiva un producto (no lo elimina de la base de datos).
    /// Útil para retirar temporalmente un producto del catálogo sin perder historial de ventas.
    /// </summary>
    [HttpDelete("{productId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DeactivateProduct(int productId)
    {
        var storeId = await ResolveStoreIdFromJwtAsync();
        if (storeId is null)
            return Forbid();

        var result = await _productService.DeactivateProductAsync(storeId.Value, productId);
        return result.IsSuccess ? NoContent() : BadRequest(new { Error = result.Error });
    }

    // ══════════════════════════════════════════════════════════════════
    //  POST — Bulk Upload (Excel)
    // ══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Sube masivamente productos desde un archivo Excel (.xlsx).
    /// El proceso valida cada fila y devuelve un reporte detallado de los errores.
    /// Las filas válidas se insertan en bloque, y las filas con errores son omitidas.
    /// </summary>
    [HttpPost("bulk-upload")]
    [ProducesResponseType(typeof(BulkProductUploadResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(BulkProductUploadResult), StatusCodes.Status207MultiStatus)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> BulkUploadProducts(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new { Error = "Debe adjuntar un archivo Excel (.xlsx)." });
        }

        if (!file.FileName.EndsWith(".xlsx", System.StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { Error = "Solo se permiten archivos con extensión .xlsx." });
        }

        var storeId = await ResolveStoreIdFromJwtAsync();
        if (storeId is null)
            return Forbid();

        using var stream = file.OpenReadStream();
        var result = await _productService.BulkUploadProductsAsync(storeId.Value, stream);

        if (!result.IsSuccess)
        {
            return BadRequest(new { Error = result.Error });
        }

        // Si hay errores parciales pero se procesaron algunos, devolvemos 207 Multi-Status
        if (result.Data!.FailedCount > 0 && result.Data!.SuccessfulCount > 0)
        {
            return StatusCode(StatusCodes.Status207MultiStatus, result.Data);
        }

        return Ok(result.Data);
    }

    // ══════════════════════════════════════════════════════════════════
    //  GET — Obtener productos por Store_ID (para SuperAdmin / Soporte)
    // ══════════════════════════════════════════════════════════════════
    [HttpGet("store/{storeId}")]
    [Authorize(AuthenticationSchemes = "Bearer,ApiKey,AzureAd", Roles = "SuperAdmin,CustomerService")]
    public async Task<IActionResult> GetProductsByStore(int storeId)
    {
        var result = await _productService.GetMyProductsAsync(storeId);
        return result.IsSuccess ? Ok(result.Data) : BadRequest(new { Error = result.Error });
    }

    // ══════════════════════════════════════════════════════════════════
    //  POST — Crear producto en Store_ID (para SuperAdmin / Soporte)
    // ══════════════════════════════════════════════════════════════════
    [HttpPost("store/{storeId}")]
    [Authorize(AuthenticationSchemes = "Bearer,ApiKey,AzureAd", Roles = "SuperAdmin,CustomerService")]
    public async Task<IActionResult> CreateProductForStore(int storeId, [FromBody] CreateProductRequest request)
    {
        var result = await _productService.CreateProductAsync(storeId, request);
        if (!result.IsSuccess)
            return BadRequest(new { Error = result.Error });

        return Ok(result.Data);
    }

    // ══════════════════════════════════════════════════════════════════
    //  PUT — Actualizar producto en Store_ID (para SuperAdmin / Soporte)
    // ══════════════════════════════════════════════════════════════════
    [HttpPut("store/{storeId}/{productId}")]
    [Authorize(AuthenticationSchemes = "Bearer,ApiKey,AzureAd", Roles = "SuperAdmin,CustomerService")]
    public async Task<IActionResult> UpdateProductForStore(int storeId, int productId, [FromBody] CreateProductRequest request)
    {
        var result = await _productService.UpdateProductAsync(storeId, productId, request);
        return result.IsSuccess ? Ok(result.Data) : BadRequest(new { Error = result.Error });
    }

    // ══════════════════════════════════════════════════════════════════
    //  POST — Carga masiva Excel para Store_ID (para SuperAdmin / Soporte)
    // ══════════════════════════════════════════════════════════════════
    [HttpPost("store/{storeId}/bulk-upload")]
    [Authorize(AuthenticationSchemes = "Bearer,ApiKey,AzureAd", Roles = "SuperAdmin,CustomerService")]
    public async Task<IActionResult> BulkUploadProductsForStore(int storeId, IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { Error = "Debe adjuntar un archivo Excel (.xlsx)." });

        if (!file.FileName.EndsWith(".xlsx", System.StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { Error = "Solo se permiten archivos con extensión .xlsx." });

        using var stream = file.OpenReadStream();
        var result = await _productService.BulkUploadProductsAsync(storeId, stream);

        if (!result.IsSuccess)
            return BadRequest(new { Error = result.Error });

        if (result.Data!.FailedCount > 0 && result.Data!.SuccessfulCount > 0)
            return StatusCode(StatusCodes.Status207MultiStatus, result.Data);

        return Ok(result.Data);
    }

    // ══════════════════════════════════════════════════════════════════
    //  GET — Descargar plantilla Excel de carga masiva (para SuperAdmin)
    // ══════════════════════════════════════════════════════════════════
    [HttpGet("store/{storeId}/bulk-template")]
    [Authorize(AuthenticationSchemes = "Bearer,ApiKey,AzureAd", Roles = "SuperAdmin,CustomerService")]
    public async Task<IActionResult> DownloadBulkTemplate(int storeId)
    {
        var result = await _productService.ExportProductsToExcelAsync(storeId);
        if (!result.IsSuccess)
            return BadRequest(new { Error = result.Error });

        return File(result.Data!, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"plantilla-productos-tienda-{storeId}.xlsx");
    }

    // ══════════════════════════════════════════════════════════════════
    //  GET — Descargar plantilla Excel de MI tienda (JWT-based)
    // ══════════════════════════════════════════════════════════════════
    [HttpGet("bulk-template")]
    public async Task<IActionResult> DownloadMyBulkTemplate()
    {
        var storeId = await ResolveStoreIdFromJwtAsync();
        if (storeId is null)
            return Forbid();

        var result = await _productService.ExportProductsToExcelAsync(storeId.Value);
        if (!result.IsSuccess)
            return BadRequest(new { Error = result.Error });

        return File(result.Data!, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"plantilla-productos-tienda-{storeId}.xlsx");
    }

    // ══════════════════════════════════════════════════════════════════
    //  GET — Catálogo Público de Productos (base de datos SQL)
    // ══════════════════════════════════════════════════════════════════

    [HttpGet("public")]
    [AllowAnonymous]
    public async Task<IActionResult> GetPublicProducts([FromQuery] int? categoryId, [FromQuery] string? search)
    {
        using var connection = _dapperContext.CreateConnection();
        var sql = @"
            SELECT 
                p.Product_ID AS ProductId,
                p.Store_ID AS StoreId,
                p.SKU,
                p.Name,
                p.Description,
                p.Category_ID AS CategoryId,
                c.Category_Name AS CategoryName,
                c.Category_Slug AS CategorySlug,
                p.Brand,
                p.Weight_Kg AS WeightKg,
                p.Width_Cm AS WidthCm,
                p.Height_Cm AS HeightCm,
                p.Length_Cm AS LengthCm,
                p.Status_Name AS StatusName,
                p.Created_At AS CreatedAt,
                COALESCE(v.Price_Amount, 250.00) AS Price,
                COALESCE(v.Price_Amount, 250.00) AS RegularPrice,
                COALESCE(img.Image_URL, 'https://images.unsplash.com/photo-1586528116311-ad8dd3c8310d?auto=format&fit=crop&w=800&q=80') AS ImageUrl
            FROM [Catalog].[Products] p
            LEFT JOIN [Logistics].[Categories] c ON p.Category_ID = c.Category_ID
            OUTER APPLY (
                SELECT TOP 1 Price_Amount
                FROM [Logistics].[Product_Variants]
                WHERE Product_ID = p.Product_ID
                ORDER BY Variant_ID ASC
            ) v
            OUTER APPLY (
                SELECT TOP 1 Image_URL
                FROM [Logistics].[Product_Images]
                WHERE Product_ID = p.Product_ID
                ORDER BY Is_Main DESC, Image_ID ASC
            ) img
            WHERE (p.Status_Name IS NULL OR p.Status_Name = 'Activo')
              AND (@CategoryId IS NULL OR p.Category_ID = @CategoryId)
              AND (@Search IS NULL OR p.Name LIKE '%' + @Search + '%' OR p.Description LIKE '%' + @Search + '%')
            ORDER BY p.Created_At DESC";

        var products = await connection.QueryAsync(sql, new { CategoryId = categoryId, Search = search });
        return Ok(products);
    }

    [HttpGet("public/{id}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetPublicProductById(int id)
    {
        using var connection = _dapperContext.CreateConnection();
        var sql = @"
            SELECT 
                p.Product_ID AS ProductId,
                p.Store_ID AS StoreId,
                p.SKU,
                p.Name,
                p.Description,
                p.Category_ID AS CategoryId,
                c.Category_Name AS CategoryName,
                c.Category_Slug AS CategorySlug,
                p.Brand,
                p.Weight_Kg AS WeightKg,
                p.Width_Cm AS WidthCm,
                p.Height_Cm AS HeightCm,
                p.Length_Cm AS LengthCm,
                p.Status_Name AS StatusName,
                p.Created_At AS CreatedAt,
                COALESCE(v.Price_Amount, 250.00) AS Price,
                COALESCE(v.Price_Amount, 250.00) AS RegularPrice,
                COALESCE(img.Image_URL, 'https://images.unsplash.com/photo-1586528116311-ad8dd3c8310d?auto=format&fit=crop&w=800&q=80') AS ImageUrl
            FROM [Catalog].[Products] p
            LEFT JOIN [Logistics].[Categories] c ON p.Category_ID = c.Category_ID
            OUTER APPLY (
                SELECT TOP 1 Price_Amount
                FROM [Logistics].[Product_Variants]
                WHERE Product_ID = p.Product_ID
                ORDER BY Variant_ID ASC
            ) v
            OUTER APPLY (
                SELECT TOP 1 Image_URL
                FROM [Logistics].[Product_Images]
                WHERE Product_ID = p.Product_ID
                ORDER BY Is_Main DESC, Image_ID ASC
            ) img
            WHERE p.Product_ID = @Id";

        var product = await connection.QuerySingleOrDefaultAsync(sql, new { Id = id });
        if (product == null) return NotFound(new { Error = "Producto no encontrado" });

        return Ok(product);
    }

    // ══════════════════════════════════════════════════════════════════
    //  GET — Catálogo de Categorías (público, para el select del frontend)
    // ══════════════════════════════════════════════════════════════════

    [HttpGet("categories")]
    [AllowAnonymous]
    public async Task<IActionResult> GetCategories()
    {
        using var connection = _dapperContext.CreateConnection();
        var categories = await connection.QueryAsync(
            "SELECT Category_ID AS CategoryId, Category_Name AS Name, Category_Slug AS Slug, Default_Commission_Percentage AS CommissionPct FROM [Logistics].[Categories] ORDER BY Category_Name");
        return Ok(categories);
    }
}

