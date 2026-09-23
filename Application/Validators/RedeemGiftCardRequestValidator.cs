using FluentValidation;
using Application.DTOs.Wallet;

namespace Application.Validators;

public class RedeemGiftCardRequestValidator : AbstractValidator<RedeemGiftCardRequest>
{
    public RedeemGiftCardRequestValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("El código de la Gift Card es obligatorio.")
            .MinimumLength(8).WithMessage("El código debe tener al menos 8 caracteres.")
            .MaximumLength(20).WithMessage("El código no puede exceder los 20 caracteres.")
            .Matches(@"^[A-Za-z0-9\-]+$").WithMessage("El código solo puede contener letras, números y guiones.");
    }
}
