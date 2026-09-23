using System;

namespace Domain.Entities.Logistics;

public class SubOrder
{
    public Guid Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    
    // Cliente asociado a la orden
    public int CustomerId { get; set; }
    
    // Relación con Store
    public int StoreId { get; set; }
    public Store Store { get; set; } = null!;

    // Relación con Carrier
    public int CarrierId { get; set; }
    public Carrier Carrier { get; set; } = null!;

    // Seguimiento y Estado
    public string Status { get; set; } = "Pendiente"; // "Pendiente", "EnTránsito", "Entregado"
    public string? TrackingNumber { get; set; }

    // Pago Contra Entrega (COD)
    public bool RequiresCarrierCollection { get; set; }
    public decimal AmountToCollect { get; set; }
}
