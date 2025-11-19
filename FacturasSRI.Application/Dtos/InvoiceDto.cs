using System;
using System.Collections.Generic;
using FacturasSRI.Domain.Enums; // Added

namespace FacturasSRI.Application.Dtos
{
    public class InvoiceDto
    {
        public Guid Id { get; set; }
        public DateTime FechaEmision { get; set; }
        public string NumeroFactura { get; set; } = string.Empty;
        public EstadoFactura Estado { get; set; } // Added
        public Guid? ClienteId { get; set; }
        public decimal SubtotalSinImpuestos { get; set; }
        public decimal TotalDescuento { get; set; }
        public decimal TotalIVA { get; set; }
        public decimal Total { get; set; }
        public string CreadoPor { get; set; } = string.Empty;
        public List<InvoiceDetailDto> Detalles { get; set; } = new();
    }
}
