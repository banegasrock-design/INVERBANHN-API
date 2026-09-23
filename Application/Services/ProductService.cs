using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Application.Common;
using Application.DTOs.Product;
using Application.Interfaces;
using ClosedXML.Excel;
using Dapper;
using Infrastructure.Data.Contexts;
using Microsoft.Data.SqlClient;

namespace Application.Services;

public class ProductService : IProductService
{
    private readonly DapperContext _dapperContext;

    public ProductService(DapperContext dapperContext)
    {
        _dapperContext = dapperContext;
    }

    public async Task<Result<IEnumerable<ProductDto>>> GetMyProductsAsync(int storeId)
    {
        // El Store_ID viene inyectado desde el JWT por el controlador — nunca desde el body.
        using var connection = _dapperContext.CreateConnection();
        try
        {
            var sql = @"
                SELECT 
                    Product_ID       AS ProductId,
                    Store_ID         AS StoreId,
                    SKU,
                    Name,
                    Description,
                    Category_ID      AS CategoryId,
                    Brand,
                    Weight_Kg        AS WeightKg,
                    Width_Cm         AS WidthCm,
                    Height_Cm        AS HeightCm,
                    Length_Cm        AS LengthCm,
                    Status_Name      AS StatusName,
                    Created_At       AS CreatedAt
                FROM [Catalog].[Products]
                WHERE Store_ID = @StoreId
                  AND Status_Name != 'Inactivo'
                ORDER BY Created_At DESC";

            var products = await connection.QueryAsync<ProductDto>(sql, new { StoreId = storeId });
            return Result<IEnumerable<ProductDto>>.Success(products);
        }
        catch (Exception ex)
        {
            return Result<IEnumerable<ProductDto>>.Failure($"Error al obtener los productos: {ex.Message}");
        }
    }

    public async Task<Result<ProductDto>> CreateProductAsync(int storeId, CreateProductRequest request)
    {
        using var connection = _dapperContext.CreateConnection();
        try
        {
            var sql = @"
                INSERT INTO [Catalog].[Products]
                    (Store_ID, SKU, Name, Description, Category_ID, Brand,
                     Weight_Kg, Width_Cm, Height_Cm, Length_Cm, Status_Name, Created_At)
                VALUES
                    (@Store_ID, @SKU, @Name, @Description, @Category_ID, @Brand,
                     @Weight_Kg, @Width_Cm, @Height_Cm, @Length_Cm, 'Activo', @Created_At);
                SELECT CAST(SCOPE_IDENTITY() AS INT);";

            var newId = await connection.QuerySingleAsync<int>(sql, new
            {
                Store_ID    = storeId,        // ← Siempre del JWT, nunca del body
                SKU         = request.SKU,
                Name        = request.Name,
                Description = request.Description,
                Category_ID = request.CategoryId,
                Brand       = request.Brand,
                Weight_Kg   = request.WeightKg,
                Width_Cm    = request.WidthCm,
                Height_Cm   = request.HeightCm,
                Length_Cm   = request.LengthCm,
                Created_At  = DateTime.UtcNow
            });

            // Leer el producto recién creado para devolver la respuesta completa
            var created = await GetProductByIdInternalAsync(connection, storeId, newId);
            return Result<ProductDto>.Success(created!);
        }
        catch (SqlException ex) when (ex.Number == 2627) // UNIQUE CONSTRAINT violation (SKU duplicado)
        {
            return Result<ProductDto>.Failure($"El SKU '{request.SKU}' ya existe en tu tienda. Usa un código único.");
        }
        catch (Exception ex)
        {
            return Result<ProductDto>.Failure($"Error al crear el producto: {ex.Message}");
        }
    }

