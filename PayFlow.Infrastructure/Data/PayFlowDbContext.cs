using Microsoft.EntityFrameworkCore;
using PayFlow.Domain.Entities;

namespace PayFlow.Infrastructure.Data;

public class PayFlowDbContext : DbContext
{
    public PayFlowDbContext(DbContextOptions<PayFlowDbContext> options) : base(options) { }

    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<Wallet> Wallets => Set<Wallet>();
    public DbSet<WebhookEvent> WebhookEvents => Set<WebhookEvent>();
    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PayFlowDbContext).Assembly);
    }
}
