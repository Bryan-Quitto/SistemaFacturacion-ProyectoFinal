using System;

namespace FacturasSRI.Domain.Entities
{
    public class Secuencial
    {
        public Guid Id { get; set; }
        public string Establecimiento { get; set; } = string.Empty;
        public string PuntoEmision { get; set; } = string.Empty;
        public int UltimoSecuencialFactura { get; set; }
    }
}