using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PayFlow.Domain.Entities;

namespace PayFlow.Infrastructure.Configurations;

public class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
{
    public void Configure(EntityTypeBuilder<Transaction> entity)
    {
        entity.ToTable("transactions");
        entity.HasKey(t => t.Id);

        entity.Property(t => t.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        entity.Property(t => t.Reference)
            .HasColumnName("reference")
            .HasMaxLength(100)
            .IsRequired();

        entity.HasIndex(t => t.Reference).IsUnique();

        entity.Property(t => t.Amount)
            .HasColumnName("amount")
            .HasColumnType("numeric(18,4)")
            .IsRequired();

        entity.Property(t => t.Currency)
            .HasColumnName("currency")
            .HasMaxLength(3)
            .IsRequired();

        entity.Property(t => t.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        entity.Property(t => t.Type)
            .HasColumnName("type")
            .HasConversion<string>()
            .HasMaxLength(10)
            .IsRequired();

        entity.Property(t => t.WalletOwnerId)
            .HasColumnName("wallet_owner_id")
            .HasMaxLength(200);

        entity.Property(t => t.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        entity.Property(t => t.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        entity.HasIndex(t => t.Status);
        entity.HasIndex(t => t.WalletOwnerId);
        entity.HasIndex(t => t.CreatedAt);
    }
}
