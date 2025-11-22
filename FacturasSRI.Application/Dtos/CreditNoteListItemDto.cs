using FacturasSRI.Domain.Enums;
using System;

namespace FacturasSRI.Application.Dtos
{
    public class CreditNoteListItemDto
    {
        public Guid Id { get; set; }
        public string NumeroNotaCredito { get; set; }
        public DateTime FechaEmision { get; set; }
        public string ClienteNombre { get; set; }
        public string NumeroFacturaAfectada { get; set; }
        public decimal Total { get; set; }
        public EstadoNotaDeCredito Estado { get; set; }
    }
}
