using System;
using FacturasSRI.Domain.Enums;

namespace FacturasSRI.Application.Dtos
{
    public class FacturasConPagosDto
    {
        public Guid FacturaId { get; set; }
        public string NumeroFactura { get; set; } = string.Empty;
        public string ClienteNombre { get; set; } = string.Empty;
        public decimal TotalFactura { get; set; }
        public decimal SaldoPendiente { get; set; }
        public decimal TotalPagado { get; set; }
        public FormaDePago FormaDePago { get; set; }
        public EstadoFactura EstadoFactura { get; set; }
    }
}
