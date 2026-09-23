using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.Carrier;

public class CreateCarrierRequest
{
    /// <summary>
    /// Nombre del proveedor logístico.
    /// </summary>
    /// <example>Cargo Expreso</example>
    [Required]
    public string CarrierName { get; set; } = string.Empty;

    /// <summary>
    /// Clave de integración API del carrier (opcional).
    /// </summary>
    /// <example>sk_live_abc123xyz</example>
    public string? ApiIntegrationKey { get; set; }

    /// <summary>
    /// Indica si el carrier ofrece el servicio de Pago al Recibir (COD - Cash On Delivery).
    /// </summary>
    /// <example>true</example>
    public bool OffersCOD { get; set; }

    /// <summary>
    /// Porcentaje de comisión que cobra el carrier por el servicio COD.
    /// Solo aplica si OffersCOD es true.
    /// </summary>
    /// <example>3.50</example>
    public decimal? CodFeePercentage { get; set; }
}
