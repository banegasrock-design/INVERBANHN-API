using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.Carrier;

public class QuoteRequestDto
{
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
    /// ID del carrier (paquetera) a consultar.
    /// </summary>
    /// <example>1</example>
    public int CarrierId { get; set; }
}
