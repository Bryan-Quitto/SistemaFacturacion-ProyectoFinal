using System;

namespace FacturasSRI.Domain.Entities
{
    public class ProductoComponente
    {
        public Guid Id { get; set; }
        public Guid ProductoKitId { get; set; }
        public virtual Producto ProductoKit { get; set; } = null!;
        public Guid ProductoComponenteId { get; set; }
        public virtual Producto ProductoComponenteItem { get; set; } = null!;
        public int Cantidad { get; set; }
    }
}