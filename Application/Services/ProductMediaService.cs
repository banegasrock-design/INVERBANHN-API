using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Application.Common;
using Application.DTOs.Product;
using Application.Interfaces;
using Dapper;
using Infrastructure.Data.Contexts;
using Microsoft.Data.SqlClient;

namespace Application.Services;

public class ProductMediaService : IProductMediaService
{
    private readonly DapperContext _dapperContext;

    public ProductMediaService(DapperContext dapperContext)
    {
        _dapperContext = dapperContext;
    }

    // ════════════════════════════════════════════════════════════════════════
    //  HELPER: Verificar que el producto pertenece a la tienda autenticada.
    //  Nunca se salta esta verificación — garantiza aislamiento de datos.
    // ════════════════════════════════════════════════════════════════════════
    private static async Task<bool> ProductBelongsToStoreAsync(
        System.Data.IDbConnection conn, int productId, int storeId)
    {
        var exists = await conn.QuerySingleOrDefaultAsync<int?>(
            "SELECT Product_ID FROM [Catalog].[Products] WHERE Product_ID = @ProductId AND Store_ID = @StoreId",
            new { ProductId = productId, StoreId = storeId });
        return exists.HasValue;
    }

    // ════════════════════════════════════════════════════════════════════════
    //  IMÁGENES
    // ════════════════════════════════════════════════════════════════════════

    public async Task<Result<IEnumerable<ProductImageDto>>> GetProductImagesAsync(int storeId, int productId)
    {
        using var conn = _dapperContext.CreateConnection();
        try
        {
            if (!await ProductBelongsToStoreAsync(conn, productId, storeId))
                return Result<IEnumerable<ProductImageDto>>.Failure("El producto no existe o no pertenece a tu tienda.");

            var sql = @"
                SELECT 
                    Image_ID        AS ImageId,
                    Product_ID      AS ProductId,
                    Variant_ID      AS VariantId,
                    Image_URL       AS ImageUrl,
                    Thumbnail_URL   AS ThumbnailUrl,
                    Medium_URL      AS MediumUrl,
                    Alt_Text        AS AltText,
                    Is_Main         AS IsMain,
                    Display_Order   AS DisplayOrder,
                    Mime_Type       AS MimeType,
                    Image_Size_Bytes AS ImageSizeBytes,
                    Created_At      AS CreatedAt
                FROM [Logistics].[Product_Images]
                WHERE Product_ID = @ProductId
                ORDER BY Is_Main DESC, Display_Order ASC";

            var images = await conn.QueryAsync<ProductImageDto>(sql, new { ProductId = productId });
            return Result<IEnumerable<ProductImageDto>>.Success(images);
        }
        catch (Exception ex)
        {
            return Result<IEnumerable<ProductImageDto>>.Failure($"Error al obtener imágenes: {ex.Message}");
        }
    }

