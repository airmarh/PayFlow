using FluentValidation;
using PayFlow.Application.DTOs;
using PayFlow.Domain.Constants;

namespace PayFlow.Application.Validators;

public class InitiatePaymentRequestValidator : AbstractValidator<InitiatePaymentRequest>
{
    public InitiatePaymentRequestValidator()
    {
        RuleFor(x => x.Reference)
            .NotEmpty().WithMessage("Reference is required.")
            .MaximumLength(100).WithMessage("Reference must not exceed 100 characters.")
            .Matches(@"^[a-zA-Z0-9\-_]+$").WithMessage("Reference may only contain letters, digits, hyphens and underscores.");

        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("Amount must be greater than zero.")
            .LessThanOrEqualTo(10_000_000).WithMessage("Amount exceeds the maximum allowed per transaction.");

        RuleFor(x => x.Currency)
            .NotEmpty().WithMessage("Currency is required.")
            .Must(c => CurrencyConstants.Supported.Contains(c.ToUpperInvariant()))
            .WithMessage($"Currency must be one of: {string.Join(", ", CurrencyConstants.Supported)}.");

        RuleFor(x => x.WalletOwnerId)
            .NotEmpty().WithMessage("WalletOwnerId is required.")
            .MaximumLength(200).WithMessage("WalletOwnerId must not exceed 200 characters.");

        RuleFor(x => x.Type)
            .IsInEnum().WithMessage("Type must be either Credit (0) or Debit (1).");
    }
}
