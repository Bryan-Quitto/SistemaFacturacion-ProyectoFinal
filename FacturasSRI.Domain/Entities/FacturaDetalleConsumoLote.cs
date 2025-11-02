using System;

namespace FacturasSRI.Domain.Entities
{
    public class FacturaDetalleConsumoLote
    {
        public Guid Id { get; set; }
        public Guid FacturaDetalleId { get; set; }
        public virtual FacturaDetalle FacturaDetalle { get; set; } = null!;
        public Guid LoteId { get; set; }
        public virtual Lote Lote { get; set; } = null!;
        public int CantidadConsumida { get; set; }
    }
}