using Microsoft.Extensions.Logging;
using PayFlow.Application.DTOs;
using PayFlow.Application.Exceptions;
using PayFlow.Application.Interfaces;
using PayFlow.Domain.Entities;
using PayFlow.Domain.Enums;

namespace PayFlow.Application.Services;

public class TransactionService : ITransactionService
{
    private readonly ITransactionRepository _transactionRepository;
    private readonly IWalletRepository _walletRepository;
    private readonly ILogger<TransactionService> _logger;

    public TransactionService(
        ITransactionRepository transactionRepository,
        IWalletRepository walletRepository,
        ILogger<TransactionService> logger)
    {
        _transactionRepository = transactionRepository;
        _walletRepository = walletRepository;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<InitiatePaymentResponse> InitiatePaymentAsync(
        InitiatePaymentRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Initiating {Type} payment of {Amount} {Currency} for wallet owner {OwnerId} with reference {Reference}",
            request.Type, request.Amount, request.Currency, request.WalletOwnerId, request.Reference);

        var existing = await _transactionRepository.GetByReferenceAsync(request.Reference, cancellationToken);
        if (existing is not null)
        {
            throw new ConflictException($"A transaction with reference '{request.Reference}' already exists.");
        }

        var wallet = await _walletRepository.GetByOwnerIdAsync(request.WalletOwnerId, cancellationToken)
            ?? throw new NotFoundException(nameof(Wallet), request.WalletOwnerId);

        if (!wallet.Currency.Equals(request.Currency, StringComparison.OrdinalIgnoreCase))
            throw new ValidationException(
                $"Transaction currency '{request.Currency}' does not match wallet currency '{wallet.Currency}'.");

        if (request.Type == TransactionType.Debit && wallet.Balance < request.Amount)
        {
            throw new InsufficientFundsException(request.WalletOwnerId, request.Amount, wallet.Balance);
        }

        var transaction = new Transaction
        {
            Reference = request.Reference,
            Amount = request.Amount,
            Currency = request.Currency.ToUpperInvariant(),
            Type = request.Type,
            Status = TransactionStatus.Pending,
            WalletOwnerId = request.WalletOwnerId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _transactionRepository.AddAsync(transaction, cancellationToken);
        await _transactionRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Transaction {Reference} created with ID {Id}", transaction.Reference, transaction.Id);

        return new InitiatePaymentResponse
        {
            TransactionId = transaction.Id,
            Reference = transaction.Reference,
            Status = transaction.Status,
            CreatedAt = transaction.CreatedAt
        };
    }

    /// <inheritdoc />
    public async Task<TransactionStatusResponse?> GetTransactionStatusAsync(
        string reference,
        CancellationToken cancellationToken = default)
    {
        var transaction = await _transactionRepository.GetByReferenceAsync(reference, cancellationToken);
        if (transaction is null) return null;

        return MapToStatusResponse(transaction);
    }

    /// <inheritdoc />
    public async Task<PagedResult<TransactionStatusResponse>> GetTransactionsAsync(
        int page,
        int pageSize,
        TransactionStatus? statusFilter,
        CancellationToken cancellationToken = default)
    {
        var (items, totalCount) = await _transactionRepository.GetPagedAsync(page, pageSize, statusFilter, cancellationToken);

        return new PagedResult<TransactionStatusResponse>
        {
            Items = items.Select(MapToStatusResponse),
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    private static TransactionStatusResponse MapToStatusResponse(Domain.Entities.Transaction t) =>
        new()
        {
            Id = t.Id,
            Reference = t.Reference,
            Amount = t.Amount,
            Currency = t.Currency,
            Status = t.Status,
            Type = t.Type,
            CreatedAt = t.CreatedAt,
            UpdatedAt = t.UpdatedAt
        };
}
