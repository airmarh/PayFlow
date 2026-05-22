using PayFlow.Domain.Enums;

namespace PayFlow.Application.DTOs;

public class InitiatePaymentRequest
{
    /// <summary>Caller-supplied unique reference for idempotency.</summary>
    public required string Reference { get; set; }
    public decimal Amount { get; set; }
    public required string Currency { get; set; }
    public TransactionType Type { get; set; }
    public required string WalletOwnerId { get; set; }
}
