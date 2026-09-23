using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.Product;

// ── IMAGE DTOs ──────────────────────────────────────────────────────────────

/// <summary>
/// Solicitud para registrar una nueva imagen de producto.
/// La URL de la imagen debe provenir de Azure Blob Storage o CDN ya subida.
/// </summary>
public class AddProductImageRequest
{
    /// <summary>
    /// URL pública de la imagen (Azure Blob Storage / CDN).
    /// </summary>
    /// <example>https://cdn.inverbanhn.com/products/taladro-001.webp</example>
    [Required]
    public string ImageUrl { get; set; } = string.Empty;

    /// <summary>
    /// URL de la miniatura (thumbnail) de la imagen.
    /// </summary>
    /// <example>https://cdn.inverbanhn.com/products/taladro-001-thumb.webp</example>
    public string? ThumbnailUrl { get; set; }

    /// <summary>
    /// URL de versión mediana de la imagen (para listados en React).
    /// </summary>
    public string? MediumUrl { get; set; }

    /// <summary>
    /// Texto alternativo SEO-friendly para la imagen.
    /// </summary>
    /// <example>Taladro percutor 850W vista frontal</example>
    public string? AltText { get; set; }

    /// <summary>
    /// Si es true, esta imagen será la imagen principal del producto (portada).
    /// Solo puede haber una imagen principal por producto.
    /// </summary>
    /// <example>true</example>
    public bool IsMain { get; set; }

    /// <summary>
    /// Orden de visualización en la galería (0 = primero).
    /// </summary>
    /// <example>0</example>
    public int DisplayOrder { get; set; }

    /// <summary>
    /// ID de la variante a la que pertenece esta imagen (opcional).
    /// </summary>
    public int? VariantId { get; set; }

    /// <summary>
    /// Tipo MIME de la imagen para validación.
    /// </summary>
    /// <example>image/webp</example>
    public string? MimeType { get; set; }

    /// <summary>
    /// Tamaño del archivo en bytes.
    /// </summary>
    /// <example>245760</example>
    public long? ImageSizeBytes { get; set; }
}

public class ProductImageDto
{
    public int ImageId { get; set; }
    public int ProductId { get; set; }
    public int? VariantId { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public string? ThumbnailUrl { get; set; }
    public string? MediumUrl { get; set; }
    public string? AltText { get; set; }
    public bool IsMain { get; set; }
    public int DisplayOrder { get; set; }
    public string? MimeType { get; set; }
    public long? ImageSizeBytes { get; set; }
    public DateTime? CreatedAt { get; set; }
}

// ── VARIANT DTOs ────────────────────────────────────────────────────────────

/// <summary>
/// Solicitud para crear una variante de producto (talla, color, capacidad, etc.).
/// El peso de la variante sobreescribe al del producto padre para el cálculo de flete.
/// </summary>
public class CreateVariantRequest
{
    /// <summary>
    /// SKU único de la variante dentro del catálogo de la tienda.
    /// </summary>
    /// <example>FERR-TALADRO-001-850W-AZUL</example>
    [Required]
    public string SKU { get; set; } = string.Empty;

    /// <summary>
    /// Nombre descriptivo de la variante.
    /// </summary>
    /// <example>Taladro 850W — Color Azul</example>
    [Required]
    public string VariantName { get; set; } = string.Empty;

    /// <summary>
    /// Descripción específica de esta variante.
    /// </summary>
    public string? VariantDescription { get; set; }

    /// <summary>
    /// Precio de venta de esta variante en Lempiras (LPS). Precisión: 2 decimales.
    /// </summary>
    /// <example>1850.00</example>
    [Required]
    public decimal PriceAmount { get; set; }

    /// <summary>
    /// Peso específico de esta variante en kg (sobreescribe el peso del producto padre para flete).
    /// </summary>
    /// <example>2.80</example>
    public decimal? WeightKg { get; set; }

    /// <summary>
    /// Si es true, esta variante es un producto digital (no requiere envío físico).
    /// </summary>
    /// <example>false</example>
    public bool IsDigitalDownload { get; set; }

    /// <summary>
    /// Tipo de posicionamiento u oferta comercial para esta variante en la tienda online.
    /// Valores: 'Sin Oferta', 'Oferta Especial', 'Oferta del Día', 'Destacado', 'Más Vendido'.
    /// </summary>
    /// <example>Oferta Especial</example>
    public string OfferType { get; set; } = "Sin Oferta";
}

public class ProductVariantDto
{
    public int VariantId { get; set; }
    public int ProductId { get; set; }
    public string SKU { get; set; } = string.Empty;
    public string? VariantName { get; set; }
    public string? VariantDescription { get; set; }

    /// <summary>
    /// Precio de la variante en Lempiras (LPS).
    /// </summary>
    public decimal PriceAmount { get; set; }

    /// <summary>
    /// Peso en kg para cálculo de flete. Si es null, se usa el del producto padre.
    /// </summary>
    public decimal? WeightKg { get; set; }

    public bool IsDigitalDownload { get; set; }

    /// <summary>
    /// Tipo de oferta/posicionamiento en la tienda online (ej. 'Destacado', 'Oferta del Día').
    /// </summary>
    public string OfferType { get; set; } = "Sin Oferta";
}
