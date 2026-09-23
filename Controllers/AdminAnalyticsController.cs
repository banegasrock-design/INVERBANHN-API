using Application.Interfaces;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Application.DTOs.Analytics;

namespace INVERBANHN.Controllers;

/// <summary>
/// Controlador de Analítica para el Administrador.
/// </summary>
[ApiController]
[Route("api/admin/analytics")]
public class AdminAnalyticsController : ControllerBase
{
    private readonly IAnalyticsService _analyticsService;

    public AdminAnalyticsController(IAnalyticsService analyticsService)
    {
        _analyticsService = analyticsService;
    }

    /// <summary>
    /// Obtiene el pasivo financiero actual por Gift Cards pendientes de canje.
    /// Consume la vista [Marketing].[v_Auditoria_GiftCards_Pendientes].
    /// </summary>
    /// <returns>Resumen del pasivo en LPS.</returns>
    [HttpGet("gift-cards/liability")]
    [ProducesResponseType(typeof(GiftCardLiabilityDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetGiftCardLiability()
    {
        var result = await _analyticsService.GetGiftCardLiabilityAsync();
        return result.IsSuccess ? Ok(result.Data) : BadRequest(new { Error = result.Error });
    }

    /// <summary>
    /// Obtiene un resumen de las facturas de tiendas informales pendientes de carga al ecommerce.
    /// Consume la vista [Sales].[v_Facturas_Pendientes_Carga].
    /// </summary>
    /// <returns>Cantidad y monto total pendiente.</returns>
    [HttpGet("billing/pending-upload")]
    [ProducesResponseType(typeof(PendingInvoicesSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetPendingInvoicesSummary()
    {
        var result = await _analyticsService.GetPendingInvoicesSummaryAsync();
        return result.IsSuccess ? Ok(result.Data) : BadRequest(new { Error = result.Error });
    }

    /// <summary>
    /// Obtiene estadísticas de alcance del canal de Marketing (usuarios activos vs opt-outs).
    /// </summary>
    /// <returns>Estadísticas de alcance y porcentaje de efectividad.</returns>
    [HttpGet("notifications/marketing-reach")]
    [ProducesResponseType(typeof(MarketingReachDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetMarketingReachStats()
    {
        var result = await _analyticsService.GetMarketingReachStatsAsync();
        return result.IsSuccess ? Ok(result.Data) : BadRequest(new { Error = result.Error });
    }
}
