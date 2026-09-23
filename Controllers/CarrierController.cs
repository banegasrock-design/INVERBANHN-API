using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Application.Interfaces;
using Application.DTOs.Carrier;
using Infrastructure.Data.Contexts;
using Microsoft.AspNetCore.Authorization;

namespace INVERBANHN.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CarrierController : ControllerBase
{
    private readonly ICarrierIntegrationFactory _carrierFactory;
    private readonly MarketplaceDbContext _context;
    private readonly INotificationService _notificationService;

    public CarrierController(
        ICarrierIntegrationFactory carrierFactory, 
        MarketplaceDbContext context,
        INotificationService notificationService)
    {
        _carrierFactory = carrierFactory;
        _context = context;
        _notificationService = notificationService;
    }

    [HttpPost("quote")]
    public async Task<IActionResult> GetQuote([FromBody] QuoteRequestDto request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var carrierService = _carrierFactory.GetCarrierIntegration(request.CarrierId);
            var quote = await carrierService.GetQuoteAsync(request.OriginCity, request.DestinationCity);
            
            return Ok(new { CarrierId = request.CarrierId, Cost = quote });
        }
        catch (Exception ex)
        {
            return BadRequest(new { Error = ex.Message });
        }
    }

    [HttpPost("generate-guide/{subOrderId}")]
    public async Task<IActionResult> GenerateGuide(Guid subOrderId)
    {
        var order = await _context.SubOrders.FindAsync(subOrderId);
        if (order == null)
            return NotFound(new { Error = "La orden no existe." });

        try
        {
            var carrierService = _carrierFactory.GetCarrierIntegration(order.CarrierId);
            var guide = await carrierService.GenerateShippingGuideAsync(order);
            
            // Aquí podríamos actualizar la BD para guardar el número de guía (ejemplo simplificado)
            order.Status = "EnTránsito";
            await _context.SaveChangesAsync();

            return Ok(new { Message = "Guía generada exitosamente.", GuideDetails = guide });
        }
        catch (Exception ex)
        {
            return BadRequest(new { Error = ex.Message });
        }
    }

    // El webhook usa [Authorize] porque el usuario indicó que quiere usar ApiKey authentication por ahora
    [HttpPost("webhook")]
    [Authorize]
    public async Task<IActionResult> Webhook([FromBody] WebhookPayloadDto payload)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        // Buscar la orden por TrackingNumber (asumiendo que TrackingNumber único)
        var order = await _context.SubOrders
            .FirstOrDefaultAsync(o => o.TrackingNumber == payload.TrackingNumber);

        if (order == null)
        {
            // A veces las paqueteras envían webhooks de guías que no son nuestras
            return Ok(new { Message = "TrackingNumber ignorado, no pertenece a este sistema." });
        }

        // Actualizamos el estado siempre que nos notifiquen algo válido
        var validStatuses = new[] { "Entregado", "Recibido en Bodega", "En camino" };
        
        if (Array.Exists(validStatuses, s => s.Equals(payload.NewStatus, StringComparison.OrdinalIgnoreCase)))
        {
            order.Status = payload.NewStatus;
            await _context.SaveChangesAsync();

            // Si es Recibido en Bodega o En camino, disparamos la notificación InApp
            if (payload.NewStatus.Equals("Recibido en Bodega", StringComparison.OrdinalIgnoreCase) || 
                payload.NewStatus.Equals("En camino", StringComparison.OrdinalIgnoreCase))
            {
                await _notificationService.NotifyOrderStatusChangedAsync(
                    order.CustomerId, 
                    order.OrderNumber, 
                    payload.NewStatus);
            }
        }

        return Ok(new { Message = "Webhook procesado exitosamente." });
    }
}
