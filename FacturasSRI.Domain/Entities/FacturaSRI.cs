using System;

namespace FacturasSRI.Domain.Entities
{
    public class FacturaSRI
    {
        public Guid FacturaId { get; set; }
        public virtual Factura Factura { get; set; } = null!;
        public string? XmlGenerado { get; set; }
        public string? XmlFirmado { get; set; }
        public string? ClaveAcceso { get; set; }
        public string? NumeroAutorizacion { get; set; }
        public DateTime? FechaAutorizacion { get; set; }
        public string? RespuestaSRI { get; set; }
        public string? RideUrl { get; set; }
    }
}