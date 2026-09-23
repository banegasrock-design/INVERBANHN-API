namespace Application.DTOs.Admin;

public class AuditLogDto
{
    public int Id { get; set; }
    public DateTime Fecha { get; set; }
    public string Tabla { get; set; } = string.Empty;
    public string Operacion { get; set; } = string.Empty;
    public string AdminId { get; set; } = string.Empty;
    
    // JSON strings
    public string? EstadoAnterior { get; set; }
    public string? EstadoNuevo { get; set; }
}