    public async Task<Result<ProductImageDto>> AddProductImageAsync(int storeId, int productId, AddProductImageRequest request)
    {
        using var conn = _dapperContext.CreateConnection();
        conn.Open();
        using var tx = conn.BeginTransaction();
        try
        {
            if (!await ProductBelongsToStoreAsync(conn, productId, storeId))
                return Result<ProductImageDto>.Failure("El producto no existe o no pertenece a tu tienda.");

            // Si es imagen principal, quitar la bandera de la anterior
            if (request.IsMain)
            {
                await conn.ExecuteAsync(
                    "UPDATE [Logistics].[Product_Images] SET Is_Main = 0 WHERE Product_ID = @ProductId",
                    new { ProductId = productId }, tx);
            }

            var sql = @"
                INSERT INTO [Logistics].[Product_Images]
                    (Product_ID, Variant_ID, Image_URL, Thumbnail_URL, Medium_URL,
                     Alt_Text, Is_Main, Display_Order, Mime_Type, Image_Size_Bytes, Created_At)
                VALUES
                    (@Product_ID, @Variant_ID, @Image_URL, @Thumbnail_URL, @Medium_URL,
                     @Alt_Text, @Is_Main, @Display_Order, @Mime_Type, @Image_Size_Bytes, @Created_At);
                SELECT CAST(SCOPE_IDENTITY() AS INT);";

            var newId = await conn.QuerySingleAsync<int>(sql, new
            {
                Product_ID       = productId,
                Variant_ID       = request.VariantId,
                Image_URL        = request.ImageUrl,
                Thumbnail_URL    = request.ThumbnailUrl,
                Medium_URL       = request.MediumUrl,
                Alt_Text         = request.AltText,
                Is_Main          = request.IsMain,
                Display_Order    = request.DisplayOrder,
                Mime_Type        = request.MimeType,
                Image_Size_Bytes = request.ImageSizeBytes,
                Created_At       = DateTime.UtcNow
            }, tx);

            tx.Commit();

            var created = await conn.QuerySingleAsync<ProductImageDto>(
                "SELECT Image_ID AS ImageId, Product_ID AS ProductId, Variant_ID AS VariantId, Image_URL AS ImageUrl, Thumbnail_URL AS ThumbnailUrl, Medium_URL AS MediumUrl, Alt_Text AS AltText, Is_Main AS IsMain, Display_Order AS DisplayOrder, Mime_Type AS MimeType, Image_Size_Bytes AS ImageSizeBytes, Created_At AS CreatedAt FROM [Logistics].[Product_Images] WHERE Image_ID = @Id",
                new { Id = newId });

            return Result<ProductImageDto>.Success(created);
        }
        catch (Exception ex)
        {
            tx.Rollback();
            return Result<ProductImageDto>.Failure($"Error al agregar imagen: {ex.Message}");
        }
    }

    public async Task<Result<bool>> SetMainImageAsync(int storeId, int productId, int imageId)
    {
        using var conn = _dapperContext.CreateConnection();
        conn.Open();
        using var tx = conn.BeginTransaction();
        try
        {
            if (!await ProductBelongsToStoreAsync(conn, productId, storeId))
                return Result<bool>.Failure("El producto no existe o no pertenece a tu tienda.");

            // Desmarcar todas, luego marcar la seleccionada
            await conn.ExecuteAsync(
                "UPDATE [Logistics].[Product_Images] SET Is_Main = 0 WHERE Product_ID = @ProductId",
                new { ProductId = productId }, tx);

            var rows = await conn.ExecuteAsync(
                "UPDATE [Logistics].[Product_Images] SET Is_Main = 1 WHERE Image_ID = @ImageId AND Product_ID = @ProductId",
                new { ImageId = imageId, ProductId = productId }, tx);

            if (rows == 0)
            {
                tx.Rollback();
                return Result<bool>.Failure("La imagen no existe en este producto.");
            }

            tx.Commit();
            return Result<bool>.Success(true);
        }
        catch (Exception ex)
        {
            tx.Rollback();
            return Result<bool>.Failure($"Error al cambiar imagen principal: {ex.Message}");
        }
    }

    public async Task<Result<bool>> DeleteProductImageAsync(int storeId, int productId, int imageId)
    {
        using var conn = _dapperContext.CreateConnection();
        try
        {
            if (!await ProductBelongsToStoreAsync(conn, productId, storeId))
                return Result<bool>.Failure("El producto no existe o no pertenece a tu tienda.");

            // No permitir borrar la imagen principal si es la única
            var isMain = await conn.QuerySingleOrDefaultAsync<bool?>(
                "SELECT Is_Main FROM [Logistics].[Product_Images] WHERE Image_ID = @ImageId AND Product_ID = @ProductId",
                new { ImageId = imageId, ProductId = productId });

            if (isMain == null)
                return Result<bool>.Failure("La imagen no existe en este producto.");

            if (isMain == true)
            {
                var count = await conn.QuerySingleAsync<int>(
                    "SELECT COUNT(*) FROM [Logistics].[Product_Images] WHERE Product_ID = @ProductId",
                    new { ProductId = productId });
                if (count == 1)
                    return Result<bool>.Failure("No puedes eliminar la única imagen del producto. Agrega otra imagen primero.");
            }

            await conn.ExecuteAsync(
                "DELETE FROM [Logistics].[Product_Images] WHERE Image_ID = @ImageId AND Product_ID = @ProductId",
                new { ImageId = imageId, ProductId = productId });

            return Result<bool>.Success(true);
        }
        catch (Exception ex)
        {
            return Result<bool>.Failure($"Error al eliminar imagen: {ex.Message}");
        }
    }

