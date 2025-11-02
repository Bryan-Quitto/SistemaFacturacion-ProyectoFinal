using System;

namespace FacturasSRI.Domain.Entities
{
    public class PrecioEspecial
    {
        public Guid Id { get; set; }
        public Guid ProductoId { get; set; }
        public Guid? ClienteId { get; set; }
        public decimal Precio { get; set; }
    }
}