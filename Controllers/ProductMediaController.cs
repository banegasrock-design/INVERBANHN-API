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
/// Gestión de Imágenes y Variantes de Productos del Marketplace.
/// Todos los endpoints están aislados por tienda (Store_ID del JWT).
/// </summary>
[ApiController]
[Route("api/products/{productId}")]
[Authorize]
public class ProductMediaController : ControllerBase
{
    private readonly IProductMediaService _mediaService;
    private readonly DapperContext _dapperContext;

    public ProductMediaController(IProductMediaService mediaService, DapperContext dapperContext)
    {
        _mediaService = mediaService;
        _dapperContext = dapperContext;
    }

    // Reutiliza la misma lógica de resolución de Store_ID del ProductController
    private async Task<int?> ResolveStoreIdAsync()
    {
        using var conn = _dapperContext.CreateConnection();

        // Si el usuario es SuperAdmin o CustomerService, obtenemos la tienda del producto directamente
        if (User.IsInRole("SuperAdmin") || User.IsInRole("CustomerService") || User.FindFirst("role")?.Value == "SuperAdmin")
        {
            if (RouteData.Values.TryGetValue("productId", out var prodIdObj) && int.TryParse(prodIdObj?.ToString(), out var productId))
            {
                return await conn.QuerySingleOrDefaultAsync<int?>(
                    "SELECT Store_ID FROM [Catalog].[Products] WHERE Product_ID = @ProductId",
                    new { ProductId = productId });
            }
        }

        var claim = User.FindFirst("sub")
                 ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);

        if (claim != null && int.TryParse(claim.Value, out var userId))
        {
            var storeId = await conn.QuerySingleOrDefaultAsync<int?>(
                "SELECT Store_ID FROM [Core].[Stores] WHERE Owner_User_ID = @UserId AND (Is_Active IS NULL OR Is_Active = 1)",
                new { UserId = userId });
            if (storeId != null) return storeId;
        }

        if (User.Identity?.AuthenticationType == "ApiKey" || claim?.Value == "SuperAdmin_ApiKey")
        {
            var storeId = await conn.QuerySingleOrDefaultAsync<int?>(
                "SELECT TOP 1 Store_ID FROM [Core].[Stores] WHERE Store_ID = 2 OR (Is_Active IS NULL OR Is_Active = 1) ORDER BY CASE WHEN Store_ID = 2 THEN 0 ELSE 1 END");
            return storeId;
        }

