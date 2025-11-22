using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FacturasSRI.Application.Dtos;

namespace FacturasSRI.Application.Interfaces
{
    public interface ICobroService
    {
        Task<CobroDto> RegistrarCobroAsync(RegistrarCobroDto cobroDto, System.IO.Stream fileStream, string fileName);
        Task<List<CobroDto>> GetCobrosByFacturaIdAsync(Guid facturaId);
        Task<List<CobroDto>> GetAllCobrosAsync();
    }
}
