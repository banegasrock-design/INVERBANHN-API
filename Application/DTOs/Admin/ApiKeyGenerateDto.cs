using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.Admin;

public class ApiKeyGenerateDto
{
    [Required, MaxLength(100)]
    public string ClientName { get; set; } = string.Empty;

    [Required, MaxLength(50)]
    public string Role { get; set; } = string.Empty;
}
