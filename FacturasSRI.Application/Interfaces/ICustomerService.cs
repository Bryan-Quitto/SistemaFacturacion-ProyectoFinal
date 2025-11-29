using FacturasSRI.Application.Dtos;
using FacturasSRI.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FacturasSRI.Application.Interfaces
{
    public interface ICustomerService
    {
        Task<PaginatedList<CustomerDto>> GetCustomersAsync(int pageNumber, int pageSize, string? searchTerm, bool? isActive, TipoIdentificacion? tipoIdentificacion);
        Task<CustomerDto?> GetCustomerByIdAsync(Guid id);
        Task<CustomerDto> CreateCustomerAsync(CustomerDto customer);
        Task UpdateCustomerAsync(CustomerDto customer);
        Task DeleteCustomerAsync(Guid id);
        Task<List<CustomerDto>> GetActiveCustomersAsync();
        Task<CustomerDto> RegistrarNuevoClienteAsync(ClienteRegistroDto dto);
        Task<CustomerDto?> AutenticarClienteAsync(ClienteLoginDto dto);
        Task<bool> ConfirmEmailAsync(string token);
    }
}
