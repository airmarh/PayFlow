using PayFlow.Domain.Entities;

namespace PayFlow.Application.Interfaces;

public interface IWalletRepository
{
    Task<Wallet?> GetByOwnerIdAsync(string ownerId, CancellationToken cancellationToken = default);
    Task AddAsync(Wallet wallet, CancellationToken cancellationToken = default);
    Task UpdateAsync(Wallet wallet, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
