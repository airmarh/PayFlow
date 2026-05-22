using PayFlow.Application.Interfaces;
using PayFlow.Domain.Entities;
using PayFlow.Infrastructure.Data;

namespace PayFlow.Infrastructure.Repositories;

public class WebhookEventRepository : IWebhookEventRepository
{
    private readonly PayFlowDbContext _context;

    public WebhookEventRepository(PayFlowDbContext context) => _context = context;

    public async Task AddAsync(WebhookEvent webhookEvent, CancellationToken cancellationToken = default)
        => await _context.WebhookEvents.AddAsync(webhookEvent, cancellationToken);
}
