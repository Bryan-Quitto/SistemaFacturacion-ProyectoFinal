namespace FacturasSRI.Application.Dtos.Reports
{
    public class VentasPorPeriodoDto
    {
        public DateTime Fecha { get; set; }
        public string Vendedor { get; set; } = string.Empty;
        public int CantidadFacturas { get; set; }
        public decimal Subtotal { get; set; }
        public decimal TotalIva { get; set; }
        public decimal Total { get; set; }
    }
}
