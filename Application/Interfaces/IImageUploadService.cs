using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Application.Interfaces;

public record ImageUploadResult(string OriginalUrl, string MediumUrl, string ThumbnailUrl, string MimeType, long SizeBytes);

public interface IImageUploadService
{
    /// <summary>
    /// Recibe un archivo, lo redimensiona (Medium y Thumbnail), lo convierte a WebP 
    /// y lo sube a Azure Blob Storage.
    /// </summary>
    /// <param name="file">El archivo subido por el cliente.</param>
    /// <param name="fileNamePrefix">Prefijo para el nombre de los archivos en Blob Storage.</param>
    /// <returns>Los URLs de las imágenes subidas y metadatos.</returns>
    Task<ImageUploadResult> ProcessAndUploadImageAsync(IFormFile file, string fileNamePrefix);
}
