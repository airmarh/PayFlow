using PayFlow.Domain.Enums;

namespace PayFlow.Application.DTOs;

public class TransactionStatusResponse
{
    public Guid Id { get; set; }
    public required string Reference { get; set; }
    public decimal Amount { get; set; }
    public required string Currency { get; set; }
    public TransactionStatus Status { get; set; }
    public TransactionType Type { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
