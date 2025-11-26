using FacturasSRI.Application.Dtos;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FacturasSRI.Application.Interfaces
{
    public interface IProductService
    {
        Task<PaginatedList<ProductDto>> GetProductsAsync(int pageNumber, int pageSize, string? searchTerm, Guid? categoryId, string? marca, string? stockStatus);
        Task<ProductDto?> GetProductByIdAsync(Guid id);
        Task<ProductDto> CreateProductAsync(ProductDto productDto);
        Task UpdateProductAsync(ProductDto productDto);
        Task DeleteProductAsync(Guid id);
        Task<List<ProductDto>> GetActiveProductsAsync(); // New method
        Task<ProductStockDto?> GetProductStockDetailsAsync(Guid productId);
        Task ApplyTaxToAllProductsAsync(Guid taxId);
        Task<TaxDto?> GetCurrentGlobalTaxAsync();
        Task<ProductDetailDto?> GetProductDetailsByIdAsync(Guid id);
        Task<List<CategoriaDto>> GetAllCategoriasAsync();
        Task<List<string>> GetAllMarcasAsync();
    }
}