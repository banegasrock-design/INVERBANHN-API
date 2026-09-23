using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Domain.Entities.Core;
using Domain.Entities.Sales;
using Domain.Entities.Logistics;
using Domain.Entities.Marketing;

namespace Infrastructure.Data.Configurations;

public class ShippingRateConfiguration : IEntityTypeConfiguration<ShippingRate>
{
    public void Configure(EntityTypeBuilder<ShippingRate> builder)
    {
        builder.HasKey(s => s.Id);

        builder.Property(s => s.OriginCity)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(s => s.DestinationCity)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(s => s.BaseRate)
            .HasColumnType("decimal(18,2)");

        // Relación con Carrier
        builder.HasOne(s => s.Carrier)
               .WithMany() // No es necesario listar las tarifas en el Carrier directamente
               .HasForeignKey(s => s.CarrierId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}

