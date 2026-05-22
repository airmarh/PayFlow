using PayFlow.Domain.Enums;

namespace PayFlow.Application.DTOs;

public class WebhookPayload
{
    public string TransactionReference { get; set; } = string.Empty;

    public TransactionStatus Status { get; set; }

    public string RawPayload { get; set; } = string.Empty;

    public DateTime EventTimestamp { get; set; } = DateTime.UtcNow;
}
