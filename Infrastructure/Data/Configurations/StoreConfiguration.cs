using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Domain.Entities.Core;
using Domain.Entities.Sales;
using Domain.Entities.Logistics;
using Domain.Entities.Marketing;

namespace Infrastructure.Data.Configurations;

public class StoreConfiguration : IEntityTypeConfiguration<Store>
{
    public void Configure(EntityTypeBuilder<Store> builder)
    {
        builder.ToTable("Stores", "Core");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("Store_ID");

        builder.Property(s => s.Name)
            .IsRequired()
            .HasMaxLength(100)
            .HasColumnName("Store_Name");

        builder.Property(s => s.InventoryMode)
            .IsRequired()
            .HasConversion<string>()
            .HasColumnName("Inventory_Mode");

        builder.Property(s => s.BillingType)
            .IsRequired()
            .HasConversion<string>()
            .HasColumnName("Billing_Type");

        builder.Property(s => s.HasIsrWithholding)
            .HasDefaultValue(false);
    }
}

