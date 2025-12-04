using System;
using System.Collections.Generic;

namespace FacturasSRI.Application.Dtos
{
    public class DashboardStatsDto
    {
        // Métricas de Ventas (Admin / Vendedor)
        public int TotalFacturasEmitidas { get; set; }
        public int TotalClientesRegistrados { get; set; }
        public decimal IngresosEsteMes { get; set; } // Para Admin es Global, para Vendedor es Personal
        
        // Métricas de Bodega (Admin / Bodeguero)
        public int TotalProductosBajoStock { get; set; }
        public int TotalComprasMes { get; set; }

        // Listas
        public List<RecentInvoiceDto> RecentInvoices { get; set; } = new();
        public List<RecentCreditNoteDto> RecentCreditNotes { get; set; } = new();
        public List<TopProductDto> TopProducts { get; set; } = new();
        
        // Nueva lista para Bodega
        public List<LowStockProductWidgetDto> LowStockProducts { get; set; } = new();
    }

    public class RecentInvoiceDto
    {
        public Guid Id { get; set; }
        public string NumeroFactura { get; set; } = string.Empty;
        public string ClienteNombre { get; set; } = string.Empty;
        public DateTime FechaEmision { get; set; }
        public decimal Total { get; set; }
        public string Estado { get; set; } = string.Empty;
    }

    public class RecentCreditNoteDto
    {
        public Guid Id { get; set; }
        public string NumeroNotaCredito { get; set; } = string.Empty;
        public string ClienteNombre { get; set; } = string.Empty;
        public DateTime FechaEmision { get; set; }
        public decimal Total { get; set; }
        public string Estado { get; set; } = string.Empty;
    }

    public class TopProductDto
    {
        public string ProductName { get; set; } = string.Empty;
        public int QuantitySold { get; set; }
        public decimal TotalRevenue { get; set; }
    }

    public class LowStockProductWidgetDto
    {
        public string ProductName { get; set; } = string.Empty;
        public decimal StockActual { get; set; }
        public decimal StockMinimo { get; set; }
    }
}