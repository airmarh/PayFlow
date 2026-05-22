namespace PayFlow.Domain.Entities;

public class WebhookEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string TransactionReference { get; set; } = string.Empty;

    public string Payload { get; set; } = string.Empty;

    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;

    public bool Processed { get; set; }
}
