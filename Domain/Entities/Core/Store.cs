using System.Collections.Generic;
using Domain.Enums;

namespace Domain.Entities.Core;

public class Store
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public InventoryMode InventoryMode { get; set; }
    public BillingType BillingType { get; set; }
    
    public bool HasIsrWithholding { get; set; } // Retención de ISR Automática

    // Navegación
    public ICollection<SubOrder> SubOrders { get; set; } = new List<SubOrder>();
}
