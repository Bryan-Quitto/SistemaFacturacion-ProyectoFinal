using System;

namespace FacturasSRI.Application.Dtos
{
    public class CobroDto
    {
        public Guid Id { get; set; }
        public Guid FacturaId { get; set; }
        public string NumeroFactura { get; set; } = string.Empty;
        public string ClienteNombre { get; set; } = string.Empty;
        public DateTime FechaCobro { get; set; }
        public decimal Monto { get; set; }
        public string MetodoDePago { get; set; } = string.Empty;
        public string? Referencia { get; set; }
        public string CreadoPor { get; set; } = string.Empty;
        public string? ComprobantePagoPath { get; set; }
    }
}
