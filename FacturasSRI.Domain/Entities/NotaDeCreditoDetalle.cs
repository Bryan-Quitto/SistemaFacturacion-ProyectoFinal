using System;

namespace FacturasSRI.Domain.Entities
{
    public class NotaDeCreditoDetalle
    {
        public Guid Id { get; set; }
        public Guid NotaDeCreditoId { get; set; }
        public virtual NotaDeCredito NotaDeCredito { get; set; } = null!;
        public Guid ProductoId { get; set; }
        public virtual Producto Producto { get; set; } = null!;
        public int Cantidad { get; set; }
        public decimal PrecioVentaUnitario { get; set; }
        public decimal DescuentoAplicado { get; set; }
        public decimal Subtotal { get; set; }
        public decimal ValorIVA { get; set; }
    }
}