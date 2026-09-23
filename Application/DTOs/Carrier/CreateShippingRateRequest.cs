using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.Carrier;

public class CreateShippingRateRequest
{
    /// <summary>
    /// ID del carrier al que pertenece esta tarifa.
    /// </summary>
    /// <example>1</example>
    [Required]
    public int CarrierId { get; set; }

    /// <summary>
    /// Ciudad de origen del envío.
    /// </summary>
    /// <example>San Pedro Sula</example>
    [Required]
    public string OriginCity { get; set; } = string.Empty;

    /// <summary>
    /// Ciudad de destino del envío.
    /// </summary>
    /// <example>Tegucigalpa</example>
    [Required]
    public string DestinationCity { get; set; } = string.Empty;

    /// <summary>
    /// Costo que el ecommerce paga al carrier en Lempiras (LPS). Precisión: 2 decimales.
    /// </summary>
    /// <example>85.00</example>
    [Required]
    public decimal CostToEcommerce { get; set; }

    /// <summary>
    /// Precio que se le cobra al cliente final en Lempiras (LPS). Precisión: 2 decimales.
    /// </summary>
    /// <example>120.00</example>
    [Required]
    public decimal PriceToCustomer { get; set; }
}
