using System.Collections.Generic;

namespace Domain.Entities.Logistics;

public class Carrier
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    
    // Navegación
    public ICollection<SubOrder> SubOrders { get; set; } = new List<SubOrder>();
}
