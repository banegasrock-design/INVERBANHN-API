using System;

namespace Domain.Entities.Marketing;

public class GiftCard
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public bool IsRedeemed { get; set; }
    public DateTime? RedeemedAt { get; set; }
    public int? RedeemedByWalletId { get; set; }
}
