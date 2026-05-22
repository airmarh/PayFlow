using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PayFlow.Domain.Entities;

namespace PayFlow.Infrastructure.Configurations;

public class WebhookEventConfiguration : IEntityTypeConfiguration<WebhookEvent>
{
    public void Configure(EntityTypeBuilder<WebhookEvent> entity)
    {
        entity.ToTable("webhook_events");
        entity.HasKey(e => e.Id);

        entity.Property(e => e.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        entity.Property(e => e.TransactionReference)
            .HasColumnName("transaction_reference")
            .HasMaxLength(100)
            .IsRequired();

        entity.Property(e => e.Payload)
            .HasColumnName("payload")
            .HasColumnType("jsonb")
            .IsRequired();

        entity.Property(e => e.ReceivedAt)
            .HasColumnName("received_at")
            .IsRequired();

        entity.Property(e => e.Processed)
            .HasColumnName("processed")
            .IsRequired();

        entity.HasIndex(e => e.TransactionReference);
        entity.HasIndex(e => e.Processed);
    }
}
