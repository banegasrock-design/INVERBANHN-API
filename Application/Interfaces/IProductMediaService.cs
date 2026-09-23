using Application.Common;
using Application.DTOs.Product;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Application.Interfaces;

public interface IProductMediaService
{
    // ── Imágenes ──────────────────────────────────────────────────────────
    Task<Result<IEnumerable<ProductImageDto>>> GetProductImagesAsync(int storeId, int productId);
    Task<Result<ProductImageDto>> AddProductImageAsync(int storeId, int productId, AddProductImageRequest request);
    Task<Result<bool>> SetMainImageAsync(int storeId, int productId, int imageId);
    Task<Result<bool>> DeleteProductImageAsync(int storeId, int productId, int imageId);
    Task<Result<bool>> ReorderImagesAsync(int storeId, int productId, List<int> orderedImageIds);

    // ── Variantes ────────────────────────────────────────────────────────
    Task<Result<IEnumerable<ProductVariantDto>>> GetVariantsAsync(int storeId, int productId);
    Task<Result<ProductVariantDto>> AddVariantAsync(int storeId, int productId, CreateVariantRequest request);
    Task<Result<ProductVariantDto>> UpdateVariantAsync(int storeId, int productId, int variantId, CreateVariantRequest request);
    Task<Result<bool>> DeleteVariantAsync(int storeId, int productId, int variantId);
}
