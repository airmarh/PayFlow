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

namespace PayFlow.Tests.Services;

public class TransactionServiceTests
{
    private readonly Mock<ITransactionRepository> _transactionRepoMock;
    private readonly Mock<IWalletRepository> _walletRepoMock;
    private readonly Mock<ILogger<TransactionService>> _loggerMock;
    private readonly TransactionService _sut;

    public TransactionServiceTests()
    {
        _transactionRepoMock = new Mock<ITransactionRepository>();
        _walletRepoMock      = new Mock<IWalletRepository>();
        _loggerMock          = new Mock<ILogger<TransactionService>>();

        _sut = new TransactionService(
            _transactionRepoMock.Object,
            _walletRepoMock.Object,
            _loggerMock.Object);
    }

    // ── InitiatePaymentAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task InitiatePayment_ValidCreditRequest_ReturnsPendingResponse()
    {
        // Arrange
        var request = new InitiatePaymentRequest
        {
            Reference    = "TXN-001",
            Amount       = 500m,
            Currency     = "NGN",
            Type         = TransactionType.Credit,
            WalletOwnerId = "user-123"
        };

        var wallet = new Wallet { OwnerId = "user-123", Balance = 1000m, Currency = "NGN" };

        _transactionRepoMock
            .Setup(r => r.GetByReferenceAsync(request.Reference, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Transaction?)null);

        _walletRepoMock
            .Setup(r => r.GetByOwnerIdAsync(request.WalletOwnerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(wallet);

        _transactionRepoMock
            .Setup(r => r.AddAsync(It.IsAny<Transaction>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _transactionRepoMock
            .Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var response = await _sut.InitiatePaymentAsync(request);

        // Assert
        response.Should().NotBeNull();
        response.Reference.Should().Be(request.Reference);
        response.Status.Should().Be(TransactionStatus.Pending);
        response.TransactionId.Should().NotBeEmpty();
        response.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

        _transactionRepoMock.Verify(r => r.AddAsync(
            It.Is<Transaction>(t =>
                t.Reference == request.Reference &&
                t.Amount    == request.Amount    &&
                t.Currency  == "NGN"             &&
                t.Type      == TransactionType.Credit &&
                t.Status    == TransactionStatus.Pending),
            It.IsAny<CancellationToken>()), Times.Once);

        _transactionRepoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task InitiatePayment_ValidDebitRequest_WhenSufficientFunds_ReturnsPendingResponse()
    {
        // Arrange
        var request = new InitiatePaymentRequest
        {
            Reference    = "TXN-DEBIT-001",
            Amount       = 200m,
            Currency     = "USD",
            Type         = TransactionType.Debit,
            WalletOwnerId = "user-456"
        };

        var wallet = new Wallet { OwnerId = "user-456", Balance = 500m, Currency = "USD" };

        _transactionRepoMock
            .Setup(r => r.GetByReferenceAsync(request.Reference, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Transaction?)null);

        _walletRepoMock
            .Setup(r => r.GetByOwnerIdAsync(request.WalletOwnerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(wallet);

        _transactionRepoMock
            .Setup(r => r.AddAsync(It.IsAny<Transaction>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _transactionRepoMock
            .Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var response = await _sut.InitiatePaymentAsync(request);

        // Assert
        response.Status.Should().Be(TransactionStatus.Pending);
        response.Reference.Should().Be(request.Reference);
    }

    [Fact]
    public async Task InitiatePayment_DebitRequest_WhenInsufficientFunds_ThrowsInsufficientFundsException()
    {
        // Arrange
        var request = new InitiatePaymentRequest
        {
            Reference    = "TXN-DEBIT-002",
            Amount       = 1000m,
            Currency     = "NGN",
            Type         = TransactionType.Debit,
            WalletOwnerId = "user-789"
        };

        var wallet = new Wallet { OwnerId = "user-789", Balance = 50m, Currency = "NGN" };

        _transactionRepoMock
            .Setup(r => r.GetByReferenceAsync(request.Reference, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Transaction?)null);

        _walletRepoMock
            .Setup(r => r.GetByOwnerIdAsync(request.WalletOwnerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(wallet);

        // Act
        Func<Task> act = () => _sut.InitiatePaymentAsync(request);

        // Assert
        await act.Should().ThrowAsync<InsufficientFundsException>()
            .WithMessage("*user-789*");

        _transactionRepoMock.Verify(r => r.AddAsync(It.IsAny<Transaction>(), It.IsAny<CancellationToken>()), Times.Never);
        _transactionRepoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task InitiatePayment_DuplicateReference_ThrowsConflictException()
    {
        // Arrange
        var request = new InitiatePaymentRequest
        {
            Reference    = "TXN-DUP-001",
            Amount       = 100m,
            Currency     = "GBP",
            Type         = TransactionType.Credit,
            WalletOwnerId = "user-123"
        };

        var existingTransaction = new Transaction { Reference = request.Reference };

        _transactionRepoMock
            .Setup(r => r.GetByReferenceAsync(request.Reference, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingTransaction);

        // Act
        Func<Task> act = () => _sut.InitiatePaymentAsync(request);

        // Assert
        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage($"*{request.Reference}*");

        _walletRepoMock.Verify(r => r.GetByOwnerIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _transactionRepoMock.Verify(r => r.AddAsync(It.IsAny<Transaction>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task InitiatePayment_WalletNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var request = new InitiatePaymentRequest
        {
            Reference    = "TXN-NF-001",
            Amount       = 100m,
            Currency     = "USD",
            Type         = TransactionType.Credit,
            WalletOwnerId = "nonexistent-owner"
        };

        _transactionRepoMock
            .Setup(r => r.GetByReferenceAsync(request.Reference, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Transaction?)null);

        _walletRepoMock
            .Setup(r => r.GetByOwnerIdAsync(request.WalletOwnerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Wallet?)null);

        // Act
        Func<Task> act = () => _sut.InitiatePaymentAsync(request);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*nonexistent-owner*");
    }

    // ── GetTransactionStatusAsync ────────────────────────────────────────────

    [Fact]
    public async Task GetTransactionStatus_ExistingReference_ReturnsStatusResponse()
    {
        // Arrange
        var reference = "TXN-STATUS-001";
        var transaction = new Transaction
        {
            Id        = Guid.NewGuid(),
            Reference = reference,
            Amount    = 750m,
            Currency  = "NGN",
            Status    = TransactionStatus.Successful,
            Type      = TransactionType.Credit,
            CreatedAt = DateTime.UtcNow.AddMinutes(-10),
            UpdatedAt = DateTime.UtcNow.AddMinutes(-5)
        };

        _transactionRepoMock
            .Setup(r => r.GetByReferenceAsync(reference, It.IsAny<CancellationToken>()))
            .ReturnsAsync(transaction);

        // Act
        var result = await _sut.GetTransactionStatusAsync(reference);

        // Assert
        result.Should().NotBeNull();
        result!.Reference.Should().Be(reference);
        result.Amount.Should().Be(750m);
        result.Currency.Should().Be("NGN");
        result.Status.Should().Be(TransactionStatus.Successful);
        result.Type.Should().Be(TransactionType.Credit);
        result.Id.Should().Be(transaction.Id);
    }

    [Fact]
    public async Task GetTransactionStatus_NonExistentReference_ReturnsNull()
    {
        // Arrange
        _transactionRepoMock
            .Setup(r => r.GetByReferenceAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Transaction?)null);

        // Act
        var result = await _sut.GetTransactionStatusAsync("ghost-reference");

        // Assert
        result.Should().BeNull();
    }

    // ── GetTransactionsAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task GetTransactions_WithPagination_ReturnsPagedResult()
    {
        // Arrange
        var transactions = new List<Transaction>
        {
            new() { Id = Guid.NewGuid(), Reference = "TXN-A", Amount = 100m, Currency = "USD", Status = TransactionStatus.Successful, Type = TransactionType.Credit, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new() { Id = Guid.NewGuid(), Reference = "TXN-B", Amount = 200m, Currency = "USD", Status = TransactionStatus.Pending,    Type = TransactionType.Debit,  CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }
        };

        _transactionRepoMock
            .Setup(r => r.GetPagedAsync(1, 10, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((transactions, 2));

        // Act
        var result = await _sut.GetTransactionsAsync(1, 10, null);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(2);
        result.TotalCount.Should().Be(2);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(10);
        result.TotalPages.Should().Be(1);
        result.HasNextPage.Should().BeFalse();
        result.HasPreviousPage.Should().BeFalse();
    }
}
