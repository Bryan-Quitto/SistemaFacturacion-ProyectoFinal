namespace FacturasSRI.Application.Dtos.Reports
{
    public class ComprasPorPeriodoDto
    {
        public string ProductoNombre { get; set; } = string.Empty;
        public decimal CantidadComprada { get; set; }
        public decimal CostoTotal { get; set; }
        public decimal CostoPromedio { get; set; }
    }
}
