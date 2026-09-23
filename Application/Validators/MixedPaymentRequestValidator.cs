using FluentValidation;
using Application.DTOs.Wallet;

namespace Application.Validators;

public class MixedPaymentRequestValidator : AbstractValidator<MixedPaymentRequest>
{
    public MixedPaymentRequestValidator()
    {
        RuleFor(x => x.TotalOrder)
            .GreaterThan(0).WithMessage("El monto total de la orden debe ser mayor a L. 0.00.")
            .PrecisionScale(18, 2, false).WithMessage("El monto en Lempiras (LPS) debe tener máximo 2 decimales.");

        RuleFor(x => x.OrderId)
            .NotEmpty().WithMessage("El ID de la orden es obligatorio.");
    }
}
