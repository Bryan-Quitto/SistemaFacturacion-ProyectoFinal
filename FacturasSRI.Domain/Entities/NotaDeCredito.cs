using FacturasSRI.Domain.Enums;
using System;
using System.Collections.Generic;

namespace FacturasSRI.Domain.Entities
{
    public class NotaDeCredito
    {
        public Guid Id { get; set; }
        public Guid FacturaId { get; set; }
        public virtual Factura Factura { get; set; } = null!;
        public Guid ClienteId { get; set; }
        public virtual Cliente Cliente { get; set; } = null!;
        public string NumeroNotaCredito { get; set; } = string.Empty;
        public DateTime FechaEmision { get; set; }
        public EstadoNotaDeCredito Estado { get; set; }
        public string RazonModificacion { get; set; } = string.Empty;
        public decimal SubtotalSinImpuestos { get; set; }
        public decimal TotalIVA { get; set; }
        public decimal Total { get; set; }
        public Guid UsuarioIdCreador { get; set; }
        public DateTime FechaCreacion { get; set; }
        public virtual ICollection<NotaDeCreditoDetalle> Detalles { get; set; } = new List<NotaDeCreditoDetalle>();
        public virtual NotaDeCreditoSRI? InformacionSRI { get; set; }
    }
}