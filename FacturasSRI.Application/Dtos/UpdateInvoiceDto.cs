using FacturasSRI.Application.Dtos;
using FacturasSRI.Domain.Enums;
using System;
using System.Collections.Generic;

namespace FacturasSRI.Application.Dtos
{
    public class UpdateInvoiceDto
    {
        public Guid Id { get; set; }
        public Guid? ClienteId { get; set; }
        public Guid UsuarioIdModificador { get; set; }
        public List<InvoiceItemDto> Items { get; set; } = new();
        public FormaDePago FormaDePago { get; set; }
        public int? DiasCredito { get; set; }
        public decimal MontoAbonoInicial { get; set; }
        public bool EsConsumidorFinal { get; set; }
        
        // This flag will tell the service to issue the invoice to the SRI after saving
        public bool EmitirTrasGuardar { get; set; }
    }
}
