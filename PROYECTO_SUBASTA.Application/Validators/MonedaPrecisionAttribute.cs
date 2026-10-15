using System;
using System.ComponentModel.DataAnnotations;
using System.Globalization;

namespace PROYECTO_SUBASTA.Application.Validators
{
    /// <summary>
    /// Valida que un valor monetario sea mayor a cero, no exceda el límite máximo configurado
    /// y contenga como máximo dos cifras decimales.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, AllowMultiple = false)]
    public class MonedaPrecisionAttribute : ValidationAttribute
    {
        public decimal LimiteMaximo { get; set; }
        public decimal LimiteMinimo { get; set; }

        public MonedaPrecisionAttribute(double limiteMaximo = 100_000_000.0, double limiteMinimo = 0.01)
        {
            LimiteMaximo = (decimal)limiteMaximo;
            LimiteMinimo = (decimal)limiteMinimo;
        }

        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            if (value == null)
            {
                return ValidationResult.Success;
            }

            decimal monto;
            if (value is decimal d)
            {
                monto = d;
            }
            else if (decimal.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
            {
                monto = parsed;
            }
            else
            {
                return new ValidationResult(ErrorMessage ?? $"El campo {validationContext.DisplayName} no es un número válido.");
            }

            var memberNames = string.IsNullOrEmpty(validationContext.MemberName)
                ? Array.Empty<string>()
                : new[] { validationContext.MemberName };

            if (monto < LimiteMinimo)
            {
                return new ValidationResult(ErrorMessage ?? $"El campo {validationContext.DisplayName} debe ser mayor o igual a {LimiteMinimo}.", memberNames);
            }

            if (monto > LimiteMaximo)
            {
                return new ValidationResult(ErrorMessage ?? $"El campo {validationContext.DisplayName} no puede superar el límite permitido de {LimiteMaximo:N2}.", memberNames);
            }

            if (decimal.Round(monto, 2) != monto)
            {
                return new ValidationResult(ErrorMessage ?? $"El campo {validationContext.DisplayName} no puede tener más de dos decimales.", memberNames);
            }

            return ValidationResult.Success;
        }
    }
}
