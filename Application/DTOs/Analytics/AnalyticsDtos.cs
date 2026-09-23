namespace Application.DTOs.Analytics;

public class GiftCardLiabilityDto
{
    /// <summary>
    /// Cantidad total de Gift Cards que aún tienen saldo pendiente.
    /// </summary>
    public int TotalPendingCards { get; set; }

    /// <summary>
    /// Monto total del pasivo financiero en Lempiras (LPS).
    /// </summary>
    /// <example>250000.50</example>
    public decimal TotalLiabilityLps { get; set; }
}

public class PendingInvoicesSummaryDto
{
    /// <summary>
    /// Cantidad de facturas de tiendas informales que faltan por subir.
    /// </summary>
    public int PendingCount { get; set; }

    /// <summary>
    /// Monto total de las facturas pendientes de carga (LPS).
    /// </summary>
    public decimal TotalAmountLps { get; set; }
}

public class MarketingReachDto
{
    /// <summary>
    /// Total de usuarios registrados en la plataforma.
    /// </summary>
    public int TotalUsers { get; set; }

    /// <summary>
    /// Usuarios que han desactivado el canal de Marketing.
    /// </summary>
    public int OptOutCount { get; set; }

    /// <summary>
    /// Usuarios que mantienen el canal de Marketing activo.
    /// </summary>
    public int ReachCount => TotalUsers - OptOutCount;

    /// <summary>
    /// Porcentaje de alcance efectivo de las campañas.
    /// </summary>
    /// <example>85.5</example>
    public double ReachPercentage => TotalUsers > 0 ? (double)ReachCount / TotalUsers * 100 : 0;
}
