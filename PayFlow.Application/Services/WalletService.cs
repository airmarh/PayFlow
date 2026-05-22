using Microsoft.Extensions.Logging;
using PayFlow.Application.DTOs;
using PayFlow.Application.Exceptions;
using PayFlow.Application.Interfaces;
using PayFlow.Domain.Entities;

namespace PayFlow.Application.Services;

public class WalletService : IWalletService
{
    private readonly IWalletRepository _walletRepository;
    private readonly ILogger<WalletService> _logger;

    public WalletService(IWalletRepository walletRepository, ILogger<WalletService> logger)
    {
        _walletRepository = walletRepository;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<CreateWalletResponse> CreateWalletAsync(
        CreateWalletRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Creating wallet for owner {OwnerId} with currency {Currency}",
            request.OwnerId, request.Currency);

        var existing = await _walletRepository.GetByOwnerIdAsync(request.OwnerId, cancellationToken);
        if (existing is not null)
            throw new ConflictException($"A wallet for owner '{request.OwnerId}' already exists.");

        var wallet = new Wallet
        {
            OwnerId   = request.OwnerId,
            Currency  = request.Currency.ToUpperInvariant(),
            Balance   = 0m,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _walletRepository.AddAsync(wallet, cancellationToken);
        await _walletRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Wallet {WalletId} created for owner {OwnerId}", wallet.Id, wallet.OwnerId);

        return MapToResponse(wallet);
    }

    /// <inheritdoc />
    public async Task<CreateWalletResponse?> GetWalletByOwnerIdAsync(
        string ownerId,
        CancellationToken cancellationToken = default)
    {
        var wallet = await _walletRepository.GetByOwnerIdAsync(ownerId, cancellationToken);
        return wallet is null ? null : MapToResponse(wallet);
    }

    private static CreateWalletResponse MapToResponse(Wallet wallet) => new()
    {
        Id        = wallet.Id,
        OwnerId   = wallet.OwnerId,
        Balance   = wallet.Balance,
        Currency  = wallet.Currency,
        CreatedAt = wallet.CreatedAt
    };
}