    public async Task<Result<ProductDto>> UpdateProductAsync(int storeId, int productId, CreateProductRequest request)
    {
        using var connection = _dapperContext.CreateConnection();
        try
        {
            // Verificar ownership — la tienda solo puede editar SUS propios productos
            var exists = await connection.QuerySingleOrDefaultAsync<int?>(
                "SELECT Product_ID FROM [Catalog].[Products] WHERE Product_ID = @ProductId AND Store_ID = @StoreId",
                new { ProductId = productId, StoreId = storeId });

            if (exists is null)
                return Result<ProductDto>.Failure("El producto no existe o no pertenece a tu tienda.");

            var sql = @"
                UPDATE [Catalog].[Products] SET
                    SKU         = @SKU,
                    Name        = @Name,
                    Description = @Description,
                    Category_ID = @Category_ID,
                    Brand       = @Brand,
                    Weight_Kg   = @Weight_Kg,
                    Width_Cm    = @Width_Cm,
                    Height_Cm   = @Height_Cm,
                    Length_Cm   = @Length_Cm
                WHERE Product_ID = @ProductId AND Store_ID = @StoreId";

            await connection.ExecuteAsync(sql, new
            {
                ProductId   = productId,
                StoreId     = storeId,
                SKU         = request.SKU,
                Name        = request.Name,
                Description = request.Description,
                Category_ID = request.CategoryId,
                Brand       = request.Brand,
                Weight_Kg   = request.WeightKg,
                Width_Cm    = request.WidthCm,
                Height_Cm   = request.HeightCm,
                Length_Cm   = request.LengthCm
            });

            var updated = await GetProductByIdInternalAsync(connection, storeId, productId);
            return Result<ProductDto>.Success(updated!);
        }
        catch (SqlException ex) when (ex.Number == 2627)
        {
            return Result<ProductDto>.Failure($"El SKU '{request.SKU}' ya está en uso por otro producto de tu tienda.");
        }
        catch (Exception ex)
        {
            return Result<ProductDto>.Failure($"Error al actualizar el producto: {ex.Message}");
        }
    }

    public async Task<Result<bool>> DeactivateProductAsync(int storeId, int productId)
    {
        using var connection = _dapperContext.CreateConnection();
        try
        {
            var rows = await connection.ExecuteAsync(
                "UPDATE [Catalog].[Products] SET Status_Name = 'Inactivo' WHERE Product_ID = @ProductId AND Store_ID = @StoreId",
                new { ProductId = productId, StoreId = storeId });

            if (rows == 0)
                return Result<bool>.Failure("El producto no existe o no pertenece a tu tienda.");

            return Result<bool>.Success(true);
        }
        catch (Exception ex)
        {
            return Result<bool>.Failure($"Error al desactivar el producto: {ex.Message}");
        }
    }

    // ── Método privado reutilizable ──
    private static async Task<ProductDto?> GetProductByIdInternalAsync(
        System.Data.IDbConnection connection, int storeId, int productId)
    {
        var sql = @"
            SELECT 
                Product_ID AS ProductId, Store_ID AS StoreId, SKU, Name,
                Description, Category_ID AS CategoryId, Brand,
                Weight_Kg AS WeightKg, Width_Cm AS WidthCm,
                Height_Cm AS HeightCm, Length_Cm AS LengthCm,
                Status_Name AS StatusName, Created_At AS CreatedAt
            FROM [Catalog].[Products]
            WHERE Product_ID = @ProductId AND Store_ID = @StoreId";

        return await connection.QuerySingleOrDefaultAsync<ProductDto>(
            sql, new { ProductId = productId, StoreId = storeId });
    }

