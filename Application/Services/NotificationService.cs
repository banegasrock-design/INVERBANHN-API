using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Application.Interfaces;
using Infrastructure.Hubs;
using Infrastructure.Data.Contexts;

namespace Application.Services;

public class NotificationService : INotificationService
{
    private readonly MarketplaceDbContext _context;
    private readonly IHubContext<NotificationHub> _hubContext;

    public NotificationService(MarketplaceDbContext context, IHubContext<NotificationHub> hubContext)
    {
        _context = context;
        _hubContext = hubContext;
    }

    public async Task NotifyOrderStatusChangedAsync(int customerId, string orderNumber, string newStatus)
    {
        // 1. Verificar el PreferenceCenter
        var preference = await _context.UserNotificationPreferences
            .FirstOrDefaultAsync(p => p.CustomerId == customerId 
                                      && p.Topic == "Logística" 
                                      && p.Channel == "InApp");

        // Si la preferencia existe y está habilitada, procedemos a notificar
        if (preference != null && preference.IsEnabled)
        {
            string message = $"Tu pedido {orderNumber} ha cambiado al estado: {newStatus}.";

            // 2. Enviar mensaje InApp vía SignalR
            await _hubContext.Clients.Group($"Customer_{customerId}").SendAsync("ReceiveNotification", message);
        }
    }

    public async Task NotifyPaymentConfirmedAsync(int customerId, string orderNumber)
    {
        // 1. Verificar el PreferenceCenter (Opcional, pero siguiendo el patrón existente)
        var preference = await _context.UserNotificationPreferences
            .FirstOrDefaultAsync(p => p.CustomerId == customerId 
                                      && p.Topic == "Ventas" 
                                      && p.Channel == "InApp");

        // Si la preferencia no existe, podríamos habilitarla por defecto o simplemente notificar
        if (preference == null || preference.IsEnabled)
        {
            string message = "¡Pago procesado! Tu factura ha sido generada.";

            // 2. Enviar mensaje InApp vía SignalR
            await _hubContext.Clients.Group($"Customer_{customerId}").SendAsync("ReceiveNotification", message);
        }
    }

    public async Task NotifyWalletBalanceUpdatedAsync(int customerId, decimal newBalance)
    {
        // Enviar mensaje de actualización de saldo vía SignalR
        // El cliente React escuchará "UpdateWalletBalance" y actualizará el header
        await _hubContext.Clients.Group($"Customer_{customerId}").SendAsync("UpdateWalletBalance", newBalance);
    }
}
