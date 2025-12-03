namespace FacturasSRI.Application.Dtos.Reports
{
    public class MovimientoInventarioDto
    {
        public DateTime Fecha { get; set; }
        public string ProductoNombre { get; set; } = string.Empty;
        public string TipoMovimiento { get; set; } = string.Empty; // e.g., "Venta", "Compra", "Ajuste"
        public string DocumentoReferencia { get; set; } = string.Empty; // e.g., Invoice number, Purchase order
        public int Cantidad { get; set; }
    }
}