    public async Task<Result<BulkProductUploadResult>> BulkUploadProductsAsync(int storeId, Stream excelStream)
    {
        var result = new BulkProductUploadResult();
        var validProducts = new List<CreateProductRequest>();
        
        using var connection = _dapperContext.CreateConnection();
        
        try
        {
            // 1. Cargar categorías y SKUs en caché
            var validCategoryIds = new HashSet<int>(await connection.QueryAsync<int>("SELECT Category_ID FROM [Logistics].[Categories]"));
            var existingSkus = new HashSet<string>(await connection.QueryAsync<string>("SELECT SKU FROM [Catalog].[Products] WHERE Store_ID = @StoreId", new { StoreId = storeId }), StringComparer.OrdinalIgnoreCase);
            var newSkusInFile = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            using var workbook = new XLWorkbook(excelStream);
            var worksheet = workbook.Worksheets.FirstOrDefault();
            
            if (worksheet == null)
            {
                return Result<BulkProductUploadResult>.Failure("El archivo Excel está vacío o no tiene hojas.");
            }

            // Buscar encabezados
            var firstRow = worksheet.FirstRowUsed();
            if (firstRow == null)
            {
                return Result<BulkProductUploadResult>.Failure("El archivo Excel no tiene datos.");
            }

            var colMapping = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            int colIndex = 1;
            foreach (var cell in firstRow.Cells())
            {
                var val = cell.GetString().Trim();
                if (!string.IsNullOrEmpty(val))
                {
                    colMapping[val] = colIndex;
                }
                colIndex++;
            }

            // Validar que las columnas requeridas existen
            var requiredCols = new[] { "SKU", "Name", "CategoryId", "WeightKg", "WidthCm", "HeightCm", "LengthCm" };
            var missingCols = requiredCols.Where(c => !colMapping.ContainsKey(c)).ToList();
            if (missingCols.Any())
            {
                return Result<BulkProductUploadResult>.Failure($"Faltan columnas requeridas en el encabezado: {string.Join(", ", missingCols)}");
            }

            var rows = worksheet.RowsUsed().Skip(1); // Saltar encabezado
            foreach (var row in rows)
            {
                result.TotalProcessed++;
                int rowNum = row.RowNumber();
                
                try 
                {
                    string sku = row.Cell(colMapping["SKU"]).GetString().Trim();
                    if (string.IsNullOrEmpty(sku))
                    {
                        AddError(result, rowNum, sku, "El SKU es obligatorio.");
                        continue;
                    }
                    if (newSkusInFile.Contains(sku))
                    {
                        AddError(result, rowNum, sku, "SKU duplicado dentro del mismo archivo Excel.");
                        continue;
                    }

                    string name = row.Cell(colMapping["Name"]).GetString().Trim();
                    if (string.IsNullOrEmpty(name))
                    {
                        AddError(result, rowNum, sku, "El nombre del producto es obligatorio.");
                        continue;
                    }

                    if (!int.TryParse(row.Cell(colMapping["CategoryId"]).GetString(), out int categoryId) || !validCategoryIds.Contains(categoryId))
                    {
                        AddError(result, rowNum, sku, "El CategoryId no es válido o no existe.");
                        continue;
                    }

                    if (!decimal.TryParse(row.Cell(colMapping["WeightKg"]).GetString(), out decimal weightKg) || weightKg <= 0)
                    {
                        AddError(result, rowNum, sku, "El peso (WeightKg) debe ser mayor a 0.");
                        continue;
                    }

                    if (!decimal.TryParse(row.Cell(colMapping["WidthCm"]).GetString(), out decimal widthCm) || widthCm <= 0)
                    {
                        AddError(result, rowNum, sku, "El ancho (WidthCm) debe ser mayor a 0.");
                        continue;
                    }

                    if (!decimal.TryParse(row.Cell(colMapping["HeightCm"]).GetString(), out decimal heightCm) || heightCm <= 0)
                    {
                        AddError(result, rowNum, sku, "El alto (HeightCm) debe ser mayor a 0.");
                        continue;
                    }

                    if (!decimal.TryParse(row.Cell(colMapping["LengthCm"]).GetString(), out decimal lengthCm) || lengthCm <= 0)
                    {
                        AddError(result, rowNum, sku, "El largo (LengthCm) debe ser mayor a 0.");
                        continue;
                    }

                    string? description = colMapping.ContainsKey("Description") ? row.Cell(colMapping["Description"]).GetString().Trim() : null;
                    string? brand = colMapping.ContainsKey("Brand") ? row.Cell(colMapping["Brand"]).GetString().Trim() : null;

                    newSkusInFile.Add(sku);
                    validProducts.Add(new CreateProductRequest
                    {
                        SKU = sku,
                        Name = name,
                        Description = description,
                        CategoryId = categoryId,
                        Brand = brand,
                        WeightKg = weightKg,
                        WidthCm = widthCm,
                        HeightCm = heightCm,
                        LengthCm = lengthCm
                    });
                }
                catch (Exception ex)
                {
                    AddError(result, rowNum, null, $"Error al leer la fila: {ex.Message}");
                }
            }

            if (validProducts.Any())
            {
                connection.Open();
                using var tx = connection.BeginTransaction();
                try
                {
                    var sql = @"
                        INSERT INTO [Catalog].[Products]
                            (Store_ID, SKU, Name, Description, Category_ID, Brand,
                             Weight_Kg, Width_Cm, Height_Cm, Length_Cm, Status_Name, Created_At)
                        VALUES
                            (@Store_ID, @SKU, @Name, @Description, @Category_ID, @Brand,
                             @Weight_Kg, @Width_Cm, @Height_Cm, @Length_Cm, 'Activo', @Created_At)";

                    var parameters = validProducts.Select(p => new
                    {
                        Store_ID = storeId,
                        SKU = p.SKU,
                        Name = p.Name,
                        Description = p.Description,
                        Category_ID = p.CategoryId,
                        Brand = p.Brand,
                        Weight_Kg = p.WeightKg,
                        Width_Cm = p.WidthCm,
                        Height_Cm = p.HeightCm,
                        Length_Cm = p.LengthCm,
                        Created_At = DateTime.UtcNow
                    });

                    await connection.ExecuteAsync(sql, parameters, tx);
                    tx.Commit();
                    
                    result.SuccessfulCount = validProducts.Count;
                }
                catch (Exception ex)
                {
                    tx.Rollback();
                    return Result<BulkProductUploadResult>.Failure($"Error crítico al insertar los productos: {ex.Message}");
                }
            }

            return Result<BulkProductUploadResult>.Success(result);
        }
        catch (Exception ex)
        {
            return Result<BulkProductUploadResult>.Failure($"Error al procesar el archivo Excel: {ex.Message}");
        }
    }

