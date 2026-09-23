using System;

namespace Domain.Entities.Sales;

public class WalletTransaction
{
    public int Id { get; set; }
    public int WalletId { get; set; }
    public decimal Amount { get; set; } // Positivo = Abono, Negativo = Cargo
    public string TransactionType { get; set; } = string.Empty; // Ej. "RedeemGiftCard", "MixedPayment"
    public string? ReferenceId { get; set; } // ID del pedido o código de la gift card
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