        return null;
    }

    // ════════════════════════════════════════════════════════════════════════
    //  IMÁGENES  (/api/products/{productId}/images)
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Retorna todas las imágenes de un producto. La imagen principal aparece primero.
    /// </summary>
    [HttpGet("images")]
    [ProducesResponseType(typeof(IEnumerable<ProductImageDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetImages(int productId)
    {
        var storeId = await ResolveStoreIdAsync();
        if (storeId is null) return Forbid();

        var result = await _mediaService.GetProductImagesAsync(storeId.Value, productId);
        return result.IsSuccess ? Ok(result.Data) : BadRequest(new { Error = result.Error });
    }

    /// <summary>
    /// Agrega una nueva imagen al producto.
    /// Si IsMain es true, esta imagen pasa a ser la portada y la anterior pierde ese estado automáticamente.
    /// La URL debe provenir de Azure Blob Storage (ya procesada antes de llamar a este endpoint).
    /// </summary>
    [HttpPost("images")]
    [ProducesResponseType(typeof(ProductImageDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AddImage(int productId, [FromBody] AddProductImageRequest request)
    {
        var storeId = await ResolveStoreIdAsync();
        if (storeId is null) return Forbid();

        var result = await _mediaService.AddProductImageAsync(storeId.Value, productId, request);
        if (!result.IsSuccess) return BadRequest(new { Error = result.Error });

        return CreatedAtAction(nameof(GetImages), new { productId }, result.Data);
    }

    /// <summary>
    /// Cambia la imagen principal (portada) del producto.
    /// Internamente usa una transacción Dapper para garantizar que solo una imagen sea IsMain=true.
    /// </summary>
    [HttpPatch("images/{imageId}/set-main")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SetMainImage(int productId, int imageId)
    {
        var storeId = await ResolveStoreIdAsync();
        if (storeId is null) return Forbid();

        var result = await _mediaService.SetMainImageAsync(storeId.Value, productId, imageId);
        return result.IsSuccess ? NoContent() : BadRequest(new { Error = result.Error });
    }

    /// <summary>
    /// Reordena las imágenes de la galería. Enviar los Image_IDs en el nuevo orden deseado.
    /// </summary>
    /// <param name="productId">ID del producto.</param>
    /// <param name="orderedImageIds">Lista de Image_IDs en el nuevo orden (index 0 = primera posición).</param>
    [HttpPatch("images/reorder")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ReorderImages(int productId, [FromBody] List<int> orderedImageIds)
    {
        if (orderedImageIds == null || orderedImageIds.Count == 0)
            return BadRequest(new { Error = "Debe enviar al menos un Image_ID." });

        var storeId = await ResolveStoreIdAsync();
        if (storeId is null) return Forbid();

        var result = await _mediaService.ReorderImagesAsync(storeId.Value, productId, orderedImageIds);
        return result.IsSuccess ? NoContent() : BadRequest(new { Error = result.Error });
    }

    /// <summary>
    /// Elimina una imagen del producto.
    /// Si es la única imagen del producto, la operación es rechazada.
    /// </summary>
    [HttpDelete("images/{imageId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DeleteImage(int productId, int imageId)
    {
        var storeId = await ResolveStoreIdAsync();
        if (storeId is null) return Forbid();

        var result = await _mediaService.DeleteProductImageAsync(storeId.Value, productId, imageId);
        return result.IsSuccess ? NoContent() : BadRequest(new { Error = result.Error });
    }

    // ════════════════════════════════════════════════════════════════════════
    //  VARIANTES  (/api/products/{productId}/variants)
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Lista todas las variantes de un producto (tallas, colores, capacidades, etc.).
    /// </summary>
    [HttpGet("variants")]
    [ProducesResponseType(typeof(IEnumerable<ProductVariantDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetVariants(int productId)
    {
        var storeId = await ResolveStoreIdAsync();
        if (storeId is null) return Forbid();

        var result = await _mediaService.GetVariantsAsync(storeId.Value, productId);
        return result.IsSuccess ? Ok(result.Data) : BadRequest(new { Error = result.Error });
    }

    /// <summary>
    /// Agrega una nueva variante al producto.
    /// Si la variante tiene peso propio (WeightKg), ese valor se usará en el motor de cotización
    /// de paqueteras en lugar del peso base del producto.
    /// </summary>
    [HttpPost("variants")]
    [ProducesResponseType(typeof(ProductVariantDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AddVariant(int productId, [FromBody] CreateVariantRequest request)
    {
        var storeId = await ResolveStoreIdAsync();
        if (storeId is null) return Forbid();

        var result = await _mediaService.AddVariantAsync(storeId.Value, productId, request);
        if (!result.IsSuccess) return BadRequest(new { Error = result.Error });

        return CreatedAtAction(nameof(GetVariants), new { productId }, result.Data);
    }

    /// <summary>
    /// Actualiza una variante existente del producto.
    /// </summary>
    [HttpPut("variants/{variantId}")]
    [ProducesResponseType(typeof(ProductVariantDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateVariant(int productId, int variantId, [FromBody] CreateVariantRequest request)
    {
        var storeId = await ResolveStoreIdAsync();
        if (storeId is null) return Forbid();

        var result = await _mediaService.UpdateVariantAsync(storeId.Value, productId, variantId, request);
        return result.IsSuccess ? Ok(result.Data) : BadRequest(new { Error = result.Error });
    }

    /// <summary>
    /// Elimina una variante del producto.
    /// Si la variante tiene imágenes asociadas, estas también son eliminadas.
    /// </summary>
    [HttpDelete("variants/{variantId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DeleteVariant(int productId, int variantId)
    {
        var storeId = await ResolveStoreIdAsync();
        if (storeId is null) return Forbid();

        var result = await _mediaService.DeleteVariantAsync(storeId.Value, productId, variantId);
        return result.IsSuccess ? NoContent() : BadRequest(new { Error = result.Error });
    }
}
