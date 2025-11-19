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
        public string? ComprobantePath { get; set; }
        public DateTime FechaEmision { get; set; }
        public DateTime FechaVencimiento { get; set; }
        public decimal MontoTotal { get; set; }
        public int Cantidad { get; set; }
        public decimal SaldoPendiente { get; set; }
        public bool Pagada { get; set; }
        public Guid UsuarioIdCreador { get; set; }
        public DateTime FechaCreacion { get; set; }
    }
}