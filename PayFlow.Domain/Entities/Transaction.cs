using PayFlow.Domain.Enums;

namespace PayFlow.Domain.Entities;

public class Transaction
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Reference { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public string Currency { get; set; } = string.Empty;

    public TransactionStatus Status { get; set; } = TransactionStatus.Pending;

    public TransactionType Type { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation — a transaction belongs to a wallet via OwnerId (denormalised for query performance)
    public string? WalletOwnerId { get; set; }
}
