using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.Wallet;

public class RedeemGiftCardRequest
{
    /// <summary>
    /// Código alfanumérico único de la Gift Card (entre 8 y 20 caracteres).
    /// </summary>
    /// <example>GC-2024-ABCD</example>
    [Required]
    public string Code { get; set; } = string.Empty;
}
