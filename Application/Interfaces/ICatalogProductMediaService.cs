using System.Threading.Tasks;
using Application.Common;
using System.Collections.Generic;

namespace Application.Interfaces;

public class CatalogProductMediaDto
{
    public int MediaId { get; set; }
    public int ProductId { get; set; }
    public string OriginalUrl { get; set; } = string.Empty;
    public string MediumUrl { get; set; } = string.Empty;
    public string ThumbnailUrl { get; set; } = string.Empty;
    public bool IsMain { get; set; }
    public string MimeType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
}

public interface ICatalogProductMediaService
{
    Task<Result<IEnumerable<CatalogProductMediaDto>>> GetProductImagesAsync(int storeId, int productId);
    Task<Result<CatalogProductMediaDto>> SaveProductMediaAsync(int storeId, int productId, ImageUploadResult uploadResult, bool isMain);
    Task<Result<bool>> SetMainImageAsync(int storeId, int productId, int mediaId);
    Task<Result<bool>> DeleteProductMediaAsync(int storeId, int productId, int mediaId);
}
