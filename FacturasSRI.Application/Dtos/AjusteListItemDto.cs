using FacturasSRI.Domain.Enums;
using System;

namespace FacturasSRI.Application.Dtos
{
    public class AjusteListItemDto
    {
        public DateTime Fecha { get; set; }
        public string ProductoNombre { get; set; } = string.Empty;
        public TipoAjusteInventario Tipo { get; set; }
        public int CantidadAjustada { get; set; }
        public string Motivo { get; set; } = string.Empty;
        public string UsuarioAutoriza { get; set; } = string.Empty;
    }
}