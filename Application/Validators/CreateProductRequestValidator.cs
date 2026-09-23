using FluentValidation;
using Application.DTOs.Product;

namespace Application.Validators;

public class CreateProductRequestValidator : AbstractValidator<CreateProductRequest>
{
    public CreateProductRequestValidator()
    {
        // ── Identificación ──

        RuleFor(x => x.SKU)
            .NotEmpty().WithMessage("El SKU del producto es obligatorio.")
            .MaximumLength(50).WithMessage("El SKU no puede exceder los 50 caracteres.")
            .Matches(@"^[A-Za-z0-9\-_]+$").WithMessage("El SKU solo puede contener letras, números, guiones y guiones bajos.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("El nombre del producto es obligatorio.")
            .MaximumLength(200).WithMessage("El nombre no puede exceder los 200 caracteres.");

        RuleFor(x => x.CategoryId)
            .GreaterThan(0).WithMessage("Debe seleccionar una categoría válida.");

        // ── Dimensiones (críticas para el motor de cotización de paqueteras) ──

        RuleFor(x => x.WeightKg)
            .GreaterThan(0).WithMessage("El peso en kg es obligatorio y debe ser mayor a 0.")
            .LessThanOrEqualTo(500).WithMessage("El peso no puede exceder los 500 kg.")
            .PrecisionScale(10, 3, false).WithMessage("El peso acepta máximo 3 decimales (ej. 2.500 kg).");

        RuleFor(x => x.WidthCm)
            .GreaterThan(0).WithMessage("El ancho en cm es obligatorio para calcular el envío.")
            .LessThanOrEqualTo(300).WithMessage("El ancho no puede exceder los 300 cm.")
            .PrecisionScale(10, 2, false).WithMessage("El ancho acepta máximo 2 decimales.");

        RuleFor(x => x.HeightCm)
            .GreaterThan(0).WithMessage("El alto en cm es obligatorio para calcular el envío.")
            .LessThanOrEqualTo(300).WithMessage("El alto no puede exceder los 300 cm.")
            .PrecisionScale(10, 2, false).WithMessage("El alto acepta máximo 2 decimales.");

        RuleFor(x => x.LengthCm)
            .GreaterThan(0).WithMessage("El largo en cm es obligatorio para calcular el envío.")
            .LessThanOrEqualTo(300).WithMessage("El largo no puede exceder los 300 cm.")
            .PrecisionScale(10, 2, false).WithMessage("El largo acepta máximo 2 decimales.");
    }
}
