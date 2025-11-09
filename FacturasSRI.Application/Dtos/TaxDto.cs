using System;
using System.ComponentModel.DataAnnotations;

namespace FacturasSRI.Application.Dtos
{
    public class TaxDto
    {
        public Guid Id { get; set; }

        [Required]
        public string Nombre { get; set; } = string.Empty;

        [Required]
        public string CodigoSRI { get; set; } = string.Empty;

        [Range(0, 100, ErrorMessage = "El porcentaje debe estar entre 0 y 100.")]
        public decimal Porcentaje { get; set; }
        public bool EstaActivo { get; set; } = true;
    }
}