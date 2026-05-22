namespace PayFlow.Application.DTOs;

public class UpdateProfileResponse
{
    public Guid Id { get; set; }
    public required string Email { get; set; }
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public DateTime UpdatedAt { get; set; }
}
