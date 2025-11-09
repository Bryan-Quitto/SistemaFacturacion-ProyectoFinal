using System;

namespace FacturasSRI.Application.Dtos
{
    public class PurchaseListItemDto
    {
        public Guid LoteId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public int CantidadComprada { get; set; }
        public int CantidadDisponible { get; set; }
        public decimal PrecioCompraUnitario { get; set; }
        public decimal ValorTotalCompra { get; set; }
        public DateTime FechaCompra { get; set; }
        public DateTime? FechaCaducidad { get; set; }
        public string Proveedor { get; set; } = string.Empty;
    }
}