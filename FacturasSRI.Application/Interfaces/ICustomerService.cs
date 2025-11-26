using FacturasSRI.Application.Dtos;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FacturasSRI.Application.Interfaces
{
    public interface ICustomerService
    {
        Task<PaginatedList<CustomerDto>> GetCustomersAsync(int pageNumber, int pageSize, string? searchTerm);
        Task<CustomerDto?> GetCustomerByIdAsync(Guid id);
        Task<CustomerDto> CreateCustomerAsync(CustomerDto customer);
        Task UpdateCustomerAsync(CustomerDto customer);
        Task DeleteCustomerAsync(Guid id);
        Task<List<CustomerDto>> GetActiveCustomersAsync(); // New method
    }
}
