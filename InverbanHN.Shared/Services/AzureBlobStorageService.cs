using System;
using System.IO;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace InverbanHN.Shared.Services
{
    public class AzureBlobStorageService : IMediaService
    {
        private readonly IConfiguration _configuration;
        private readonly string _connectionString;
        private readonly string _containerName;

        public AzureBlobStorageService(IConfiguration configuration)
        {
            _configuration = configuration;
            _connectionString = _configuration.GetConnectionString("AzureWebJobsStorage")
                                ?? _configuration["AzureWebJobsStorage"]
                                ?? _configuration["ConnectionStrings:AzureWebJobsStorage"]
                                ?? "UseDevelopmentStorage=true";

            _containerName = _configuration["AzureBlobStorage:ContainerName"] ?? "vendor-media";
        }

        public async Task<string> UploadMediaAsync(IFormFile file, int storeId, string folder = "products")
        {
            if (file == null || file.Length == 0)
            {
                throw new ArgumentException("El archivo proporcionado está vacío o no es válido.");
            }

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            var uniqueFileName = $"{Guid.NewGuid():N}{extension}";

            // Estructura de blob: store-{StoreId}/{folder}/{guid}.jpg
            var blobPath = $"store-{storeId}/{folder.Trim('/').ToLowerInvariant()}/{uniqueFileName}";

            // Inicializa cliente de Azure Blob
            var blobServiceClient = new BlobServiceClient(_connectionString);
            var containerClient = blobServiceClient.GetBlobContainerClient(_containerName);

            // Asegura que el contenedor exista con acceso público a nivel de Blob
            await containerClient.CreateIfNotExistsAsync(PublicAccessType.Blob);

            var blobClient = containerClient.GetBlobClient(blobPath);

            var blobHttpHeaders = new BlobHttpHeaders
            {
                ContentType = file.ContentType ?? GetContentTypeByExtension(extension)
            };

            using (var stream = file.OpenReadStream())
            {
                await blobClient.UploadAsync(stream, new BlobUploadOptions
                {
                    HttpHeaders = blobHttpHeaders
                });
            }

            return blobClient.Uri.ToString();
        }

        private static string GetContentTypeByExtension(string extension)
        {
            return extension switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".webp" => "image/webp",
                _ => "application/octet-stream"
            };
        }
    }
}
