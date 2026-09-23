using FluentValidation;

namespace Application.Validators;

/// <summary>
/// Validadores estáticos reutilizables para formatos hondureños (RTN y CAI).
/// </summary>
public static class HondurasFormats
{
    /// <summary>
    /// RTN Hondureño: exactamente 14 dígitos numéricos.
    /// Ejemplo válido: 08019000123456
    /// </summary>
    public const string RtnPattern = @"^\d{14}$";
    public const string RtnMessage = "El RTN debe contener exactamente 14 dígitos numéricos (ej. 08019000123456).";

    /// <summary>
    /// CAI Hondureño: formato alfanumérico con guiones, 37 caracteres.
    /// Ejemplo válido: A1B2C3-D4E5F6-G7H8I9-J0K1L2-M3N4O5-P6
    /// </summary>
    public const string CaiPattern = @"^[A-Z0-9]{6}-[A-Z0-9]{6}-[A-Z0-9]{6}-[A-Z0-9]{6}-[A-Z0-9]{6}-[A-Z0-9]{2}$";
    public const string CaiMessage = "El CAI debe tener el formato XXXXXX-XXXXXX-XXXXXX-XXXXXX-XXXXXX-XX (37 caracteres alfanuméricos con guiones).";
}

/// <summary>
/// Extensiones de FluentValidation para validar RTN y CAI hondureños.
/// </summary>
public static class HondurasValidationExtensions
{
    public static IRuleBuilderOptions<T, string> MustBeValidRtn<T>(this IRuleBuilder<T, string> ruleBuilder)
    {
        return ruleBuilder
            .Matches(HondurasFormats.RtnPattern).WithMessage(HondurasFormats.RtnMessage);
    }

    public static IRuleBuilderOptions<T, string?> MustBeValidCai<T>(this IRuleBuilder<T, string?> ruleBuilder)
    {
        return ruleBuilder
            .Matches(HondurasFormats.CaiPattern).WithMessage(HondurasFormats.CaiMessage);
    }
}
