namespace PayFlow.Application.DTOs;

public class CreateWalletRequest
{
    /// <summary>External user / merchant identifier that will own this wallet.</summary>
    public string OwnerId { get; set; } = string.Empty;

    /// <summary>ISO 4217 currency code, e.g. "NGN", "USD".</summary>
    public string Currency { get; set; } = string.Empty;
}
