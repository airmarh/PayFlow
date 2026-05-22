using PayFlow.Application.DTOs;

namespace PayFlow.Application.Interfaces;

public interface IWalletService
{
    Task<CreateWalletResponse> CreateWalletAsync(
        CreateWalletRequest request,
        CancellationToken cancellationToken = default);

    Task<CreateWalletResponse?> GetWalletByOwnerIdAsync(
        string ownerId,
        CancellationToken cancellationToken = default);
}
