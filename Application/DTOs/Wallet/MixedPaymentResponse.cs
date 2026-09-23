namespace Application.DTOs.Wallet;

public class MixedPaymentResponse
{
    /// <summary>
    /// Monto total de la orden en Lempiras (LPS).
    /// </summary>
    /// <example>1250.50</example>
    public decimal TotalOrder { get; set; }

    /// <summary>
    /// Monto debitado de la Wallet del cliente en Lempiras (LPS).
    /// </summary>
    /// <example>500.00</example>
    public decimal WalletAmountUsed { get; set; }

    /// <summary>
    /// Diferencia pendiente a cobrar vía pasarela de pagos en Lempiras (LPS).
    /// </summary>
    /// <example>750.50</example>
    public decimal DiferenciaPendiente { get; set; }
}
