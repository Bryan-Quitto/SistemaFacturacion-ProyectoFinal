using System;

namespace FacturasSRI.Application.Dtos
{
    public class LoteDto
    {
        public Guid Id { get; set; }
        public int CantidadDisponible { get; set; }
        public decimal PrecioCompraUnitario { get; set; }
        public DateTime? FechaCaducidad { get; set; }
    }
}