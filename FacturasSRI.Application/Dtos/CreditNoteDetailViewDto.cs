using System;
using System.Collections.Generic;
using FacturasSRI.Domain.Enums;

namespace FacturasSRI.Application.Dtos
{
    public class CreditNoteDetailViewDto
    {
        public Guid Id { get; set; }
        public Guid? ClienteId { get; set; }
        public string? NumeroNotaCredito { get; set; }
        public DateTime FechaEmision { get; set; }
        public string ClienteNombre { get; set; } = string.Empty;
        public string ClienteIdentificacion { get; set; } = string.Empty;
        public string ClienteDireccion { get; set; } = string.Empty;
        public string ClienteEmail { get; set; } = string.Empty;

        // Datos Factura Modificada
        public Guid FacturaId { get; set; }
        public string NumeroFacturaModificada { get; set; } = string.Empty;
        public DateTime FechaEmisionFacturaModificada { get; set; }
        public string RazonModificacion { get; set; } = string.Empty;

        // Totales
        public decimal SubtotalSinImpuestos { get; set; }
        public decimal TotalIVA { get; set; }
        public decimal Total { get; set; }

        public List<CreditNoteItemDetailDto> Items { get; set; } = new();
        public List<TaxSummary> TaxSummaries { get; set; } = new();

        // SRI Info
        public string? ClaveAcceso { get; set; }
        public string? NumeroAutorizacion { get; set; }
        public string? RespuestaSRI { get; set; }
        public EstadoNotaDeCredito Estado { get; set; }
    }

    public class CreditNoteItemDetailDto
    {
        public Guid ProductoId { get; set; }
        public string ProductCode { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public int Cantidad { get; set; }
        public decimal PrecioVentaUnitario { get; set; }
        public decimal Subtotal { get; set; }
    }
}