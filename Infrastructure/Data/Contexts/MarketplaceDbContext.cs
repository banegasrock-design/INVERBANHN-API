using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Data.Contexts;

public class MarketplaceDbContext : DbContext
{
    public MarketplaceDbContext()
    {
    }

    public MarketplaceDbContext(DbContextOptions<MarketplaceDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Domain.Entities.Logistics.Carrier> Carriers { get; set; }
    public virtual DbSet<Domain.Entities.Core.Store> Stores { get; set; }
    public virtual DbSet<Domain.Entities.Logistics.SubOrder> SubOrders { get; set; }
    public virtual DbSet<Domain.Entities.Sales.CustomerWallet> CustomerWallets { get; set; }
    public virtual DbSet<Domain.Entities.Marketing.GiftCard> GiftCards { get; set; }
    public virtual DbSet<Domain.Entities.Sales.WalletTransaction> WalletTransactions { get; set; }
    public virtual DbSet<Domain.Entities.Logistics.ShippingRate> ShippingRates { get; set; }
    public virtual DbSet<Domain.Entities.Core.UserNotificationPreference> UserNotificationPreferences { get; set; }
    public virtual DbSet<Domain.Entities.Core.User> Users { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            optionsBuilder.UseSqlServer("Server=tcp:inverban.database.windows.net;Authentication=Active Directory Interactive;Database=INVERBANHN;TrustServerCertificate=True;MultipleActiveResultSets=true;");
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new Infrastructure.Data.Configurations.CarrierConfiguration());
        modelBuilder.ApplyConfiguration(new Infrastructure.Data.Configurations.StoreConfiguration());
        modelBuilder.ApplyConfiguration(new Infrastructure.Data.Configurations.SubOrderConfiguration());
        modelBuilder.ApplyConfiguration(new Infrastructure.Data.Configurations.CustomerWalletConfiguration());
        modelBuilder.ApplyConfiguration(new Infrastructure.Data.Configurations.GiftCardConfiguration());
        modelBuilder.ApplyConfiguration(new Infrastructure.Data.Configurations.WalletTransactionConfiguration());
        modelBuilder.ApplyConfiguration(new Infrastructure.Data.Configurations.ShippingRateConfiguration());
        modelBuilder.ApplyConfiguration(new Infrastructure.Data.Configurations.UserNotificationPreferenceConfiguration());
        modelBuilder.ApplyConfiguration(new Infrastructure.Data.Configurations.UserConfiguration());
    }
}
