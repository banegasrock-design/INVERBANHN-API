using System.ComponentModel.DataAnnotations;
using Domain.Enums;

namespace Application.DTOs.Admin;

public class StoreCreateDto
{
    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required, StringLength(14, MinimumLength = 14, ErrorMessage = "El RTN debe tener exactamente 14 dígitos")]
    [RegularExpression("^[0-9]*$", ErrorMessage = "El RTN solo puede contener números")]
    public string Rtn { get; set; } = string.Empty;

    [Required]
    public InventoryMode InventoryMode { get; set; }

    [Required]
    public BillingType BillingType { get; set; }

    public bool HasIsrWithholding { get; set; }

    // SAR Configuration (Condicional si BillingType es Autoimpresor)
    [MaxLength(50)]
    public string? Cai { get; set; }

    [MaxLength(30)]
    public string? RangoInicial { get; set; }

    [MaxLength(30)]
    public string? RangoFinal { get; set; }

    public int? NumeroActual { get; set; }

    public DateTime? FechaExpiracion { get; set; }
}
