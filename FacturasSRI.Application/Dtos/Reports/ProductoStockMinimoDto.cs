namespace FacturasSRI.Application.Dtos.Reports
{
    public class ProductoStockMinimoDto
    {
        public string CodigoPrincipal { get; set; } = string.Empty;
        public string NombreProducto { get; set; } = string.Empty;
        public int StockActual { get; set; }
        public int StockMinimo { get; set; }
        public int CantidadFaltante { get; set; }
        
        public decimal CostoPromedioUnitario { get; set; }
        public decimal CostoEstimadoReposicion { get; set; }
    }
}