using System;
using System.Collections.Generic;

namespace FacturasSRI.Domain.Entities
{
    public class CuentaPorCobrar
    {
        public Guid Id { get; set; }
        public Guid FacturaId { get; set; }
        public virtual Factura Factura { get; set; } = null!;
        public Guid? ClienteId { get; set; }
        public virtual Cliente Cliente { get; set; } = null!;
        public DateTime FechaEmision { get; set; }
        public DateTime FechaVencimiento { get; set; }
        public decimal MontoTotal { get; set; }
        public decimal SaldoPendiente { get; set; }
        public bool Pagada { get; set; }
        public Guid UsuarioIdCreador { get; set; }
        public DateTime FechaCreacion { get; set; }
    }
}