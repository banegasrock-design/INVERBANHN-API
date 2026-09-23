using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Application.Common;
using Application.Interfaces;
using Domain.Enums;
using Infrastructure.Data.Contexts;
using Infrastructure.Hubs;

namespace Application.Services;

public class DispatchService : IDispatchService
{
    private readonly MarketplaceDbContext _context;
    private readonly IHubContext<NotificationHub> _hubContext;

    // Bodega central de San Pedro Sula (configurable a futuro)
    private const string CentralWarehouseName = "Bodega Central SPS";

    public DispatchService(MarketplaceDbContext context, IHubContext<NotificationHub> hubContext)
    {
        _context = context;
        _hubContext = hubContext;
    }

    public async Task<Result<string>> ProcessDispatchAsync(Guid subOrderId)
    {
        // 1. Cargar la orden con su tienda
        var order = await _context.SubOrders
            .Include(o => o.Store)
            .FirstOrDefaultAsync(o => o.Id == subOrderId);

        if (order == null)
            return Result<string>.Failure("La orden especificada no existe.");

        if (order.Store == null)
            return Result<string>.Failure("La orden no tiene una tienda asociada.");

        // 2. Evaluar el Inventory_Mode de la tienda
        switch (order.Store.InventoryMode)
        {
            case InventoryMode.Ecommerce:
                return await AssignToWarehouseQueueAsync(order);

            case InventoryMode.Tienda:
                return await NotifyStoreAsync(order);

            default:
                return Result<string>.Failure($"Inventory_Mode desconocido: {order.Store.InventoryMode}");
        }
    }

    /// <summary>
    /// Ecommerce: Asigna la orden al WarehouseQueue de la bodega central de SPS.
    /// </summary>
    private async Task<Result<string>> AssignToWarehouseQueueAsync(Domain.Entities.Logistics.SubOrder order)
    {
        order.Status = "AsignadaBodega";

        // Registrar en la base de datos qué bodega se encargará
        // (En una fase futura se puede buscar dinámicamente la bodega más cercana)
        await _context.SaveChangesAsync();

        // Notificar al grupo del personal de bodega vía SignalR
        await _hubContext.Clients.Group("Warehouse_SPS").SendAsync("NewOrderAssigned", new
        {
            OrderId = order.Id,
            OrderNumber = order.OrderNumber,
            StoreName = order.Store.Name,
            TotalAmount = order.TotalAmount,
            Warehouse = CentralWarehouseName,
            AssignedAt = DateTime.UtcNow
        });

        return Result<string>.Success($"Orden {order.OrderNumber} asignada a {CentralWarehouseName}.");
    }

    /// <summary>
    /// Tienda: Notifica al panel específico de la tienda vía SignalR.
    /// </summary>
    private async Task<Result<string>> NotifyStoreAsync(Domain.Entities.Logistics.SubOrder order)
    {
        order.Status = "PendientePreparacion";
        await _context.SaveChangesAsync();

        // Enviar notificación al grupo exclusivo de la tienda
        await _hubContext.Clients.Group($"Store_{order.StoreId}").SendAsync("PrepareOrder", new
        {
            OrderId = order.Id,
            OrderNumber = order.OrderNumber,
            TotalAmount = order.TotalAmount,
            Message = $"Nueva orden #{order.OrderNumber} por preparar. Monto: L. {order.TotalAmount:N2}",
            ReceivedAt = DateTime.UtcNow
        });

        return Result<string>.Success($"Orden {order.OrderNumber} notificada a la tienda {order.Store.Name}.");
    }
}
