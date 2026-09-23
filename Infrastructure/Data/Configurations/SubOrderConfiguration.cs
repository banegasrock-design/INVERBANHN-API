using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Domain.Entities.Core;
using Domain.Entities.Sales;
using Domain.Entities.Logistics;
using Domain.Entities.Marketing;

namespace Infrastructure.Data.Configurations;

public class SubOrderConfiguration : IEntityTypeConfiguration<SubOrder>
{
    public void Configure(EntityTypeBuilder<SubOrder> builder)
    {
        builder.HasKey(s => s.Id);

        builder.Property(s => s.OrderNumber)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(s => s.TotalAmount)
            .HasColumnType("decimal(18,2)");

        // Relación: Una SubOrder pertenece a un Store
        builder.HasOne(s => s.Store)
               .WithMany(st => st.SubOrders)
               .HasForeignKey(s => s.StoreId)
               .OnDelete(DeleteBehavior.Restrict);

        // Relación: Una SubOrder tiene un Carrier asignado
        builder.HasOne(s => s.Carrier)
               .WithMany(c => c.SubOrders)
               .HasForeignKey(s => s.CarrierId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}

