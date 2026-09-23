using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Application.Common;
using Application.Interfaces;
using Dapper;
using Infrastructure.Data.Contexts;

namespace Application.Services;

public class CatalogProductMediaService : ICatalogProductMediaService
{
    private readonly DapperContext _dapperContext;

    public CatalogProductMediaService(DapperContext dapperContext)
    {
        _dapperContext = dapperContext;
    }

    private async Task<bool> ProductBelongsToStoreAsync(System.Data.IDbConnection conn, int productId, int storeId, System.Data.IDbTransaction? transaction = null)
    {
        var exists = await conn.QuerySingleOrDefaultAsync<int?>(
            "SELECT Product_ID FROM [Catalog].[Products] WHERE Product_ID = @ProductId AND Store_ID = @StoreId",
            new { ProductId = productId, StoreId = storeId },
            transaction);
        return exists.HasValue;
    }

    public async Task<Result<IEnumerable<CatalogProductMediaDto>>> GetProductImagesAsync(int storeId, int productId)
    {
        using var conn = _dapperContext.CreateConnection();
        try
        {
            if (!await ProductBelongsToStoreAsync(conn, productId, storeId))
                return Result<IEnumerable<CatalogProductMediaDto>>.Failure("El producto no existe o no pertenece a tu tienda.");

            var sql = @"
                SELECT 
                    Media_ID        AS MediaId,
                    Product_ID      AS ProductId,
                    Image_URL       AS OriginalUrl,
                    Image_URL       AS MediumUrl,
                    Image_URL       AS ThumbnailUrl,
                    Is_Main         AS IsMain,
                    'image/webp'    AS MimeType,
                    0               AS SizeBytes
                FROM [Catalog].[Product_Media]
                WHERE Product_ID = @ProductId
                ORDER BY Is_Main DESC, Media_ID ASC";

            var images = await conn.QueryAsync<CatalogProductMediaDto>(sql, new { ProductId = productId });
            return Result<IEnumerable<CatalogProductMediaDto>>.Success(images);
        }
        catch (Exception ex)
        {
            return Result<IEnumerable<CatalogProductMediaDto>>.Failure($"Error al obtener imágenes: {ex.Message}");
        }
    }

    public async Task<Result<CatalogProductMediaDto>> SaveProductMediaAsync(int storeId, int productId, ImageUploadResult uploadResult, bool isMain)
    {
        using var conn = _dapperContext.CreateConnection();
        conn.Open();
        using var tx = conn.BeginTransaction();
        try
        {
            if (!await ProductBelongsToStoreAsync(conn, productId, storeId, tx))
                return Result<CatalogProductMediaDto>.Failure("El producto no existe o no pertenece a tu tienda.");

            if (isMain)
            {
                await conn.ExecuteAsync(
                    "UPDATE [Catalog].[Product_Media] SET Is_Main = 0 WHERE Product_ID = @ProductId",
                    new { ProductId = productId }, tx);
            }

            var sql = @"
                INSERT INTO [Catalog].[Product_Media]
                    (Product_ID, Image_URL, Is_Main)
                VALUES
                    (@Product_ID, @Image_URL, @Is_Main);
                SELECT CAST(SCOPE_IDENTITY() AS INT);";

            var newId = await conn.QuerySingleAsync<int>(sql, new
            {
                Product_ID = productId,
                Image_URL = uploadResult.OriginalUrl,
                Is_Main = isMain
            }, tx);

            tx.Commit();

            var created = await conn.QuerySingleAsync<CatalogProductMediaDto>(
                @"SELECT 
                    Media_ID        AS MediaId, 
                    Product_ID      AS ProductId, 
                    Image_URL       AS OriginalUrl, 
                    Image_URL       AS MediumUrl, 
                    Image_URL       AS ThumbnailUrl, 
                    Is_Main         AS IsMain, 
                    'image/webp'    AS MimeType, 
                    0               AS SizeBytes 
                FROM [Catalog].[Product_Media] 
                WHERE Media_ID = @Id",
                new { Id = newId });

            return Result<CatalogProductMediaDto>.Success(created);
        }
        catch (Exception ex)
        {
            tx.Rollback();
            return Result<CatalogProductMediaDto>.Failure($"Error al guardar imagen: {ex.Message}");
        }
    }

    public async Task<Result<bool>> SetMainImageAsync(int storeId, int productId, int mediaId)
    {
        using var conn = _dapperContext.CreateConnection();
        conn.Open();
        using var tx = conn.BeginTransaction();
        try
        {
            if (!await ProductBelongsToStoreAsync(conn, productId, storeId, tx))
                return Result<bool>.Failure("El producto no existe o no pertenece a tu tienda.");

            await conn.ExecuteAsync(
                "UPDATE [Catalog].[Product_Media] SET Is_Main = 0 WHERE Product_ID = @ProductId",
                new { ProductId = productId }, tx);

            var rows = await conn.ExecuteAsync(
                "UPDATE [Catalog].[Product_Media] SET Is_Main = 1 WHERE Media_ID = @MediaId AND Product_ID = @ProductId",
                new { MediaId = mediaId, ProductId = productId }, tx);

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

    public async Task<Result<bool>> DeleteProductMediaAsync(int storeId, int productId, int mediaId)
    {
        using var conn = _dapperContext.CreateConnection();
        try
        {
            if (!await ProductBelongsToStoreAsync(conn, productId, storeId))
                return Result<bool>.Failure("El producto no existe o no pertenece a tu tienda.");

            var isMain = await conn.QuerySingleOrDefaultAsync<bool?>(
                "SELECT Is_Main FROM [Catalog].[Product_Media] WHERE Media_ID = @MediaId AND Product_ID = @ProductId",
                new { MediaId = mediaId, ProductId = productId });

            if (isMain == null)
                return Result<bool>.Failure("La imagen no existe en este producto.");

            if (isMain == true)
            {
                var count = await conn.QuerySingleAsync<int>(
                    "SELECT COUNT(*) FROM [Catalog].[Product_Media] WHERE Product_ID = @ProductId",
                    new { ProductId = productId });
                if (count == 1)
                    return Result<bool>.Failure("No puedes eliminar la única imagen del producto.");
            }

            await conn.ExecuteAsync(
                "DELETE FROM [Catalog].[Product_Media] WHERE Media_ID = @MediaId AND Product_ID = @ProductId",
                new { MediaId = mediaId, ProductId = productId });

            return Result<bool>.Success(true);
        }
        catch (Exception ex)
        {
            return Result<bool>.Failure($"Error al eliminar imagen: {ex.Message}");
        }
    }
}
