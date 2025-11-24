using FacturasSRI.Domain.Enums;
using System;

namespace FacturasSRI.Application.Dtos
{
    public class PurchaseListItemDto
    {
        public Guid Id { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string NombreProveedor { get; set; } = string.Empty;
        public int Cantidad { get; set; }
        public decimal MontoTotal { get; set; }
        public EstadoCompra Estado { get; set; }
        public DateTime FechaEmision { get; set; }
        public DateTime? FechaVencimiento { get; set; }
        public DateTime? FechaPago { get; set; }
        public string? FacturaCompraPath { get; set; }
        public string? ComprobantePagoPath { get; set; }
        
        // NUEVO CAMPO AGREGADO
        public string? NotaCreditoPath { get; set; }
        
        public FormaDePago FormaDePago { get; set; }
    }
}