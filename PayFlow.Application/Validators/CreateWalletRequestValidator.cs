using FluentValidation;
using PayFlow.Application.DTOs;
using PayFlow.Domain.Constants;

namespace PayFlow.Application.Validators;

public class CreateWalletRequestValidator : AbstractValidator<CreateWalletRequest>
{
    public CreateWalletRequestValidator()
    {
        RuleFor(x => x.OwnerId)
            .NotEmpty().WithMessage("OwnerId is required.")
            .MaximumLength(200).WithMessage("OwnerId must not exceed 200 characters.");

        RuleFor(x => x.Currency)
            .NotEmpty().WithMessage("Currency is required.")
            .Must(c => CurrencyConstants.Supported.Contains(c.ToUpperInvariant()))
            .WithMessage($"Currency must be one of: {string.Join(", ", CurrencyConstants.Supported)}.");
    }
}
