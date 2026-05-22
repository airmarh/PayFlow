using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using PayFlow.Application.DTOs;
using PayFlow.Application.Exceptions;
using PayFlow.Application.Interfaces;
using PayFlow.Application.Services;
using PayFlow.Domain.Entities;
using PayFlow.Domain.Enums;
using Xunit;
using WebhookEvent = PayFlow.Domain.Entities.WebhookEvent;

namespace PayFlow.Tests.Services;

public class WebhookServiceTests
{
    private readonly Mock<ITransactionRepository> _transactionRepoMock;
    private readonly Mock<IWalletRepository> _walletRepoMock;
    private readonly Mock<IWebhookEventRepository> _webhookEventRepoMock;
    private readonly Mock<ILogger<WebhookService>> _loggerMock;
    private readonly WebhookService _sut;

    public WebhookServiceTests()
    {
        _transactionRepoMock  = new Mock<ITransactionRepository>();
        _walletRepoMock       = new Mock<IWalletRepository>();
        _webhookEventRepoMock = new Mock<IWebhookEventRepository>();
        _loggerMock           = new Mock<ILogger<WebhookService>>();

        _webhookEventRepoMock
            .Setup(r => r.AddAsync(It.IsAny<WebhookEvent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _sut = new WebhookService(
            _transactionRepoMock.Object,
            _walletRepoMock.Object,
            _webhookEventRepoMock.Object,
            _loggerMock.Object);
    }

    // ── Successful processing ─────────────────────────────────────────────────

    [Fact]
    public async Task ProcessWebhook_SuccessfulEvent_UpdatesTransactionAndCreditsWallet()
    {
        // Arrange
        const string reference = "TXN-WH-001";
        var walletOwnerId = "user-wh-123";

        var transaction = new Transaction
        {
            Id            = Guid.NewGuid(),
            Reference     = reference,
            Amount        = 300m,
            Currency      = "NGN",
            Status        = TransactionStatus.Pending,
            Type          = TransactionType.Credit,
            WalletOwnerId = walletOwnerId
        };

        var wallet = new Wallet
        {
            Id       = Guid.NewGuid(),
            OwnerId  = walletOwnerId,
            Balance  = 1000m,
            Currency = "NGN"
        };

        var payload = new WebhookPayload
        {
            TransactionReference = reference,
            Status               = TransactionStatus.Successful,
            RawPayload           = "{\"event\":\"payment.success\"}",
            EventTimestamp       = DateTime.UtcNow
        };

        _transactionRepoMock
            .Setup(r => r.GetByReferenceAsync(reference, It.IsAny<CancellationToken>()))
            .ReturnsAsync(transaction);

        _walletRepoMock
            .Setup(r => r.GetByOwnerIdAsync(walletOwnerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(wallet);

        _walletRepoMock.Setup(r => r.UpdateAsync(It.IsAny<Wallet>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _walletRepoMock.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _transactionRepoMock.Setup(r => r.UpdateAsync(It.IsAny<Transaction>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _transactionRepoMock.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        // Act
        await _sut.ProcessWebhookAsync(payload);

        // Assert — transaction updated to Successful
        _transactionRepoMock.Verify(r => r.UpdateAsync(
            It.Is<Transaction>(t => t.Status == TransactionStatus.Successful && t.Reference == reference),
            It.IsAny<CancellationToken>()), Times.Once);

        // Assert — wallet balance credited
        wallet.Balance.Should().Be(1300m);
        _walletRepoMock.Verify(r => r.UpdateAsync(
            It.Is<Wallet>(w => w.OwnerId == walletOwnerId && w.Balance == 1300m),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessWebhook_FailedEvent_UpdatesTransactionStatusWithoutChangingWallet()
    {
        // Arrange
        const string reference = "TXN-WH-FAIL";
        var walletOwnerId = "user-wh-456";

        var transaction = new Transaction
        {
            Id            = Guid.NewGuid(),
            Reference     = reference,
            Amount        = 200m,
            Currency      = "USD",
            Status        = TransactionStatus.Pending,
            Type          = TransactionType.Credit,
            WalletOwnerId = walletOwnerId
        };

        var payload = new WebhookPayload
        {
            TransactionReference = reference,
            Status               = TransactionStatus.Failed,
            RawPayload           = "{\"event\":\"payment.failed\"}",
            EventTimestamp       = DateTime.UtcNow
        };

        _transactionRepoMock
            .Setup(r => r.GetByReferenceAsync(reference, It.IsAny<CancellationToken>()))
            .ReturnsAsync(transaction);

        _transactionRepoMock.Setup(r => r.UpdateAsync(It.IsAny<Transaction>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _transactionRepoMock.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        // Act
        await _sut.ProcessWebhookAsync(payload);

        // Assert — transaction status updated to Failed
        _transactionRepoMock.Verify(r => r.UpdateAsync(
            It.Is<Transaction>(t => t.Status == TransactionStatus.Failed),
            It.IsAny<CancellationToken>()), Times.Once);

        // Assert — wallet never touched for a failed event
        _walletRepoMock.Verify(r => r.GetByOwnerIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _walletRepoMock.Verify(r => r.UpdateAsync(It.IsAny<Wallet>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── Duplicate / idempotency ───────────────────────────────────────────────

    [Fact]
    public async Task ProcessWebhook_DuplicateSuccessfulEvent_IsIgnoredIdempotently()
    {
        // Arrange — transaction is already Successful (terminal state)
        const string reference = "TXN-WH-DUP";

        var transaction = new Transaction
        {
            Id            = Guid.NewGuid(),
            Reference     = reference,
            Amount        = 500m,
            Currency      = "GBP",
            Status        = TransactionStatus.Successful,  // already in terminal state
            Type          = TransactionType.Credit,
            WalletOwnerId = "user-wh-789"
        };

        var payload = new WebhookPayload
        {
            TransactionReference = reference,
            Status               = TransactionStatus.Successful,  // same terminal state — duplicate
            RawPayload           = "{\"event\":\"payment.success\"}",
            EventTimestamp       = DateTime.UtcNow
        };

        _transactionRepoMock
            .Setup(r => r.GetByReferenceAsync(reference, It.IsAny<CancellationToken>()))
            .ReturnsAsync(transaction);

        // Act — should complete without throwing
        await _sut.ProcessWebhookAsync(payload);

        // Assert — no updates performed; duplicate silently swallowed
        _transactionRepoMock.Verify(r => r.UpdateAsync(It.IsAny<Transaction>(), It.IsAny<CancellationToken>()), Times.Never);
        _transactionRepoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _walletRepoMock.Verify(r => r.UpdateAsync(It.IsAny<Wallet>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessWebhook_ConflictingStatusOnTerminalTransaction_IsIgnored()
    {
        // Arrange — transaction already Failed; provider sends a conflicting Successful event
        const string reference = "TXN-WH-CONFLICT";

        var transaction = new Transaction
        {
            Id            = Guid.NewGuid(),
            Reference     = reference,
            Amount        = 100m,
            Currency      = "NGN",
            Status        = TransactionStatus.Failed,   // already terminal
            Type          = TransactionType.Credit,
            WalletOwnerId = "user-conflict"
        };

        var payload = new WebhookPayload
        {
            TransactionReference = reference,
            Status               = TransactionStatus.Successful,  // conflicting — first one wins
            RawPayload           = "{\"event\":\"payment.success\"}",
            EventTimestamp       = DateTime.UtcNow
        };

        _transactionRepoMock
            .Setup(r => r.GetByReferenceAsync(reference, It.IsAny<CancellationToken>()))
            .ReturnsAsync(transaction);

        // Act
        await _sut.ProcessWebhookAsync(payload);

        // Assert — no changes made; terminal state is immutable
        _transactionRepoMock.Verify(r => r.UpdateAsync(It.IsAny<Transaction>(), It.IsAny<CancellationToken>()), Times.Never);
        _walletRepoMock.Verify(r => r.GetByOwnerIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── Error cases ───────────────────────────────────────────────────────────

    [Fact]
    public async Task ProcessWebhook_UnknownReference_ThrowsNotFoundException()
    {
        // Arrange
        var payload = new WebhookPayload
        {
            TransactionReference = "UNKNOWN-REF",
            Status               = TransactionStatus.Successful,
            RawPayload           = "{}",
            EventTimestamp       = DateTime.UtcNow
        };

        _transactionRepoMock
            .Setup(r => r.GetByReferenceAsync("UNKNOWN-REF", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Transaction?)null);

        // Act
        Func<Task> act = () => _sut.ProcessWebhookAsync(payload);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*UNKNOWN-REF*");
    }

    [Fact]
    public async Task ProcessWebhook_SuccessfulDebit_DeductsFromWalletBalance()
    {
        // Arrange
        const string reference   = "TXN-WH-DEBIT";
        const string ownerId     = "user-debit-wh";

        var transaction = new Transaction
        {
            Id            = Guid.NewGuid(),
            Reference     = reference,
            Amount        = 150m,
            Currency      = "USD",
            Status        = TransactionStatus.Pending,
            Type          = TransactionType.Debit,
            WalletOwnerId = ownerId
        };

        var wallet = new Wallet
        {
            OwnerId  = ownerId,
            Balance  = 600m,
            Currency = "USD"
        };

        var payload = new WebhookPayload
        {
            TransactionReference = reference,
            Status               = TransactionStatus.Successful,
            RawPayload           = "{\"event\":\"payment.success\"}",
            EventTimestamp       = DateTime.UtcNow
        };

        _transactionRepoMock
            .Setup(r => r.GetByReferenceAsync(reference, It.IsAny<CancellationToken>()))
            .ReturnsAsync(transaction);

        _walletRepoMock
            .Setup(r => r.GetByOwnerIdAsync(ownerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(wallet);

        _walletRepoMock.Setup(r => r.UpdateAsync(It.IsAny<Wallet>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _walletRepoMock.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _transactionRepoMock.Setup(r => r.UpdateAsync(It.IsAny<Transaction>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _transactionRepoMock.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        // Act
        await _sut.ProcessWebhookAsync(payload);

        // Assert — 600 - 150 = 450
        wallet.Balance.Should().Be(450m);
    }
}
