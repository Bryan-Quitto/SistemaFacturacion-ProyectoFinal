using FacturasSRI.Domain.Enums;
using System;
using System.ComponentModel.DataAnnotations;

namespace FacturasSRI.Application.Dtos
{
    public class AjusteInventarioDto
    {
        public Guid? LoteId { get; set; }
        
        [Required]
        public Guid ProductoId { get; set; }

        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "La cantidad debe ser mayor que cero.")]
        public int CantidadAjustada { get; set; }

        [Required]
        public TipoAjusteInventario Tipo { get; set; }

        [Required(ErrorMessage = "El motivo es obligatorio.")]
        public string Motivo { get; set; } = string.Empty;
        
        public Guid UsuarioIdAutoriza { get; set; }
    }
}