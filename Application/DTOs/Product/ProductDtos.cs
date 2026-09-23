using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.Product;

/// <summary>
/// DTO para crear un nuevo producto. Las dimensiones son obligatorias
/// para calcular el costo de envío con las paqueteras integradas (CAEX, Cargo Expreso).
/// </summary>
public class CreateProductRequest
{
    // ── Identificación ──

    /// <summary>
    /// Código único de referencia del producto (SKU).
    /// </summary>
    /// <example>FERR-TALADRO-001</example>
    [Required]
    public string SKU { get; set; } = string.Empty;

    /// <summary>
    /// Nombre del producto visible al público.
    /// </summary>
    /// <example>Taladro Percutor 850W</example>
    [Required]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Descripción detallada del producto.
    /// </summary>
    /// <example>Taladro percutor de alta potencia, ideal para concreto y madera.</example>
    public string? Description { get; set; }

    /// <summary>
    /// ID de la categoría del producto (ver /api/categories para el catálogo).
    /// </summary>
    /// <example>12</example>
    [Required]
    public int CategoryId { get; set; }

    /// <summary>
    /// Marca del producto.
    /// </summary>
    /// <example>Dewalt</example>
    public string? Brand { get; set; }

    // ── Dimensiones (Obligatorias para cálculo de envío con paqueteras) ──

    /// <summary>
    /// Peso del producto en kilogramos (kg). Requerido por las paqueteras para calcular el flete.
    /// </summary>
    /// <example>2.50</example>
    [Required]
    public decimal WeightKg { get; set; }

    /// <summary>
    /// Ancho del producto empacado en centímetros (cm).
    /// </summary>
    /// <example>25.00</example>
    [Required]
    public decimal WidthCm { get; set; }

    /// <summary>
    /// Alto del producto empacado en centímetros (cm).
    /// </summary>
    /// <example>30.00</example>
    [Required]
    public decimal HeightCm { get; set; }

    /// <summary>
    /// Largo del producto empacado en centímetros (cm).
    /// </summary>
    /// <example>40.00</example>
    [Required]
    public decimal LengthCm { get; set; }
}

/// <summary>
/// DTO de respuesta para un producto creado o consultado.
/// </summary>
public class ProductDto
{
    public int ProductId { get; set; }
    public int StoreId { get; set; }
    public string SKU { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int CategoryId { get; set; }
    public string? Brand { get; set; }

    /// <summary>
    /// Peso en kg para cálculo de flete.
    /// </summary>
    public decimal WeightKg { get; set; }

    /// <summary>
    /// Ancho en cm para cálculo de flete.
    /// </summary>
    public decimal WidthCm { get; set; }

    /// <summary>
    /// Alto en cm para cálculo de flete.
    /// </summary>
    public decimal HeightCm { get; set; }

    /// <summary>
    /// Largo en cm para cálculo de flete.
    /// </summary>
    public decimal LengthCm { get; set; }

    public string? StatusName { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// DTO que representa un error en una fila específica durante la carga masiva.
/// </summary>
public class BulkProductError
{
    public int RowNumber { get; set; }
    public string? SKU { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
}

/// <summary>
/// Resultado del proceso de carga masiva de productos.
/// </summary>
public class BulkProductUploadResult
{
    public int TotalProcessed { get; set; }
    public int SuccessfulCount { get; set; }
    public int FailedCount { get; set; }
    public List<BulkProductError> Errors { get; set; } = new();
}
