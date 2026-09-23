using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;

namespace Infrastructure.Hubs;

public class NotificationHub : Hub
{
    // Grupo de cliente (para notificaciones de pedido al comprador)
    public async Task JoinCustomerGroup(int customerId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"Customer_{customerId}");
    }

    public async Task LeaveCustomerGroup(int customerId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"Customer_{customerId}");
    }

    // Grupo de tienda (para notificaciones de despacho al panel de la tienda)
    public async Task JoinStoreGroup(int storeId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"Store_{storeId}");
    }

    public async Task LeaveStoreGroup(int storeId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"Store_{storeId}");
    }

    // Grupo de bodega (para el personal de bodega central SPS)
    public async Task JoinWarehouseGroup(string warehouseCode)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"Warehouse_{warehouseCode}");
    }

    public async Task LeaveWarehouseGroup(string warehouseCode)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"Warehouse_{warehouseCode}");
    }
}
