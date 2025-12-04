using System;

namespace FacturasSRI.Application.Dtos.Reports
{
    public class ComprasPorPeriodoDto
    {
        public DateTime Fecha { get; set; }
        public string ProductoNombre { get; set; } = string.Empty;
        public string NombreProveedor { get; set; } = string.Empty;
        public string? NumeroFacturaProveedor { get; set; }
        public int NumeroCompraInterno { get; set; }
        public decimal CantidadComprada { get; set; }
        public decimal CostoUnitario { get; set; }
        public decimal CostoTotal { get; set; }
        public string UsuarioResponsable { get; set; } = string.Empty;
    }
}