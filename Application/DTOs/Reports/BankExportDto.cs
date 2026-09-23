namespace Application.DTOs.Reports;

public class BankExportDto
{
    /// <summary>
    /// Nombre del banco destino.
    /// </summary>
    /// <example>BAC Honduras</example>
    public string Banco { get; set; } = string.Empty;

    /// <summary>
    /// Número de cuenta destino del beneficiario.
    /// </summary>
    /// <example>200-01-012-123456-7</example>
    public string Cuenta_Destino { get; set; } = string.Empty;

    /// <summary>
    /// Tipo de cuenta bancaria (Ahorro, Corriente, etc.).
    /// </summary>
    /// <example>Ahorro</example>
    public string Tipo_Cuenta { get; set; } = string.Empty;

    /// <summary>
    /// Nombre completo del beneficiario del pago.
    /// </summary>
    /// <example>Juan Carlos Pérez López</example>
    public string Beneficiario { get; set; } = string.Empty;

    /// <summary>
    /// Monto a transferir en Lempiras (LPS). Precisión: 2 decimales.
    /// </summary>
    /// <example>15750.00</example>
    public decimal Monto_A_Transferir { get; set; }

    /// <summary>
    /// Referencia interna del sistema para trazabilidad.
    /// </summary>
    /// <example>LIQ-2024-0542</example>
    public string Referencia_Interna { get; set; } = string.Empty;

    /// <summary>
    /// RTN del vendedor (14 dígitos numéricos, formato SAR Honduras).
    /// </summary>
    /// <example>08019000123456</example>
    public string RTN_Vendedor { get; set; } = string.Empty;
}
