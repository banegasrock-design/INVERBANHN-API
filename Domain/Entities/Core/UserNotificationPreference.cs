namespace Domain.Entities.Core;

public class UserNotificationPreference
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public string Topic { get; set; } = string.Empty; // Ej: "Logística"
    public string Channel { get; set; } = string.Empty; // Ej: "InApp"
    public bool IsEnabled { get; set; }
}
