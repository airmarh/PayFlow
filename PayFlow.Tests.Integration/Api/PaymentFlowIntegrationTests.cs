using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using PayFlow.Application.DTOs;
using PayFlow.Domain.Enums;
using Xunit;

namespace PayFlow.Tests.Integration.Api;

/// <summary>
/// End-to-end tests covering the complete payment lifecycle:
/// register → login → create wallet → initiate payment → receive webhook → verify balance.
/// Each test resets the database so tests are fully isolated.
/// </summary>
[Collection("Integration")]
public sealed class PaymentFlowIntegrationTests : IAsyncLifetime
{
    private readonly PayFlowWebApplicationFactory _factory;
    private readonly HttpClient _client;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public PaymentFlowIntegrationTests(PayFlowWebApplicationFactory factory)
    {
        _factory = factory;
        _client  = factory.CreateClient();
    }

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();
    public Task DisposeAsync()    => Task.CompletedTask;

    // ── helpers ───────────────────────────────────────────────────────────────

    private async Task<string> RegisterAndLoginAsync(
        string email    = "test@payflow.io",
        string password = "Test1234!")
    {
        await _client.PostAsJsonAsync("/api/auth/register", new
        {
            firstName = "Test",
            lastName  = "User",
            email,
            password
        });

        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new { email, password });
        var loginBody     = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>(JsonOpts);
        return loginBody!.Token;
    }

    private void Authorize(string token) =>
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    // ── tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Register_WithValidData_Returns201()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            firstName = "Jane",
            lastName  = "Doe",
            email     = "jane@example.com",
            password  = "Secure1234!"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<AuthResponse>(JsonOpts);
        body!.Token.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Register_DuplicateEmail_Returns409()
    {
        var payload = new { firstName = "A", lastName = "B", email = "dup@example.com", password = "Test1234!" };
        await _client.PostAsJsonAsync("/api/auth/register", payload);
        var second = await _client.PostAsJsonAsync("/api/auth/register", payload);

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task CreateWallet_WithValidRequest_Returns201WithZeroBalance()
    {
        var token = await RegisterAndLoginAsync();
        Authorize(token);

        var response = await _client.PostAsJsonAsync("/api/wallets", new
        {
            ownerId  = "owner-001",
            currency = "NGN"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var wallet = await response.Content.ReadFromJsonAsync<CreateWalletResponse>(JsonOpts);
        wallet!.Balance.Should().Be(0m);
        wallet.Currency.Should().Be("NGN");
    }

    [Fact]
    public async Task CreateWallet_DuplicateOwner_Returns409()
    {
        var token = await RegisterAndLoginAsync();
        Authorize(token);

        var payload = new { ownerId = "owner-dup", currency = "USD" };
        await _client.PostAsJsonAsync("/api/wallets", payload);
        var second = await _client.PostAsJsonAsync("/api/wallets", payload);

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task InitiatePayment_CurrencyMismatch_Returns400()
    {
        var token = await RegisterAndLoginAsync();
        Authorize(token);

        await _client.PostAsJsonAsync("/api/wallets", new { ownerId = "owner-cm", currency = "NGN" });

        var response = await _client.PostAsJsonAsync("/api/transactions", new
        {
            reference     = "TXN-MISMATCH-001",
            amount        = 100m,
            currency      = "USD",      // wallet is NGN
            type          = "Credit",
            walletOwnerId = "owner-cm"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task FullPaymentFlow_CreditWebhook_UpdatesWalletBalance()
    {
        // Arrange
        var token = await RegisterAndLoginAsync();
        Authorize(token);

        const string ownerId    = "owner-flow-001";
        const string reference  = "TXN-FLOW-001";

        await _client.PostAsJsonAsync("/api/wallets", new { ownerId, currency = "NGN" });

        var initiateResponse = await _client.PostAsJsonAsync("/api/transactions", new
        {
            reference,
            amount        = 5000m,
            currency      = "NGN",
            type          = "Credit",
            walletOwnerId = ownerId
        });
        initiateResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        // Act — simulate payment provider webhook
        var webhookResponse = await _client.PostAsJsonAsync("/api/webhooks/notify", new
        {
            transactionReference = reference,
            status               = "Successful",
            rawPayload           = "{\"event\":\"payment.success\"}",
            eventTimestamp       = DateTime.UtcNow
        });
        webhookResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert — wallet balance should be 5000
        var walletResponse = await _client.GetAsync($"/api/wallets/{ownerId}");
        walletResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var wallet = await walletResponse.Content.ReadFromJsonAsync<CreateWalletResponse>(JsonOpts);
        wallet!.Balance.Should().Be(5000m);

        // Assert — transaction status should be Successful
        var txResponse = await _client.GetAsync($"/api/transactions/{reference}");
        txResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var tx = await txResponse.Content.ReadFromJsonAsync<TransactionStatusResponse>(JsonOpts);
        tx!.Status.Should().Be(TransactionStatus.Successful);
    }

    [Fact]
    public async Task FullPaymentFlow_FailedWebhook_DoesNotCreditWallet()
    {
        var token = await RegisterAndLoginAsync();
        Authorize(token);

        const string ownerId   = "owner-fail-001";
        const string reference = "TXN-FAIL-001";

        await _client.PostAsJsonAsync("/api/wallets", new { ownerId, currency = "NGN" });
        await _client.PostAsJsonAsync("/api/transactions", new
        {
            reference,
            amount        = 1000m,
            currency      = "NGN",
            type          = "Credit",
            walletOwnerId = ownerId
        });

        var webhookResponse = await _client.PostAsJsonAsync("/api/webhooks/notify", new
        {
            transactionReference = reference,
            status               = "Failed",
            rawPayload           = "{\"event\":\"payment.failed\"}",
            eventTimestamp       = DateTime.UtcNow
        });
        webhookResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var walletResponse = await _client.GetAsync($"/api/wallets/{ownerId}");
        var wallet = await walletResponse.Content.ReadFromJsonAsync<CreateWalletResponse>(JsonOpts);
        wallet!.Balance.Should().Be(0m);
    }

    [Fact]
    public async Task Webhook_DuplicateEvent_IsIdempotent()
    {
        var token = await RegisterAndLoginAsync();
        Authorize(token);

        const string ownerId   = "owner-idem-001";
        const string reference = "TXN-IDEM-001";

        await _client.PostAsJsonAsync("/api/wallets", new { ownerId, currency = "NGN" });
        await _client.PostAsJsonAsync("/api/transactions", new
        {
            reference,
            amount        = 500m,
            currency      = "NGN",
            type          = "Credit",
            walletOwnerId = ownerId
        });

        var webhookPayload = new
        {
            transactionReference = reference,
            status               = "Successful",
            rawPayload           = "{}",
            eventTimestamp       = DateTime.UtcNow
        };

        // Send the same successful webhook twice
        await _client.PostAsJsonAsync("/api/webhooks/notify", webhookPayload);
        var second = await _client.PostAsJsonAsync("/api/webhooks/notify", webhookPayload);
        second.StatusCode.Should().Be(HttpStatusCode.OK);

        // Balance should only be credited once
        var walletResponse = await _client.GetAsync($"/api/wallets/{ownerId}");
        var wallet = await walletResponse.Content.ReadFromJsonAsync<CreateWalletResponse>(JsonOpts);
        wallet!.Balance.Should().Be(500m);
    }

    [Fact]
    public async Task GetTransactions_WithPagination_ReturnsPaginatedResults()
    {
        var token = await RegisterAndLoginAsync();
        Authorize(token);

        const string ownerId = "owner-paged";
        await _client.PostAsJsonAsync("/api/wallets", new { ownerId, currency = "NGN" });

        for (var i = 1; i <= 5; i++)
        {
            await _client.PostAsJsonAsync("/api/transactions", new
            {
                reference     = $"TXN-PAGE-{i:000}",
                amount        = 100m * i,
                currency      = "NGN",
                type          = "Credit",
                walletOwnerId = ownerId
            });
        }

        var response = await _client.GetAsync("/api/transactions?page=1&pageSize=3");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<PagedResult<TransactionStatusResponse>>(JsonOpts);
        body!.Items.Should().HaveCount(3);
        body.TotalCount.Should().Be(5);
        body.TotalPages.Should().Be(2);
        body.HasNextPage.Should().BeTrue();
    }
}