    public async Task<Result<bool>> ReorderImagesAsync(int storeId, int productId, List<int> orderedImageIds)
    {
        using var conn = _dapperContext.CreateConnection();
        conn.Open();
        using var tx = conn.BeginTransaction();
        try
        {
            if (!await ProductBelongsToStoreAsync(conn, productId, storeId))
                return Result<bool>.Failure("El producto no existe o no pertenece a tu tienda.");

            for (int i = 0; i < orderedImageIds.Count; i++)
            {
                await conn.ExecuteAsync(
                    "UPDATE [Logistics].[Product_Images] SET Display_Order = @Order WHERE Image_ID = @ImageId AND Product_ID = @ProductId",
                    new { Order = i, ImageId = orderedImageIds[i], ProductId = productId }, tx);
            }

            tx.Commit();
            return Result<bool>.Success(true);
        }
        catch (Exception ex)
        {
            tx.Rollback();
            return Result<bool>.Failure($"Error al reordenar imágenes: {ex.Message}");
        }
    }

    // ════════════════════════════════════════════════════════════════════════
    //  VARIANTES
    // ════════════════════════════════════════════════════════════════════════

    public async Task<Result<IEnumerable<ProductVariantDto>>> GetVariantsAsync(int storeId, int productId)
    {
        using var conn = _dapperContext.CreateConnection();
        try
        {
            if (!await ProductBelongsToStoreAsync(conn, productId, storeId))
                return Result<IEnumerable<ProductVariantDto>>.Failure("El producto no existe o no pertenece a tu tienda.");

            var sql = @"
                SELECT 
                    Variant_ID          AS VariantId,
                    Product_ID          AS ProductId,
                    SKU,
                    Variant_Name        AS VariantName,
                    Variant_Description AS VariantDescription,
                    Price_Amount        AS PriceAmount,
                    Weight_Kg           AS WeightKg,
                    Is_Digital_Download AS IsDigitalDownload,
                    Offer_Type          AS OfferType
                FROM [Logistics].[Product_Variants]
                WHERE Product_ID = @ProductId
                ORDER BY Variant_ID";

            var variants = await conn.QueryAsync<ProductVariantDto>(sql, new { ProductId = productId });
            return Result<IEnumerable<ProductVariantDto>>.Success(variants);
        }
        catch (Exception ex)
        {
            return Result<IEnumerable<ProductVariantDto>>.Failure($"Error al obtener variantes: {ex.Message}");
        }
    }

    public async Task<Result<ProductVariantDto>> AddVariantAsync(int storeId, int productId, CreateVariantRequest request)
    {
        using var conn = _dapperContext.CreateConnection();
        try
        {
            if (!await ProductBelongsToStoreAsync(conn, productId, storeId))
                return Result<ProductVariantDto>.Failure("El producto no existe o no pertenece a tu tienda.");

            var sql = @"
                INSERT INTO [Logistics].[Product_Variants]
                    (Product_ID, SKU, Variant_Name, Variant_Description, Price_Amount, Weight_Kg, Is_Digital_Download, Offer_Type)
                VALUES
                    (@Product_ID, @SKU, @Variant_Name, @Variant_Description, @Price_Amount, @Weight_Kg, @Is_Digital_Download, @Offer_Type);
                SELECT CAST(SCOPE_IDENTITY() AS INT);";

            var newId = await conn.QuerySingleAsync<int>(sql, new
            {
                Product_ID          = productId,
                SKU                 = request.SKU,
                Variant_Name        = request.VariantName,
                Variant_Description = request.VariantDescription,
                Price_Amount        = request.PriceAmount,
                Weight_Kg           = request.WeightKg,
                Is_Digital_Download = request.IsDigitalDownload,
                Offer_Type          = request.OfferType
            });

            var created = await conn.QuerySingleAsync<ProductVariantDto>(
                "SELECT Variant_ID AS VariantId, Product_ID AS ProductId, SKU, Variant_Name AS VariantName, Variant_Description AS VariantDescription, Price_Amount AS PriceAmount, Weight_Kg AS WeightKg, Is_Digital_Download AS IsDigitalDownload, Offer_Type AS OfferType FROM [Logistics].[Product_Variants] WHERE Variant_ID = @Id",
                new { Id = newId });

            return Result<ProductVariantDto>.Success(created);
        }
        catch (SqlException ex) when (ex.Number == 2627)
        {
            return Result<ProductVariantDto>.Failure($"El SKU '{request.SKU}' ya existe en este producto.");
        }
        catch (Exception ex)
        {
            return Result<ProductVariantDto>.Failure($"Error al crear variante: {ex.Message}");
        }
    }

