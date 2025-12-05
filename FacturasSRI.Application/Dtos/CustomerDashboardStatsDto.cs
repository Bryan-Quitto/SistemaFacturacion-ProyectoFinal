using System;

namespace FacturasSRI.Application.Dtos
{
    public class CustomerDashboardStatsDto
    {
        public decimal SaldoPendiente { get; set; }
        public int FacturasPendientes { get; set; }
        public decimal TotalCompradoHistorico { get; set; }
        public int TotalFacturasHistorico { get; set; }
        
        // Datos de la última factura para mostrar un widget rápido
        public Guid? UltimaFacturaId { get; set; }
        public string UltimaFacturaNumero { get; set; } = string.Empty;
        public decimal UltimaFacturaTotal { get; set; }
        public DateTime? UltimaFacturaFecha { get; set; }
    }
}