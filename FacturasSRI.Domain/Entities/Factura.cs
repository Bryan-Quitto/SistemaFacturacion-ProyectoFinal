using FacturasSRI.Domain.Enums;
using System;
using System.Collections.Generic;

namespace FacturasSRI.Domain.Entities
{
    public class Factura
    {
        public Guid Id { get; set; }
        public DateTime FechaEmision { get; set; }
        public string NumeroFactura { get; set; } = string.Empty;
        public EstadoFactura Estado { get; set; }
        public FormaDePago FormaDePago { get; set; }
        public int? DiasCredito { get; set; }
        public decimal MontoAbonoInicial { get; set; }
        public Guid? ClienteId { get; set; }
        public virtual Cliente Cliente { get; set; } = null!;
        public decimal SubtotalSinImpuestos { get; set; }
        public decimal TotalDescuento { get; set; }
        public decimal TotalIVA { get; set; }
        public decimal Total { get; set; }
        public Guid UsuarioIdCreador { get; set; }
        public DateTime FechaCreacion { get; set; }
        public virtual ICollection<FacturaDetalle> Detalles { get; set; } = new List<FacturaDetalle>();
        public virtual ICollection<Cobro> Cobros { get; set; } = new List<Cobro>();
        public virtual FacturaSRI? InformacionSRI { get; set; }
    }
}