    public async Task<Result<ProductVariantDto>> UpdateVariantAsync(int storeId, int productId, int variantId, CreateVariantRequest request)
    {
        using var conn = _dapperContext.CreateConnection();
        try
        {
            if (!await ProductBelongsToStoreAsync(conn, productId, storeId))
                return Result<ProductVariantDto>.Failure("El producto no existe o no pertenece a tu tienda.");

            var rows = await conn.ExecuteAsync(@"
                UPDATE [Logistics].[Product_Variants] SET
                    SKU                 = @SKU,
                    Variant_Name        = @Variant_Name,
                    Variant_Description = @Variant_Description,
                    Price_Amount        = @Price_Amount,
                    Weight_Kg           = @Weight_Kg,
                    Is_Digital_Download = @Is_Digital_Download,
                    Offer_Type          = @Offer_Type
                WHERE Variant_ID = @VariantId AND Product_ID = @ProductId",
                new
                {
                    VariantId           = variantId,
                    ProductId           = productId,
                    SKU                 = request.SKU,
                    Variant_Name        = request.VariantName,
                    Variant_Description = request.VariantDescription,
                    Price_Amount        = request.PriceAmount,
                    Weight_Kg           = request.WeightKg,
                    Is_Digital_Download = request.IsDigitalDownload,
                    Offer_Type          = request.OfferType
                });

            if (rows == 0)
                return Result<ProductVariantDto>.Failure("La variante no existe en este producto.");

            var updated = await conn.QuerySingleAsync<ProductVariantDto>(
                "SELECT Variant_ID AS VariantId, Product_ID AS ProductId, SKU, Variant_Name AS VariantName, Variant_Description AS VariantDescription, Price_Amount AS PriceAmount, Weight_Kg AS WeightKg, Is_Digital_Download AS IsDigitalDownload, Offer_Type AS OfferType FROM [Logistics].[Product_Variants] WHERE Variant_ID = @Id",
                new { Id = variantId });

            return Result<ProductVariantDto>.Success(updated);
        }
        catch (SqlException ex) when (ex.Number == 2627)
        {
            return Result<ProductVariantDto>.Failure($"El SKU '{request.SKU}' ya está en uso por otra variante.");
        }
        catch (Exception ex)
        {
            return Result<ProductVariantDto>.Failure($"Error al actualizar variante: {ex.Message}");
        }
    }

    public async Task<Result<bool>> DeleteVariantAsync(int storeId, int productId, int variantId)
    {
        using var conn = _dapperContext.CreateConnection();
        try
        {
            if (!await ProductBelongsToStoreAsync(conn, productId, storeId))
                return Result<bool>.Failure("El producto no existe o no pertenece a tu tienda.");

            var rows = await conn.ExecuteAsync(
                "DELETE FROM [Logistics].[Product_Variants] WHERE Variant_ID = @VariantId AND Product_ID = @ProductId",
                new { VariantId = variantId, ProductId = productId });

            if (rows == 0)
                return Result<bool>.Failure("La variante no existe en este producto.");

            return Result<bool>.Success(true);
        }
        catch (Exception ex)
        {
            return Result<bool>.Failure($"Error al eliminar variante: {ex.Message}");
        }
    }
}
