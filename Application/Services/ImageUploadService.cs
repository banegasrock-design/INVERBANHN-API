using System;
using System.IO;
using System.Threading.Tasks;
using Application.Interfaces;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace Application.Services;

public class ImageUploadService : IImageUploadService
{
    private readonly string _connectionString;
    private readonly string _containerName;

    public ImageUploadService(IConfiguration configuration)
    {
        _connectionString = configuration["AzureStorage:ConnectionString"] ?? throw new ArgumentNullException("AzureStorage:ConnectionString is missing");
        _containerName = configuration["AzureStorage:ContainerName"] ?? "product-images";
    }

    public async Task<ImageUploadResult> ProcessAndUploadImageAsync(IFormFile file, string fileNamePrefix)
    {
        if (file == null || file.Length == 0)
            throw new ArgumentException("El archivo es inválido o está vacío.", nameof(file));

        var blobServiceClient = new BlobServiceClient(_connectionString);
        var containerClient = blobServiceClient.GetBlobContainerClient(_containerName);
        try
        {
            await containerClient.CreateIfNotExistsAsync(PublicAccessType.Blob);
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 403)
        {
            // Omitir si la firma SAS a nivel de contenedor no permite verificar o crear el contenedor
        }

        using var imageStream = file.OpenReadStream();
        using var image = await Image.LoadAsync(imageStream);

        string originalFileName = $"{fileNamePrefix}-{Guid.NewGuid()}.webp";
        string mediumFileName = $"{fileNamePrefix}-{Guid.NewGuid()}-medium.webp";
        string thumbnailFileName = $"{fileNamePrefix}-{Guid.NewGuid()}-thumb.webp";

        // Configuración WebP
        var encoder = new WebpEncoder { Quality = 80 };

        // 1. Original / Large (Max 1200x1200px)
        using var originalStream = new MemoryStream();
        var originalImage = image.Clone(x => x.Resize(new ResizeOptions
        {
            Mode = ResizeMode.Max,
            Size = new Size(1200, 1200)
        }));
        await originalImage.SaveAsync(originalStream, encoder);
        originalStream.Position = 0;
        long sizeBytes = originalStream.Length;
        var originalUrl = await UploadToBlobAsync(containerClient, originalFileName, originalStream);

        // 2. Medium (Max 600x600px)
        using var mediumStream = new MemoryStream();
        var mediumImage = image.Clone(x => x.Resize(new ResizeOptions
        {
            Mode = ResizeMode.Max,
            Size = new Size(600, 600)
        }));
        await mediumImage.SaveAsync(mediumStream, encoder);
        mediumStream.Position = 0;
        var mediumUrl = await UploadToBlobAsync(containerClient, mediumFileName, mediumStream);

        // 3. Thumbnail (Max 150x150px)
        using var thumbStream = new MemoryStream();
        var thumbImage = image.Clone(x => x.Resize(new ResizeOptions
        {
            Mode = ResizeMode.Max,
            Size = new Size(150, 150)
        }));
        await thumbImage.SaveAsync(thumbStream, encoder);
        thumbStream.Position = 0;
        var thumbUrl = await UploadToBlobAsync(containerClient, thumbnailFileName, thumbStream);

        return new ImageUploadResult(originalUrl, mediumUrl, thumbUrl, "image/webp", sizeBytes);
    }

    private async Task<string> UploadToBlobAsync(BlobContainerClient containerClient, string blobName, Stream content)
    {
        var blobClient = containerClient.GetBlobClient(blobName);
        var options = new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = "image/webp" }
        };
        await blobClient.UploadAsync(content, options);
        return blobClient.Uri.ToString();
    }
}


