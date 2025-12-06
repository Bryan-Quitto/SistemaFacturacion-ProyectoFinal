using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace FacturasSRI.Application.Dtos
{
    public class RegistrarCobroDto : IValidatableObject
    {
        public Guid FacturaId { get; set; }

        [Required(ErrorMessage = "El monto es obligatorio.")]
        [Range(0.01, double.MaxValue, ErrorMessage = "El monto debe ser mayor a cero.")]
        public decimal Monto { get; set; }

        [Required(ErrorMessage = "El método de pago es obligatorio.")]
        public string MetodoDePago { get; set; } = string.Empty;

        public string? Referencia { get; set; }

        public Guid? UsuarioIdCreador { get; set; }

        public DateTime FechaCobro { get; set; } = DateTime.UtcNow;

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (MetodoDePago == "Transferencia" || MetodoDePago == "Cheque")
            {
                if (string.IsNullOrWhiteSpace(Referencia))
                {
                    yield return new ValidationResult("El número de referencia es obligatorio para este método de pago.", new[] { nameof(Referencia) });
                }
            }
            else if (MetodoDePago == "Tarjeta de Crédito/Débito" || MetodoDePago == "Otro")
            {
                if (string.IsNullOrWhiteSpace(Referencia))
                {
                    yield return new ValidationResult("La referencia es obligatoria para este método de pago.", new[] { nameof(Referencia) });
                }
            }
        }
    }
}