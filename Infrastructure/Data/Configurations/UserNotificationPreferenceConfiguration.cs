using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Domain.Entities.Core;
using Domain.Entities.Sales;
using Domain.Entities.Logistics;
using Domain.Entities.Marketing;

namespace Infrastructure.Data.Configurations;

public class UserNotificationPreferenceConfiguration : IEntityTypeConfiguration<UserNotificationPreference>
{
    public void Configure(EntityTypeBuilder<UserNotificationPreference> builder)
    {
        builder.HasKey(u => u.Id);

        builder.ToTable("User_Notification_Preferences"); // Mapear explícitamente a la tabla solicitada

        builder.Property(u => u.Topic)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(u => u.Channel)
            .IsRequired()
            .HasMaxLength(50);
            
        // Podemos añadir un índice único para evitar duplicados
        builder.HasIndex(u => new { u.CustomerId, u.Topic, u.Channel }).IsUnique();
    }
}

