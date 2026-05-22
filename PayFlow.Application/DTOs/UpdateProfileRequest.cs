namespace PayFlow.Application.DTOs;

public class UpdateProfileRequest
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }

    /// <summary>
    /// In production this would trigger an email verification flow before the address is changed.
    /// Updated directly here for simplicity.
    /// </summary>
    public string? Email { get; set; }
}
