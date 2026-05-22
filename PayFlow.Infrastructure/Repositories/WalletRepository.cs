using Microsoft.EntityFrameworkCore;
using PayFlow.Application.Exceptions;
using PayFlow.Application.Interfaces;
using PayFlow.Domain.Entities;
using PayFlow.Infrastructure.Data;

namespace PayFlow.Infrastructure.Repositories;

public class WalletRepository : IWalletRepository
{
    private readonly PayFlowDbContext _context;

    public WalletRepository(PayFlowDbContext context)
    {
        _context = context;
    }

    public async Task<Wallet?> GetByOwnerIdAsync(string ownerId, CancellationToken cancellationToken = default)
        => await _context.Wallets
            .FirstOrDefaultAsync(w => w.OwnerId == ownerId, cancellationToken);

    public async Task AddAsync(Wallet wallet, CancellationToken cancellationToken = default)
        => await _context.Wallets.AddAsync(wallet, cancellationToken);

    public Task UpdateAsync(Wallet wallet, CancellationToken cancellationToken = default)
    {
        _context.Wallets.Update(wallet);
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
