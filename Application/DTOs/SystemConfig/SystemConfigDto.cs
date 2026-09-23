using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.SystemConfig;

public class SystemConfigDto
{
    [Required]
    [Range(0, 100, ErrorMessage = "La tasa de impuesto debe estar entre 0 y 100.")]
    public decimal TaxRate { get; set; }

    [Required]
    public bool MaintenanceMode { get; set; }

    [Required]
    [EmailAddress]
    public string SupportEmail { get; set; } = string.Empty;

    [Required]
    public string DefaultCurrency { get; set; } = "HNL";
}
