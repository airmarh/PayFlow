using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PayFlow.Infrastructure.Data;
using Testcontainers.PostgreSql;
using Xunit;

namespace PayFlow.Tests.Integration;

/// <summary>
/// Boots the full ASP.NET Core pipeline against a real PostgreSQL container spun up by
/// Testcontainers. One container is shared across all tests in a collection to keep the
/// suite fast; the database is reset between tests via <see cref="ResetDatabaseAsync"/>.
/// </summary>
public sealed class PayFlowWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("payflow_test")
        .WithUsername("test_user")
        .WithPassword("test_pass")
        .Build();

    public string ConnectionString => _postgres.GetConnectionString();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Replace the registered DbContext with one pointing at the test container
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<PayFlowDbContext>));
            if (descriptor is not null)
                services.Remove(descriptor);

            services.AddDbContext<PayFlowDbContext>(options =>
                options.UseNpgsql(_postgres.GetConnectionString()));
        });

        builder.UseSetting("Jwt:SecretKey", "integration-test-secret-key-at-least-32-chars!");
        builder.UseSetting("Jwt:Issuer",    "PayFlow");
        builder.UseSetting("Jwt:Audience",  "PayFlowUsers");
        // Disable webhook signature check in tests
        builder.UseSetting("Webhook:SecretKey", "");
    }

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        // Apply migrations so the schema is ready before any test runs
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PayFlowDbContext>();
        await db.Database.MigrateAsync();
    }

    public new async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await base.DisposeAsync();
    }

    /// <summary>Truncates all data tables between tests for isolation.</summary>
    public async Task ResetDatabaseAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PayFlowDbContext>();
        await db.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE transactions, wallets, webhook_events, users RESTART IDENTITY CASCADE");
    }
}
