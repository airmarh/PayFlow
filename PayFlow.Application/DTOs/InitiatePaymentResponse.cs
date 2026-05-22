using PayFlow.Domain.Enums;

namespace PayFlow.Application.DTOs;

public class InitiatePaymentResponse
{
    public Guid TransactionId { get; set; }
    public required string Reference { get; set; }
    public TransactionStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
}
