using Microsoft.EntityFrameworkCore;
using PayFlow.Application.Exceptions;
using PayFlow.Application.Interfaces;
using PayFlow.Domain.Entities;
using PayFlow.Domain.Enums;
using PayFlow.Infrastructure.Data;

namespace PayFlow.Infrastructure.Repositories;

public class TransactionRepository : ITransactionRepository
{
    private readonly PayFlowDbContext _context;

    public TransactionRepository(PayFlowDbContext context)
    {
        _context = context;
    }

    public async Task<Transaction?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await _context.Transactions
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    public async Task<Transaction?> GetByReferenceAsync(string reference, CancellationToken cancellationToken = default)
        => await _context.Transactions
            .FirstOrDefaultAsync(t => t.Reference == reference, cancellationToken);

    public async Task<(IEnumerable<Transaction> Items, int TotalCount)> GetPagedAsync(
        int page,
        int pageSize,
        TransactionStatus? statusFilter,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Transactions.AsNoTracking().AsQueryable();

        if (statusFilter.HasValue)
            query = query.Where(t => t.Status == statusFilter.Value);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task AddAsync(Transaction transaction, CancellationToken cancellationToken = default)
        => await _context.Transactions.AddAsync(transaction, cancellationToken);

    public Task UpdateAsync(Transaction transaction, CancellationToken cancellationToken = default)
    {
        _context.Transactions.Update(transaction);
        return Task.CompletedTask;
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConcurrencyException(
                "A concurrency conflict occurred. The resource was modified by a concurrent request. Please retry.", ex);
        }
    }
}
