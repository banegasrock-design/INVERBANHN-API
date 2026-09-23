using FluentValidation;
using Application.DTOs.Carrier;

namespace Application.Validators;

public class CreateCarrierRequestValidator : AbstractValidator<CreateCarrierRequest>
{
    public CreateCarrierRequestValidator()
    {
        RuleFor(x => x.CarrierName)
            .NotEmpty().WithMessage("El nombre del carrier es obligatorio.")
            .MaximumLength(100).WithMessage("El nombre no puede exceder los 100 caracteres.");

        When(x => x.OffersCOD, () =>
        {
            RuleFor(x => x.CodFeePercentage)
                .NotNull().WithMessage("El porcentaje de comisión COD es obligatorio cuando el carrier ofrece COD.")
                .GreaterThan(0).WithMessage("El porcentaje COD debe ser mayor a 0.")
                .LessThanOrEqualTo(100).WithMessage("El porcentaje COD no puede ser mayor a 100.");
        });
    }
}

public class CreateShippingRateRequestValidator : AbstractValidator<CreateShippingRateRequest>
{
    public CreateShippingRateRequestValidator()
    {
        RuleFor(x => x.CarrierId)
            .GreaterThan(0).WithMessage("El Carrier_ID debe ser mayor a 0.");

        RuleFor(x => x.OriginCity)
            .NotEmpty().WithMessage("La ciudad de origen es obligatoria.")
            .MaximumLength(100);

        RuleFor(x => x.DestinationCity)
            .NotEmpty().WithMessage("La ciudad de destino es obligatoria.")
            .MaximumLength(100);

        RuleFor(x => x.CostToEcommerce)
            .GreaterThan(0).WithMessage("El costo al ecommerce debe ser mayor a L. 0.00.")
            .PrecisionScale(18, 2, false).WithMessage("El costo en Lempiras (LPS) debe tener máximo 2 decimales.");

        RuleFor(x => x.PriceToCustomer)
            .GreaterThan(0).WithMessage("El precio al cliente debe ser mayor a L. 0.00.")
            .PrecisionScale(18, 2, false).WithMessage("El precio en Lempiras (LPS) debe tener máximo 2 decimales.");

        RuleFor(x => x)
            .Must(x => x.PriceToCustomer >= x.CostToEcommerce)
            .WithMessage("El precio al cliente no puede ser menor que el costo al ecommerce (no se puede operar a pérdida).");
    }
}
