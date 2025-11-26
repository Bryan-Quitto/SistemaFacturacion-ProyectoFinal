using FacturasSRI.Application.Dtos;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FacturasSRI.Application.Interfaces
{
    public interface IAjusteInventarioService
    {
        Task CreateAdjustmentAsync(AjusteInventarioDto ajusteDto);
        Task<PaginatedList<AjusteListItemDto>> GetAdjustmentsAsync(int pageNumber, int pageSize, string? searchTerm);
    }
}