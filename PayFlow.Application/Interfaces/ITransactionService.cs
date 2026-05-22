using PayFlow.Application.DTOs;
using PayFlow.Domain.Enums;

namespace PayFlow.Application.Interfaces;

public interface ITransactionService
{
    Task<InitiatePaymentResponse> InitiatePaymentAsync(
        InitiatePaymentRequest request,
        CancellationToken cancellationToken = default);

    Task<TransactionStatusResponse?> GetTransactionStatusAsync(
        string reference,
        CancellationToken cancellationToken = default);

    Task<PagedResult<TransactionStatusResponse>> GetTransactionsAsync(
        int page,
        int pageSize,
        TransactionStatus? statusFilter,
        CancellationToken cancellationToken = default);
}
