using PayFlow.Application.DTOs;

namespace PayFlow.Application.Interfaces;

public interface IWebhookService
{
    /// <summary>
    /// Processes an inbound webhook notification from a payment provider.
    /// Implements idempotency — duplicate events for the same reference and status are silently ignored.
    /// </summary>
    Task ProcessWebhookAsync(WebhookPayload payload, CancellationToken cancellationToken = default);
}
