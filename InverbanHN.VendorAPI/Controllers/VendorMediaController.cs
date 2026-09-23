using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using InverbanHN.VendorAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace InverbanHN.VendorAPI.Controllers
{
    [ApiController]
    [Route("api/vendor/media")]
    [Authorize(Roles = "StoreAdmin,StoreOperator")]
    public class VendorMediaController : VendorBaseController
    {
        private readonly IMediaService _mediaService;

        // Tamaño máximo permitido: 5 MB (5 * 1024 * 1024 bytes)
        private const long MaxFileSizeBytes = 5 * 1024 * 1024;
        private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".webp" };

        public VendorMediaController(IMediaService mediaService)
        {
            _mediaService = mediaService;
        }

        /// <summary>
        /// POST /api/vendor/media/upload
        /// Sube una imagen a Azure Blob Storage aislada por Store_ID.
        /// VALIDACIONES:
        /// 1. Tamaño máximo de 5 MB.
        /// 2. Extensiones permitidas: .jpg, .jpeg, .png, .webp.
        /// Retorna JSON con la estructura: { "url": "https://..." }
        /// </summary>
        [HttpPost("upload")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadMedia([FromForm] IFormFile file, [FromForm] string? folder = "products")
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Archivo no recibido",
                    Status = StatusCodes.Status400BadRequest,
                    Detail = "Por favor adjunte un archivo de imagen válido mediante multipart/form-data."
                });
            }

            // REGLA A: Validar tamaño máximo (<= 5 MB)
            if (file.Length > MaxFileSizeBytes)
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Archivo Demasiado Grande",
                    Status = StatusCodes.Status400BadRequest,
                    Detail = $"El archivo excede el tamaño máximo permitido de 5 MB. Tamaño del archivo cargado: {file.Length / (1024.0 * 1024.0):F2} MB."
                });
            }

            // REGLA B: Validar extensiones permitidas (.jpg, .jpeg, .png, .webp)
            var extension = Path.GetExtension(file.FileName)?.ToLowerInvariant();
            if (string.IsNullOrEmpty(extension) || !AllowedExtensions.Contains(extension))
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Formato de Archivo No Permitido",
                    Status = StatusCodes.Status400BadRequest,
                    Detail = $"Formato '{extension}' no válido. Solo se admiten archivos de imagen con formato: {string.Join(", ", AllowedExtensions)}."
                });
            }

            try
            {
                int storeId = GetStoreId();
                string cleanFolder = string.IsNullOrWhiteSpace(folder) ? "products" : folder;

                string absoluteBlobUrl = await _mediaService.UploadMediaAsync(file, storeId, cleanFolder);

                return Ok(new
                {
                    url = absoluteBlobUrl,
                    fileName = file.FileName,
                    sizeBytes = file.Length,
                    uploadedAt = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
                {
                    Title = "Error en Azure Blob Storage",
                    Status = StatusCodes.Status500InternalServerError,
                    Detail = ex.Message,
                    Instance = HttpContext.Request.Path
                });
            }
        }
    }
}
