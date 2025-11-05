using FacturasSRI.Application.Dtos.Productos;
using FacturasSRI.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FacturasSRI.Application.Interfaces
{
    public interface IProductoRepository
    {
        Task<IEnumerable<Producto>> GetAllProductsAsync();
        Task<Producto?> GetProductByIdAsync(Guid id);
        Task<Producto> CreateProductAsync(Producto producto);
        Task UpdateProductAsync(Producto producto);
        Task DeactivateProductAsync(Guid id);
        Task ActivateProductAsync(Guid id);
    }
}