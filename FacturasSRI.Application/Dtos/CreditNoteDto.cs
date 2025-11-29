using FacturasSRI.Domain.Enums;
using System;

namespace FacturasSRI.Application.Dtos
{
    public class CreditNoteDto
    {
        public Guid Id { get; set; }
        public string NumeroNotaCredito { get; set; } = string.Empty;
        public string NumeroFacturaModificada { get; set; } = string.Empty;
        public string ClienteNombre { get; set; } = string.Empty;
        public DateTime FechaEmision { get; set; }
        public decimal Total { get; set; }
        public EstadoNotaDeCredito Estado { get; set; }
        public required string RazonModificacion { get; set; }
    }
}