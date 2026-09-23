namespace Domain.Entities.Logistics;

public class ShippingRate
{
    public int Id { get; set; }
    public string OriginCity { get; set; } = string.Empty;
    public string DestinationCity { get; set; } = string.Empty;
    public decimal BaseRate { get; set; }
    
    // Relación con Carrier
    public int CarrierId { get; set; }
    public Carrier Carrier { get; set; } = null!;
}
