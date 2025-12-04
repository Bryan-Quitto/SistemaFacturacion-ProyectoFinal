namespace FacturasSRI.Application.Dtos.Reports
{
    public class StockActualDto
    {
        public string? CodigoPrincipal { get; set; }
        public string NombreProducto { get; set; } = string.Empty;
        public string Categoria { get; set; } = string.Empty;
        public int StockTotal { get; set; }
        public decimal PrecioCompraPromedioPonderado { get; set; }
        public decimal ValorInventario { get; set; }
    }
}