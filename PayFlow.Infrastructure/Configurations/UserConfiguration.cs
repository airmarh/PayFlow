using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PayFlow.Domain.Entities;

namespace PayFlow.Infrastructure.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> entity)
    {
        entity.ToTable("users");
        entity.HasKey(u => u.Id);

        entity.Property(u => u.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        entity.Property(u => u.Email)
            .HasColumnName("email")
            .HasMaxLength(256)
            .IsRequired();

        entity.HasIndex(u => u.Email).IsUnique();

        entity.Property(u => u.PasswordHash)
            .HasColumnName("password_hash")
            .IsRequired();

        entity.Property(u => u.FirstName)
            .HasColumnName("first_name")
            .HasMaxLength(100)
            .IsRequired();

        entity.Property(u => u.LastName)
            .HasColumnName("last_name")
            .HasMaxLength(100)
            .IsRequired();

        entity.Property(u => u.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        entity.Property(u => u.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();
    }
}
