using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.Order;

public class CancelOrderRequest
{
    [Required]
    public string OrderId { get; set; } = string.Empty;

    [Required]
    [StringLength(500)]
    public string Reason { get; set; } = string.Empty;
}
