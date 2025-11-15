using System;
using System.Collections.Generic;
using FacturasSRI.Domain.Enums;

namespace FacturasSRI.Application.Dtos
{
    public class InvoiceDetailViewDto
    {
        public Guid Id { get; set; }
        public string NumeroFactura { get; set; } = string.Empty;
        public DateTime FechaEmision { get; set; }
        public string ClienteNombre { get; set; } = string.Empty;
        public string ClienteIdentificacion { get; set; } = string.Empty;
        public string ClienteDireccion { get; set; } = string.Empty;
        public string ClienteEmail { get; set; } = string.Empty;
        public decimal SubtotalSinImpuestos { get; set; }
        public decimal TotalIVA { get; set; }
        public decimal Total { get; set; }
        public List<InvoiceItemDetailDto> Items { get; set; } = new();
        public List<TaxSummary> TaxSummaries { get; set; } = new();

        public EstadoFactura Estado { get; set; }
        public string? ClaveAcceso { get; set; }
        public string? NumeroAutorizacion { get; set; }
        public string? RespuestaSRI { get; set; }
    }

    public class InvoiceItemDetailDto
    {
        public Guid ProductoId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public int Cantidad { get; set; }
        public decimal PrecioVentaUnitario { get; set; }
        public decimal Subtotal { get; set; }
        public List<TaxDto> Taxes { get; set; } = new();
    }
    
    public class TaxSummary
    {
        public string TaxName { get; set; } = string.Empty;
        public decimal TaxRate { get; set; }
        public decimal Amount { get; set; }
    }
}