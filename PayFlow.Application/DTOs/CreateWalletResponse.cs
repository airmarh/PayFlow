namespace PayFlow.Application.DTOs;

public class CreateWalletResponse
{
    public Guid Id { get; set; }
    public string OwnerId { get; set; } = string.Empty;
    public decimal Balance { get; set; }
    public string Currency { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
