using FacturasSRI.Application.Dtos;
using FacturasSRI.Domain.Entities;
using FacturasSRI.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FacturasSRI.Application.Interfaces
{
    public interface ICreditNoteService
    {
        Task<PaginatedList<CreditNoteDto>> GetCreditNotesAsync(int pageNumber, int pageSize, string? searchTerm, EstadoNotaDeCredito? status);
        Task<CreditNoteDetailViewDto?> GetCreditNoteDetailByIdAsync(Guid id);
        Task<NotaDeCredito> CreateCreditNoteAsync(CreateCreditNoteDto dto);
        Task CheckSriStatusAsync(Guid ncId);
        Task CancelCreditNoteAsync(Guid creditNoteId);
        Task ReactivateCancelledCreditNoteAsync(Guid creditNoteId);
        Task<CreditNoteDetailViewDto?> IssueDraftCreditNoteAsync(Guid creditNoteId);
        Task<CreditNoteDto?> UpdateCreditNoteAsync(UpdateCreditNoteDto dto);
        Task ResendCreditNoteEmailAsync(Guid creditNoteId);
    }
}
