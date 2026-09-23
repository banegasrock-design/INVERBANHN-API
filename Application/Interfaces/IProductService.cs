using Application.Common;
using Application.DTOs.Product;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Application.Interfaces;

public interface IProductService
{
    /// <summary>
    /// Obtiene todos los productos de la tienda autenticada (Store_ID inyectado desde el JWT).
    /// </summary>
    Task<Result<IEnumerable<ProductDto>>> GetMyProductsAsync(int storeId);

    /// <summary>
    /// Crea un nuevo producto. El Store_ID se asigna automáticamente desde el JWT del vendedor autenticado.
    /// </summary>
    Task<Result<ProductDto>> CreateProductAsync(int storeId, CreateProductRequest request);

    /// <summary>
    /// Actualiza un producto existente. Solo el dueño del Store puede modificarlo.
    /// </summary>
    Task<Result<ProductDto>> UpdateProductAsync(int storeId, int productId, CreateProductRequest request);

    /// <summary>
    /// Desactiva un producto (soft delete). No elimina el registro de la BD.
    /// </summary>
    Task<Result<bool>> DeactivateProductAsync(int storeId, int productId);

    /// <summary>
    /// Sube de manera masiva productos a partir de un archivo Excel (.xlsx).
    /// </summary>
    Task<Result<BulkProductUploadResult>> BulkUploadProductsAsync(int storeId, Stream excelStream);

    /// <summary>
    /// Exporta los productos de la tienda autenticada a un archivo Excel (.xlsx).
    /// </summary>
    Task<Result<byte[]>> ExportProductsToExcelAsync(int storeId);
}
