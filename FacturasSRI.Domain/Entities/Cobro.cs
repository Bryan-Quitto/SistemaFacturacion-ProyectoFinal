using System;

namespace FacturasSRI.Domain.Entities
{
    public class Cobro
    {
        public Guid Id { get; set; }
        public Guid FacturaId { get; set; }
        public virtual Factura Factura { get; set; } = null!;
        public DateTime FechaCobro { get; set; }
        public decimal Monto { get; set; }
        public string MetodoDePago { get; set; } = string.Empty;
        public string? Referencia { get; set; }
        public string? ComprobantePagoPath { get; set; }
        
        public Guid? UsuarioIdCreador { get; set; } 
        public virtual Usuario? UsuarioCreador { get; set; } 

        public DateTime FechaCreacion { get; set; }
    }
}