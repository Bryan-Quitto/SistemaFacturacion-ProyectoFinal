using System;

namespace FacturasSRI.Application.Dtos
{
    public class PurchaseListItemDto
    {
        public Guid CuentaPorPagarId { get; set; }
        public Guid LoteId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public int CantidadComprada { get; set; }
        public int CantidadDisponible { get; set; }
        public decimal PrecioCompraUnitario { get; set; }
        public decimal ValorTotalCompra { get; set; }
        public DateTime FechaCompra { get; set; }
        public DateTime? FechaCaducidad { get; set; }
        public string NombreProveedor { get; set; } = string.Empty;
        public string? ComprobantePath { get; set; }
        public string CreadoPor { get; set; } = string.Empty;
    }
}