using FacturasSRI.Application.Dtos;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FacturasSRI.Application.Interfaces
{
    public interface IProductService
    {
        Task<List<ProductDto>> GetProductsAsync();
        Task<ProductDto?> GetProductByIdAsync(Guid id);
        Task<ProductDto> CreateProductAsync(ProductDto productDto, Guid userId);
        Task UpdateProductAsync(ProductDto productDto);
        Task DeleteProductAsync(Guid id);
        Task AssignTaxesToProductAsync(Guid productId, List<Guid> taxIds);
        Task<ProductStockDto?> GetProductStockDetailsAsync(Guid productId);
    }
}