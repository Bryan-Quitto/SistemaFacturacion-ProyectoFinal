namespace FacturasSRI.Application.Dtos.Reports
{
    public class AjusteInventarioReportDto
    {
        public DateTime Fecha { get; set; }
        public string ProductoNombre { get; set; } = string.Empty;
        public string TipoAjuste { get; set; } = string.Empty;
        public int CantidadAjustada { get; set; }
        public string Motivo { get; set; } = string.Empty;
    }
}
