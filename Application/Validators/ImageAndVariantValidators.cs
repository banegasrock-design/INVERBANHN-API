using FluentValidation;
using Application.DTOs.Product;

namespace Application.Validators;

public class AddProductImageRequestValidator : AbstractValidator<AddProductImageRequest>
{
    private static readonly string[] AllowedMimeTypes = { "image/jpeg", "image/png", "image/webp", "image/gif" };

    public AddProductImageRequestValidator()
    {
        RuleFor(x => x.ImageUrl)
            .NotEmpty().WithMessage("La URL de la imagen es obligatoria.")
            .MaximumLength(2048).WithMessage("La URL no puede exceder los 2048 caracteres.")
            .Must(url => Uri.TryCreate(url, UriKind.Absolute, out _))
            .WithMessage("La URL de la imagen debe ser una URL válida (https://...).");

        RuleFor(x => x.MimeType)
            .Must(mime => mime == null || AllowedMimeTypes.Contains(mime))
            .WithMessage("El tipo de imagen debe ser: image/jpeg, image/png, image/webp o image/gif.");

        RuleFor(x => x.ImageSizeBytes)
            .LessThanOrEqualTo(5 * 1024 * 1024) // 5 MB máx
            .When(x => x.ImageSizeBytes.HasValue)
            .WithMessage("El archivo de imagen no puede superar los 5 MB.");

        RuleFor(x => x.DisplayOrder)
            .GreaterThanOrEqualTo(0).WithMessage("El orden de visualización debe ser 0 o mayor.");
    }
}

public class CreateVariantRequestValidator : AbstractValidator<CreateVariantRequest>
{
    public CreateVariantRequestValidator()
    {
        RuleFor(x => x.SKU)
            .NotEmpty().WithMessage("El SKU de la variante es obligatorio.")
            .MaximumLength(100).WithMessage("El SKU no puede exceder los 100 caracteres.")
            .Matches(@"^[A-Za-z0-9\-_]+$").WithMessage("El SKU solo puede contener letras, números, guiones y guiones bajos.");

        RuleFor(x => x.VariantName)
            .NotEmpty().WithMessage("El nombre de la variante es obligatorio.")
            .MaximumLength(200).WithMessage("El nombre no puede exceder los 200 caracteres.");

        RuleFor(x => x.PriceAmount)
            .GreaterThan(0).WithMessage("El precio de la variante debe ser mayor a L. 0.00.")
            .PrecisionScale(18, 2, false).WithMessage("El precio en Lempiras (LPS) acepta máximo 2 decimales.");

        RuleFor(x => x.WeightKg)
            .GreaterThan(0).WithMessage("El peso debe ser mayor a 0 kg.")
            .LessThanOrEqualTo(500).WithMessage("El peso no puede exceder los 500 kg.")
            .PrecisionScale(10, 3, false).WithMessage("El peso acepta máximo 3 decimales.")
            .When(x => x.WeightKg.HasValue);
    }
}
