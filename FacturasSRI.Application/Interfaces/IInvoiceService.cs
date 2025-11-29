using FacturasSRI.Application.Dtos;
using FacturasSRI.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FacturasSRI.Application.Interfaces
{
    public interface IInvoiceService
    {
        Task<PaginatedList<InvoiceDto>> GetInvoicesAsync(int pageNumber, int pageSize, string? searchTerm, EstadoFactura? status, FormaDePago? formaDePago, string? paymentStatus);
        Task<InvoiceDto?> GetInvoiceByIdAsync(Guid id);
        Task<InvoiceDto> CreateInvoiceAsync(CreateInvoiceDto invoiceDto);
        Task<InvoiceDetailViewDto?> GetInvoiceDetailByIdAsync(Guid id);
        Task<InvoiceDetailViewDto?> CheckSriStatusAsync(Guid invoiceId);
        Task ResendInvoiceEmailAsync(Guid invoiceId);
        Task CancelInvoiceAsync(Guid invoiceId);
        Task<InvoiceDetailViewDto?> IssueDraftInvoiceAsync(Guid invoiceId);
        Task ReactivateCancelledInvoiceAsync(Guid invoiceId);
        Task<InvoiceDto?> UpdateInvoiceAsync(UpdateInvoiceDto invoiceDto);
        Task SendPaymentReminderEmailAsync(Guid invoiceId);

        Task<PaginatedList<InvoiceDto>> GetInvoicesByClientIdAsync(Guid clienteId, int pageNumber, int pageSize, string? paymentStatus, FormaDePago? formaDePago, DateTime? startDate, DateTime? endDate, string? searchTerm);
    }
}