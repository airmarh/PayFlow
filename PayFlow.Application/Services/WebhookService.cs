using Microsoft.Extensions.Logging;
using PayFlow.Application.DTOs;
using PayFlow.Application.Exceptions;
using PayFlow.Application.Interfaces;
using PayFlow.Domain.Entities;
using PayFlow.Domain.Enums;

namespace PayFlow.Application.Services;

public class WebhookService : IWebhookService
{
    private readonly ITransactionRepository _transactionRepository;
    private readonly IWalletRepository _walletRepository;
    private readonly IWebhookEventRepository _webhookEventRepository;
    private readonly ILogger<WebhookService> _logger;

    public WebhookService(
        ITransactionRepository transactionRepository,
        IWalletRepository walletRepository,
        IWebhookEventRepository webhookEventRepository,
        ILogger<WebhookService> logger)
    {
        _transactionRepository  = transactionRepository;
        _walletRepository       = walletRepository;
        _webhookEventRepository = webhookEventRepository;
        _logger                 = logger;
    }

    /// <inheritdoc />
    public async Task ProcessWebhookAsync(WebhookPayload payload, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Received webhook for reference {Reference} with status {Status}",
            payload.TransactionReference, payload.Status);

        var transaction = await _transactionRepository.GetByReferenceAsync(payload.TransactionReference, cancellationToken)
            ?? throw new NotFoundException(nameof(Transaction), payload.TransactionReference);

        // Idempotency guard — duplicate terminal event, silently skip
        if (IsTerminalStatus(transaction.Status) && transaction.Status == payload.Status)
        {
            _logger.LogWarning(
                "Duplicate webhook received for reference {Reference} with status {Status}. Skipping.",
                payload.TransactionReference, payload.Status);
            return;
        }

        // First-wins — ignore conflicting event if already terminal
        if (IsTerminalStatus(transaction.Status))
        {
            _logger.LogWarning(
                "Transaction {Reference} is already in terminal state {CurrentStatus}. Ignoring new status {NewStatus}.",
                payload.TransactionReference, transaction.Status, payload.Status);
            return;
        }

        var previousStatus = transaction.Status;
        transaction.Status    = payload.Status;
        transaction.UpdatedAt = DateTime.UtcNow;

        if (payload.Status == TransactionStatus.Successful)
        {
            await ApplyWalletChangeAsync(transaction, cancellationToken);
        }

        await _transactionRepository.UpdateAsync(transaction, cancellationToken);

        // Record the inbound event for audit — saved atomically with the transaction and wallet changes below
        await _webhookEventRepository.AddAsync(new WebhookEvent
        {
            TransactionReference = payload.TransactionReference,
            Payload              = payload.RawPayload,
            ReceivedAt           = payload.EventTimestamp,
            Processed            = true
        }, cancellationToken);

        await _transactionRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Transaction {Reference} status updated from {OldStatus} to {NewStatus}",
            transaction.Reference, previousStatus, transaction.Status);
    }

    private async Task ApplyWalletChangeAsync(Transaction transaction, CancellationToken cancellationToken)
    {
        if (transaction.WalletOwnerId is null)
        {
            _logger.LogWarning(
                "Transaction {Reference} has no associated WalletOwnerId; skipping balance update.",
                transaction.Reference);
            return;
        }

        var wallet = await _walletRepository.GetByOwnerIdAsync(transaction.WalletOwnerId, cancellationToken)
            ?? throw new NotFoundException(nameof(Wallet), transaction.WalletOwnerId);

        if (transaction.Type == TransactionType.Credit)
        {
            wallet.Balance += transaction.Amount;
        }
        else
        {
            if (wallet.Balance < transaction.Amount)
                throw new InsufficientFundsException(wallet.OwnerId, transaction.Amount, wallet.Balance);

            wallet.Balance -= transaction.Amount;
        }

        wallet.UpdatedAt = DateTime.UtcNow;
        await _walletRepository.UpdateAsync(wallet, cancellationToken);
        // Intentionally no SaveChangesAsync here — the caller persists everything atomically.

        _logger.LogInformation(
            "Wallet for owner {OwnerId} balance updated to {Balance} after {Type} of {Amount}",
            wallet.OwnerId, wallet.Balance, transaction.Type, transaction.Amount);
    }

    private static bool IsTerminalStatus(TransactionStatus status) =>
        status is TransactionStatus.Successful or TransactionStatus.Failed;
}
