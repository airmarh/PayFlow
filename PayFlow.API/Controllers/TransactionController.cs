using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PayFlow.Application.DTOs;
using PayFlow.Application.Interfaces;
using PayFlow.Domain.Enums;

namespace PayFlow.API.Controllers;

[ApiController]
[Authorize]
[Route("api/transactions")]
[Produces("application/json")]
[EnableRateLimiting("api")]
public class TransactionController : ControllerBase
{
    private readonly ITransactionService _transactionService;
    private readonly IValidator<InitiatePaymentRequest> _validator;
    private readonly ILogger<TransactionController> _logger;

    public TransactionController(
        ITransactionService transactionService,
        IValidator<InitiatePaymentRequest> validator,
        ILogger<TransactionController> logger)
    {
        _transactionService = transactionService;
        _validator = validator;
        _logger = logger;
    }

    /// <summary>Initiates a new payment transaction.</summary>
    /// <response code="201">Transaction created and pending processing.</response>
    /// <response code="400">Request validation failed.</response>
    /// <response code="404">Wallet not found for the given owner ID.</response>
    /// <response code="409">A transaction with this reference already exists.</response>
    /// <response code="422">Insufficient funds for a debit transaction.</response>
    [HttpPost]
    [ProducesResponseType(typeof(InitiatePaymentResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> InitiatePayment(
        [FromBody] InitiatePaymentRequest request,
        CancellationToken cancellationToken)
    {
        var validationResult = await _validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            foreach (var error in validationResult.Errors)
                ModelState.AddModelError(error.PropertyName, error.ErrorMessage);

            return ValidationProblem(ModelState);
        }

        var response = await _transactionService.InitiatePaymentAsync(request, cancellationToken);

        return CreatedAtAction(
            nameof(GetTransactionStatus),
            new { reference = response.Reference },
            response);
    }

    /// <summary>Gets the current status of a transaction by its reference.</summary>
    /// <param name="reference">The unique business reference of the transaction.</param>
    /// <response code="200">Transaction found.</response>
    /// <response code="404">No transaction with the given reference exists.</response>
    [HttpGet("{reference}")]
    [ProducesResponseType(typeof(TransactionStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTransactionStatus(
        string reference,
        CancellationToken cancellationToken)
    {
        var result = await _transactionService.GetTransactionStatusAsync(reference, cancellationToken);

        if (result is null)
            return NotFound(new { message = $"Transaction with reference '{reference}' was not found." });

        return Ok(result);
    }

    /// <summary>Lists transactions with optional status filtering and pagination.</summary>
    /// <param name="page">Page number (1-based). Default: 1.</param>
    /// <param name="pageSize">Number of records per page (1–100). Default: 20.</param>
    /// <param name="status">Optional status filter: Pending (0), Successful (1), Failed (2).</param>
    /// <response code="200">Paginated list of transactions.</response>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<TransactionStatusResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetTransactions(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] TransactionStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        if (page < 1)
            return BadRequest(new { message = "Page must be greater than or equal to 1." });

        if (pageSize < 1 || pageSize > 100)
            return BadRequest(new { message = "PageSize must be between 1 and 100." });

        var result = await _transactionService.GetTransactionsAsync(page, pageSize, status, cancellationToken);
        return Ok(result);
    }
}
