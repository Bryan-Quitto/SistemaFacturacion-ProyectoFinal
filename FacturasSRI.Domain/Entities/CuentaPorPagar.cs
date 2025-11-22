using FacturasSRI.Domain.Enums;
using System;

namespace FacturasSRI.Domain.Entities
{
    public class CuentaPorPagar
    {
        public Guid Id { get; set; }
        public Guid ProductoId { get; set; }
        public virtual Producto Producto { get; set; } = null!;
        public Guid? LoteId { get; set; }
        public virtual Lote? Lote { get; set; }
        public string NombreProveedor { get; set; } = string.Empty;
        public string? FacturaCompraPath { get; set; }
        public string? ComprobantePagoPath { get; set; }
        public DateTime FechaEmision { get; set; }
        public DateTime? FechaVencimiento { get; set; }
        public decimal MontoTotal { get; set; }
        public int Cantidad { get; set; }
        public EstadoCompra Estado { get; set; }
        public FormaDePago FormaDePago { get; set; } // Corrected: using FormaDePago instead of TipoDePago
        public DateTime? FechaPago { get; set; }
        public Guid UsuarioIdCreador { get; set; }
        public DateTime FechaCreacion { get; set; }
    }
}