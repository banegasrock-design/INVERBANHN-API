using System.Threading.Tasks;
using Application.Interfaces;
using Dapper;
using Infrastructure.Data.Contexts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace INVERBANHN.Controllers;

/// <summary>
/// Gestión de Imágenes de Productos en el catálogo (Subida con redimensionamiento).
/// </summary>
[ApiController]
[Route("api/catalog/products/{productId}")]
[Authorize]
public class CatalogProductMediaController : ControllerBase
{
    private readonly ICatalogProductMediaService _catalogMediaService;
    private readonly IImageUploadService _imageUploadService;
    private readonly DapperContext _dapperContext;

    public CatalogProductMediaController(ICatalogProductMediaService catalogMediaService, IImageUploadService imageUploadService, DapperContext dapperContext)
    {
        _catalogMediaService = catalogMediaService;
        _imageUploadService = imageUploadService;
        _dapperContext = dapperContext;
    }

    private async Task<int?> ResolveStoreIdAsync()
    {
        var claim = User.FindFirst("sub")
                 ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);

        if (claim != null && int.TryParse(claim.Value, out var userId))
        {
            using var conn = _dapperContext.CreateConnection();
            var storeId = await conn.QuerySingleOrDefaultAsync<int?>(
                "SELECT Store_ID FROM [Core].[Stores] WHERE Owner_User_ID = @UserId AND (Is_Active IS NULL OR Is_Active = 1)",
                new { UserId = userId });
            if (storeId != null) return storeId;
        }

        if (User.Identity?.AuthenticationType == "ApiKey" || claim?.Value == "SuperAdmin_ApiKey")
        {
            using var conn = _dapperContext.CreateConnection();
            var storeId = await conn.QuerySingleOrDefaultAsync<int?>(
                "SELECT TOP 1 Store_ID FROM [Core].[Stores] WHERE Store_ID = 2 OR (Is_Active IS NULL OR Is_Active = 1) ORDER BY CASE WHEN Store_ID = 2 THEN 0 ELSE 1 END");
            return storeId;
        }

        return null;
    }

    /// <summary>
    /// Retorna todas las imágenes de un producto del catálogo.
    /// </summary>
    [HttpGet("media")]
    [ProducesResponseType(typeof(System.Collections.Generic.IEnumerable<CatalogProductMediaDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMedia(int productId)
    {
        var storeId = await ResolveStoreIdAsync();
        if (storeId is null) return Forbid();

        var result = await _catalogMediaService.GetProductImagesAsync(storeId.Value, productId);
        return result.IsSuccess ? Ok(result.Data) : BadRequest(new { Error = result.Error });
    }

    /// <summary>
    /// Sube una nueva imagen al producto. 
    /// Recibe un archivo, lo redimensiona y lo sube a Azure Blob Storage.
    /// </summary>
    [HttpPost("media/upload")]
    [ProducesResponseType(typeof(CatalogProductMediaDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UploadMedia(int productId, [FromForm] UploadProductMediaRequest request)
    {
        if (request.File == null)
            return BadRequest(new { Error = "Debe proporcionar un archivo." });

        var storeId = await ResolveStoreIdAsync();
        if (storeId is null) return Forbid();

        try
        {
            var uploadResult = await _imageUploadService.ProcessAndUploadImageAsync(request.File, $"product-{productId}");
            var saveResult = await _catalogMediaService.SaveProductMediaAsync(storeId.Value, productId, uploadResult, request.IsMain);

            if (!saveResult.IsSuccess)
                return BadRequest(new { Error = saveResult.Error });

            return CreatedAtAction(nameof(GetMedia), new { productId }, saveResult.Data);
        }
        catch (System.Exception ex)
        {
            return BadRequest(new { Error = ex.Message });
        }
    }

    /// <summary>
    /// Agrega una imagen por URL al producto del catálogo.
    /// </summary>
    [HttpPost("media")]
    public async Task<IActionResult> AddMediaUrl(int productId, [FromBody] AddCatalogProductMediaUrlRequest request)
    {
        if (request == null || string.IsNullOrEmpty(request.ImageUrl))
            return BadRequest(new { Error = "Debe proporcionar una URL de imagen." });

        var storeId = await ResolveStoreIdAsync();
        if (storeId is null) return Forbid();

        try
        {
            var uploadResult = new ImageUploadResult(request.ImageUrl, request.ImageUrl, request.ImageUrl, "image/jpeg", 0);
            var saveResult = await _catalogMediaService.SaveProductMediaAsync(storeId.Value, productId, uploadResult, request.IsMain);

            if (!saveResult.IsSuccess)
                return BadRequest(new { Error = saveResult.Error });

            return CreatedAtAction(nameof(GetMedia), new { productId }, saveResult.Data);
        }
        catch (System.Exception ex)
        {
            return BadRequest(new { Error = ex.Message });
        }
    }

    /// <summary>
    /// Cambia la imagen principal (portada) del producto.
    /// </summary>
    [HttpPatch("media/{mediaId}/set-main")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SetMainImage(int productId, int mediaId)
    {
        var storeId = await ResolveStoreIdAsync();
        if (storeId is null) return Forbid();

        var result = await _catalogMediaService.SetMainImageAsync(storeId.Value, productId, mediaId);
        return result.IsSuccess ? NoContent() : BadRequest(new { Error = result.Error });
    }

    /// <summary>
    /// Elimina una imagen del producto del catálogo.
    /// </summary>
    [HttpDelete("media/{mediaId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DeleteMedia(int productId, int mediaId)
    {
        var storeId = await ResolveStoreIdAsync();
        if (storeId is null) return Forbid();

        var result = await _catalogMediaService.DeleteProductMediaAsync(storeId.Value, productId, mediaId);
        return result.IsSuccess ? NoContent() : BadRequest(new { Error = result.Error });
    }
}

public class UploadProductMediaRequest
{
    public IFormFile File { get; set; } = null!;
    public bool IsMain { get; set; } = false;
}

public class AddCatalogProductMediaUrlRequest
{
    public string ImageUrl { get; set; } = string.Empty;
    public bool IsMain { get; set; } = false;
}
