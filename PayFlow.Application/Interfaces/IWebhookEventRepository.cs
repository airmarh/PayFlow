using PayFlow.Domain.Entities;

namespace PayFlow.Application.Interfaces;

public interface IWebhookEventRepository
{
    Task AddAsync(WebhookEvent webhookEvent, CancellationToken cancellationToken = default);
}
