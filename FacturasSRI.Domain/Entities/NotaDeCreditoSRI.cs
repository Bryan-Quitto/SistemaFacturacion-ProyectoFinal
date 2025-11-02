using System;

namespace FacturasSRI.Domain.Entities
{
    public class NotaDeCreditoSRI
    {
        public Guid NotaDeCreditoId { get; set; }
        public virtual NotaDeCredito NotaDeCredito { get; set; } = null!;
        public string? XmlGenerado { get; set; }
        public string? XmlFirmado { get; set; }
        public string? ClaveAcceso { get; set; }
        public string? NumeroAutorizacion { get; set; }
        public DateTime? FechaAutorizacion { get; set; }
        public string? RespuestaSRI { get; set; }
        public string? RideUrl { get; set; }
    }
}