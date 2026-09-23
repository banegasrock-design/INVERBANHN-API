using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace InverbanHN.Shared.Services
{
    public interface IMediaService
    {
        /// <summary>
        /// Sube un archivo de imagen o media a Azure Blob Storage.
        /// </summary>
        /// <param name="file">Archivo cargado por el cliente</param>
        /// <param name="storeId">ID de la tienda para aislar la ruta del blob</param>
        /// <param name="folder">Nombre de la subcarpeta (ej. "products", "logos", "banners")</param>
        /// <returns>URL pública absoluta generada por Azure Blob Storage</returns>
        Task<string> UploadMediaAsync(IFormFile file, int storeId, string folder = "products");
    }
}
