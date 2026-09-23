namespace Application.DTOs.Admin;

public class ApiKeyResultDto
{
    public int Id { get; set; }
    public string PlainTextKey { get; set; } = string.Empty;
    public string ClientName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public bool IsActive { get; set; }
}
