using Domain.Entities.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users", "Core");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("User_ID");

        builder.Property(e => e.FullName)
            .IsRequired()
            .HasMaxLength(200)
            .HasColumnName("Full_Name");

        builder.Property(e => e.Email)
            .IsRequired()
            .HasMaxLength(200)
            .HasColumnName("Email");

        builder.Property(e => e.PasswordHash)
            .IsRequired()
            .HasColumnName("Password_Hash");

        builder.Property(e => e.RoleName)
            .IsRequired()
            .HasMaxLength(50)
            .HasDefaultValue("Customer")
            .HasColumnName("Role_Name");

        builder.Property(e => e.IsActive)
            .HasDefaultValue(true)
            .HasColumnName("Is_Active");

        builder.Property(e => e.CreatedAt)
            .HasColumnName("Created_At");

        builder.HasIndex(e => e.Email).IsUnique();
    }
}
