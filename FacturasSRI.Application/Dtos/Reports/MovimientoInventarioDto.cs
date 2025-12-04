using System;

namespace FacturasSRI.Application.Dtos.Reports
{
    public class MovimientoInventarioDto
    {
        public DateTime Fecha { get; set; }
        public string ProductoNombre { get; set; } = string.Empty;
        public string TipoMovimiento { get; set; } = string.Empty;
        public string DocumentoReferencia { get; set; } = string.Empty;
        public int Entrada { get; set; }
        public int Salida { get; set; }
        public string UsuarioResponsable { get; set; } = string.Empty;
    }
}