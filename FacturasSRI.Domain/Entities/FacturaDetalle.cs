using System;
using System.Collections.Generic;

namespace FacturasSRI.Domain.Entities
{
    public class FacturaDetalle
    {
        public Guid Id { get; set; }
        public Guid FacturaId { get; set; }
        public virtual Factura Factura { get; set; } = null!;
        public Guid ProductoId { get; set; }
        public virtual Producto Producto { get; set; } = null!;
        public int Cantidad { get; set; }
        public decimal PrecioVentaUnitario { get; set; }
        public decimal Descuento { get; set; }
        public decimal Subtotal { get; set; }
        public decimal ValorIVA { get; set; }
        public decimal CostoTotal { get; set; }
        public virtual ICollection<FacturaDetalleConsumoLote> ConsumosDeLote { get; set; } = new List<FacturaDetalleConsumoLote>();
    }
}