using System;
using System.Collections.Generic;

namespace FacturasSRI.Application.Dtos
{
    public class CreateInvoiceDto
    {
        public Guid ClienteId { get; set; }
        public List<InvoiceItemDto> Items { get; set; } = new();
    }
}