    private void AddError(BulkProductUploadResult result, int rowNumber, string? sku, string message)
    {
        result.FailedCount++;
        result.Errors.Add(new BulkProductError
        {
            RowNumber = rowNumber,
            SKU = sku,
            ErrorMessage = message
        });
    }

    public async Task<Result<byte[]>> ExportProductsToExcelAsync(int storeId)
    {
        using var connection = _dapperContext.CreateConnection();
        try
        {
            var sql = @"
                SELECT 
                    SKU, Name, Category_ID AS CategoryId, Weight_Kg AS WeightKg, 
                    Width_Cm AS WidthCm, Height_Cm AS HeightCm, Length_Cm AS LengthCm, 
                    Description, Brand
                FROM [Catalog].[Products]
                WHERE Store_ID = @StoreId AND Status_Name != 'Inactivo'
                ORDER BY Name";

            var products = await connection.QueryAsync(sql, new { StoreId = storeId });

            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Productos");

            // Encabezados
            var headers = new[] { "SKU", "Name", "CategoryId", "WeightKg", "WidthCm", "HeightCm", "LengthCm", "Description", "Brand" };
            for (int i = 0; i < headers.Length; i++)
            {
                worksheet.Cell(1, i + 1).Value = headers[i];
                worksheet.Cell(1, i + 1).Style.Font.Bold = true;
            }

            // Datos
            int row = 2;
            foreach (var p in products)
            {
                worksheet.Cell(row, 1).Value = (string)p.SKU;
                worksheet.Cell(row, 2).Value = (string)p.Name;
                worksheet.Cell(row, 3).Value = (int)p.CategoryId;
                worksheet.Cell(row, 4).Value = (decimal)p.WeightKg;
                worksheet.Cell(row, 5).Value = (decimal)p.WidthCm;
                worksheet.Cell(row, 6).Value = (decimal)p.HeightCm;
                worksheet.Cell(row, 7).Value = (decimal)p.LengthCm;
                worksheet.Cell(row, 8).Value = p.Description != null ? (string)p.Description : "";
                worksheet.Cell(row, 9).Value = p.Brand != null ? (string)p.Brand : "";
                row++;
            }

            worksheet.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return Result<byte[]>.Success(stream.ToArray());
        }
        catch (Exception ex)
        {
            return Result<byte[]>.Failure($"Error al exportar productos a Excel: {ex.Message}");
        }
    }
}
