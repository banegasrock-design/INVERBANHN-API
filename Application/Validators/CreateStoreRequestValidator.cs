using FluentValidation;
using Application.DTOs.Store;

namespace Application.Validators;

public class CreateStoreRequestValidator : AbstractValidator<CreateStoreRequest>
{
    private static readonly string[] ValidInventoryModes = { "Tienda", "Ecommerce" };
    private static readonly string[] ValidBillingTypes = { "Autoimpresor", "Managed" };

    public CreateStoreRequestValidator()
    {
        // ── Datos Legales ──

        RuleFor(x => x.StoreName)
            .NotEmpty().WithMessage("El nombre de la tienda es obligatorio.")
            .MaximumLength(100).WithMessage("El nombre no puede exceder los 100 caracteres.");

        RuleFor(x => x.RTN)
            .NotEmpty().WithMessage("El RTN es obligatorio.")
            .MustBeValidRtn();

        RuleFor(x => x.Address)
            .NotEmpty().WithMessage("La dirección fiscal es obligatoria.")
            .MaximumLength(250).WithMessage("La dirección no puede exceder los 250 caracteres.");

        // ── Configuración Operativa ──

        RuleFor(x => x.InventoryMode)
            .NotEmpty().WithMessage("El modo de inventario es obligatorio.")
            .Must(mode => ValidInventoryModes.Contains(mode))
            .WithMessage("El Inventory_Mode debe ser 'Tienda' o 'Ecommerce'.");

        RuleFor(x => x.BillingType)
            .NotEmpty().WithMessage("El tipo de facturación es obligatorio.")
            .Must(type => ValidBillingTypes.Contains(type))
            .WithMessage("El Billing_Type debe ser 'Autoimpresor' o 'Managed'.");

        // ── Configuración SAR (condicional: solo para Autoimpresores) ──

        When(x => x.BillingType == "Autoimpresor", () =>
        {
            RuleFor(x => x.CAI)
                .NotEmpty().WithMessage("El CAI es obligatorio para tiendas Autoimpresoras.")
                .MustBeValidCai();

            RuleFor(x => x.RangeStart)
                .NotNull().WithMessage("El rango inicial es obligatorio para tiendas Autoimpresoras.")
                .GreaterThan(0).WithMessage("El rango inicial debe ser mayor a 0.");

            RuleFor(x => x.RangeEnd)
                .NotNull().WithMessage("El rango final es obligatorio para tiendas Autoimpresoras.")
                .GreaterThan(0).WithMessage("El rango final debe ser mayor a 0.");

            RuleFor(x => x)
                .Must(x => x.RangeEnd > x.RangeStart)
                .When(x => x.RangeStart.HasValue && x.RangeEnd.HasValue)
                .WithMessage("El rango final debe ser mayor que el rango inicial.");

            RuleFor(x => x.CaiExpiryDate)
                .NotNull().WithMessage("La fecha de vencimiento del CAI es obligatoria para Autoimpresores.")
                .GreaterThan(DateTime.UtcNow.Date).WithMessage("La fecha de vencimiento del CAI no puede estar vencida.");
        });
    }
}
