using FacturasSRI.Domain.Enums;
using System;

namespace FacturasSRI.Domain.Entities
{
    public class AjusteInventario
    {
        public Guid Id { get; set; }
        public Guid LoteId { get; set; }
        public virtual Lote Lote { get; set; } = null!;
        public int CantidadAjustada { get; set; }
        public TipoAjusteInventario Tipo { get; set; }
        public string Motivo { get; set; } = string.Empty;
        public DateTime Fecha { get; set; }
        public Guid UsuarioIdAutoriza { get; set; }
    }
}