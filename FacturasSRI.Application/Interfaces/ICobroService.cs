using FacturasSRI.Application.Dtos;
using FacturasSRI.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FacturasSRI.Application.Interfaces
{
    public interface ICobroService
    {
        Task<CobroDto> RegistrarCobroAsync(RegistrarCobroDto cobroDto, System.IO.Stream fileStream, string fileName);
        Task<List<CobroDto>> GetCobrosByFacturaIdAsync(Guid facturaId);
        Task<PaginatedList<CobroDto>> GetAllCobrosAsync(int pageNumber, int pageSize, string? searchTerm);
        Task<PaginatedList<FacturasConPagosDto>> GetFacturasConPagosAsync(int pageNumber, int pageSize, string? searchTerm, FormaDePago? formaDePago, EstadoFactura? estadoFactura);
    }
}
