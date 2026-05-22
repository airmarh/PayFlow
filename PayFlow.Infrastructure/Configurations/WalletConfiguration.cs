using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PayFlow.Domain.Entities;

namespace PayFlow.Infrastructure.Configurations;

public class WalletConfiguration : IEntityTypeConfiguration<Wallet>
{
    public void Configure(EntityTypeBuilder<Wallet> entity)
    {
        entity.ToTable("wallets");
        entity.HasKey(w => w.Id);

        entity.Property(w => w.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        entity.Property(w => w.OwnerId)
            .HasColumnName("owner_id")
            .HasMaxLength(200)
            .IsRequired();

        entity.HasIndex(w => w.OwnerId).IsUnique();

        entity.Property(w => w.Balance)
            .HasColumnName("balance")
            .HasColumnType("numeric(18,4)")
            .IsRequired();

        entity.Property(w => w.Currency)
            .HasColumnName("currency")
            .HasMaxLength(3)
            .IsRequired();

        entity.Property(w => w.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        entity.Property(w => w.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        // Optimistic concurrency — map PostgreSQL's built-in xmin system column as a
        // shadow concurrency token. xmin increments on every row update, so EF Core
        // detects racing writes without needing a separate version column.
        entity.Property<uint>("xmin")
            .HasColumnName("xmin")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();
    }
}
