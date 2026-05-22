using FluentValidation;
using PayFlow.Application.DTOs;

namespace PayFlow.Application.Validators;

public class UpdateProfileRequestValidator : AbstractValidator<UpdateProfileRequest>
{
    public UpdateProfileRequestValidator()
    {
        RuleFor(x => x.Email)
            .EmailAddress().WithMessage("Email must be a valid email address.")
            .MaximumLength(256).WithMessage("Email must not exceed 256 characters.")
            .When(x => !string.IsNullOrWhiteSpace(x.Email));

        RuleFor(x => x.FirstName)
            .MaximumLength(100).WithMessage("First name must not exceed 100 characters.")
            .When(x => !string.IsNullOrWhiteSpace(x.FirstName));

        RuleFor(x => x.LastName)
            .MaximumLength(100).WithMessage("Last name must not exceed 100 characters.")
            .When(x => !string.IsNullOrWhiteSpace(x.LastName));

        RuleFor(x => x)
            .Must(x => !string.IsNullOrWhiteSpace(x.FirstName)
                    || !string.IsNullOrWhiteSpace(x.LastName)
                    || !string.IsNullOrWhiteSpace(x.Email))
            .WithMessage("At least one field (FirstName, LastName, Email) must be provided.");
    }
}
