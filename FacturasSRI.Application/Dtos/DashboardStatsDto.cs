namespace FacturasSRI.Application.Dtos
{
    public class DashboardStatsDto
    {
        public int TotalFacturasEmitidas { get; set; }
        public int TotalClientesRegistrados { get; set; }
        public decimal IngresosEsteMes { get; set; }
    }
}