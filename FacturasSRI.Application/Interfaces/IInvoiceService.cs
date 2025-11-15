using FacturasSRI.Application.Dtos;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FacturasSRI.Application.Interfaces
{
    public interface IInvoiceService
    {
        Task<List<InvoiceDto>> GetInvoicesAsync();
        Task<InvoiceDto?> GetInvoiceByIdAsync(Guid id);
        Task<InvoiceDto> CreateInvoiceAsync(CreateInvoiceDto invoiceDto);
        Task<InvoiceDetailViewDto?> GetInvoiceDetailByIdAsync(Guid id);
        Task<InvoiceDetailViewDto?> CheckSriStatusAsync(Guid invoiceId);
    }
}