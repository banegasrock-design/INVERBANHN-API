using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.Wallet;

public class MixedPaymentRequest
{
    /// <summary>
    /// Monto total de la orden en Lempiras (LPS). Precisión: 2 decimales.
    /// </summary>
    /// <example>1250.50</example>
    [Required]
    public decimal TotalOrder { get; set; }

    /// <summary>
    /// Indica si el usuario desea utilizar el saldo de su Wallet para cubrir parte del pago.
    /// </summary>
    /// <example>true</example>
    public bool UseWallet { get; set; }

    /// <summary>
    /// Identificador único de la orden de compra.
    /// </summary>
    /// <example>ORD-2024-0001</example>
    [Required]
    public string OrderId { get; set; } = string.Empty;
}
