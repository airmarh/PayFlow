using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PayFlow.Application.DTOs;
using PayFlow.Application.Interfaces;

namespace PayFlow.API.Controllers;

[ApiController]
[Authorize]
[Route("api/wallets")]
[Produces("application/json")]
[EnableRateLimiting("api")]
public class WalletController : ControllerBase
{
    private readonly IWalletService _walletService;
    private readonly IValidator<CreateWalletRequest> _validator;
    private readonly ILogger<WalletController> _logger;

    public WalletController(
        IWalletService walletService,
        IValidator<CreateWalletRequest> validator,
        ILogger<WalletController> logger)
    {
        _walletService = walletService;
        _validator     = validator;
        _logger        = logger;
    }

    /// <summary>Creates a new wallet for an owner. Each owner may only have one wallet.</summary>
    /// <response code="201">Wallet created with a zero opening balance.</response>
    /// <response code="400">Validation error — missing or invalid fields.</response>
    /// <response code="409">A wallet for this owner already exists.</response>
    [HttpPost]
    [ProducesResponseType(typeof(CreateWalletResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateWallet(
        [FromBody] CreateWalletRequest request,
        CancellationToken cancellationToken)
    {
        var validationResult = await _validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            foreach (var error in validationResult.Errors)
                ModelState.AddModelError(error.PropertyName, error.ErrorMessage);

            return ValidationProblem(ModelState);
        }

        var response = await _walletService.CreateWalletAsync(request, cancellationToken);

        return CreatedAtAction(
            nameof(GetWalletBalance),
            new { ownerId = response.OwnerId },
            response);
    }

    /// <summary>Retrieves the wallet balance for a given owner.</summary>
    /// <param name="ownerId">The external owner identifier.</param>
    /// <response code="200">Wallet found.</response>
    /// <response code="404">No wallet found for the given owner ID.</response>
    [HttpGet("{ownerId}")]
    [ProducesResponseType(typeof(CreateWalletResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetWalletBalance(string ownerId, CancellationToken cancellationToken)
    {
        var wallet = await _walletService.GetWalletByOwnerIdAsync(ownerId, cancellationToken);

        if (wallet is null)
            return NotFound(new { message = $"Wallet for owner '{ownerId}' was not found." });

        return Ok(wallet);
    }
}
