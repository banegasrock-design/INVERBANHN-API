namespace Domain.Entities.Sales;

public class CustomerWallet
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public decimal Balance { get; set; }
    
    // Concurrencia Optimista
    public byte[] RowVersion { get; set; } = null!;
}
