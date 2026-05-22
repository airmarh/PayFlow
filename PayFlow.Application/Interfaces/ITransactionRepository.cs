using PayFlow.Domain.Entities;
using PayFlow.Domain.Enums;

namespace PayFlow.Application.Interfaces;

public interface ITransactionRepository
{
    Task<Transaction?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Transaction?> GetByReferenceAsync(string reference, CancellationToken cancellationToken = default);

    /// <summary>Returns a paginated list of transactions, optionally filtered by status.</summary>
    Task<(IEnumerable<Transaction> Items, int TotalCount)> GetPagedAsync(
        int page,
        int pageSize,
        TransactionStatus? statusFilter,
        CancellationToken cancellationToken = default);

    Task AddAsync(Transaction transaction, CancellationToken cancellationToken = default);
    Task UpdateAsync(Transaction transaction, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
