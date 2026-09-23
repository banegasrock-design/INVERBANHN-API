using FluentValidation;
using Application.DTOs.Carrier;

namespace Application.Validators;

/// <summary>
/// Validador para RTN hondureño (14 dígitos) y campos de cotización.
/// </summary>
public class QuoteRequestValidator : AbstractValidator<QuoteRequestDto>
{
    public QuoteRequestValidator()
    {
        RuleFor(x => x.OriginCity)
            .NotEmpty().WithMessage("La ciudad de origen es obligatoria.");

        RuleFor(x => x.DestinationCity)
            .NotEmpty().WithMessage("La ciudad de destino es obligatoria.");

        RuleFor(x => x.CarrierId)
            .GreaterThan(0).WithMessage("El ID del carrier debe ser mayor a 0.");
    }
}
