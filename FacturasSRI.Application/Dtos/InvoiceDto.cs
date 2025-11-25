using System;
using System.Collections.Generic;
using FacturasSRI.Domain.Enums;

namespace FacturasSRI.Application.Dtos
{
    public class InvoiceDto
    {
        public Guid Id { get; set; }
        public DateTime FechaEmision { get; set; }
        public string NumeroFactura { get; set; } = string.Empty;
        public EstadoFactura Estado { get; set; } // Added
        public Guid? ClienteId { get; set; }
        public string ClienteNombre { get; set; } = string.Empty;
        public decimal SubtotalSinImpuestos { get; set; }
        public decimal TotalDescuento { get; set; }
        public decimal TotalIVA { get; set; }
        public decimal Total { get; set; }
        public string CreadoPor { get; set; } = string.Empty;
        public List<InvoiceDetailDto> Detalles { get; set; } = new();
        public FormaDePago FormaDePago { get; set; }
        public int? DiasCredito { get; set; }
        public decimal MontoAbonoInicial { get; set; }
        public decimal SaldoPendiente { get; set; }
        public DateTime? FechaVencimiento { get; set; }
    }
}
