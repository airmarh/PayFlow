namespace PayFlow.Domain.Entities;

public class Wallet
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string OwnerId { get; set; } = string.Empty;

    public decimal Balance { get; set; }

    public string Currency { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
