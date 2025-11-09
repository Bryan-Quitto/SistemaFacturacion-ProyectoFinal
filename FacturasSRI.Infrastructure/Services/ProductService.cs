using FacturasSRI.Application.Dtos;
using FacturasSRI.Application.Interfaces;
using FacturasSRI.Domain.Entities;
using FacturasSRI.Infrastructure.Persistence;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace FacturasSRI.Infrastructure.Services
{
    public class ProductService : IProductService
    {
        private readonly FacturasSRIDbContext _context;

        public ProductService(FacturasSRIDbContext context)
        {
            _context = context;
        }

        public async Task<ProductDto> CreateProductAsync(ProductDto productDto)
        {
            var product = new Producto
            {
                Id = Guid.NewGuid(),
                CodigoPrincipal = productDto.CodigoPrincipal,
                Nombre = productDto.Nombre,
                Descripcion = productDto.Descripcion,
                PrecioVentaUnitario = productDto.PrecioVentaUnitario,
                ManejaInventario = productDto.ManejaInventario,
                ManejaLotes = productDto.ManejaLotes,
                FechaCreacion = DateTime.UtcNow
            };
            _context.Productos.Add(product);
            await _context.SaveChangesAsync();
            productDto.Id = product.Id;
            return productDto;
        }

        public async Task<ProductDto?> GetProductByIdAsync(Guid id)
        {
            var product = await _context.Productos.FindAsync(id);
            if (product == null)
            {
                return null;
            }
            return new ProductDto
            {
                Id = product.Id,
                CodigoPrincipal = product.CodigoPrincipal,
                Nombre = product.Nombre,
                Descripcion = product.Descripcion,
                PrecioVentaUnitario = product.PrecioVentaUnitario,
                ManejaInventario = product.ManejaInventario,
                ManejaLotes = product.ManejaLotes
            };
        }

        public async Task<List<ProductDto>> GetProductsAsync()
        {
            return await _context.Productos
                .Include(p => p.Lotes)
                .Select(product => new ProductDto
                {
                    Id = product.Id,
                    CodigoPrincipal = product.CodigoPrincipal,
                    Nombre = product.Nombre,
                    Descripcion = product.Descripcion,
                    PrecioVentaUnitario = product.PrecioVentaUnitario,
                    ManejaInventario = product.ManejaInventario,
                    ManejaLotes = product.ManejaLotes,
                    StockTotal = product.ManejaLotes ? product.Lotes.Sum(l => l.CantidadDisponible) : product.StockTotal
                }).ToListAsync();
        }

        public async Task UpdateProductAsync(ProductDto productDto)
        {
            var product = await _context.Productos.FindAsync(productDto.Id);
            if (product != null)
            {
                product.CodigoPrincipal = productDto.CodigoPrincipal;
                product.Nombre = productDto.Nombre;
                product.Descripcion = productDto.Descripcion;
                product.PrecioVentaUnitario = productDto.PrecioVentaUnitario;
                product.ManejaInventario = productDto.ManejaInventario;
                product.ManejaLotes = productDto.ManejaLotes;
                await _context.SaveChangesAsync();
            }
        }

        public async Task AssignTaxesToProductAsync(Guid productId, List<Guid> taxIds)
        {
            var product = await _context.Productos.Include(p => p.ProductoImpuestos).FirstOrDefaultAsync(p => p.Id == productId);
            if (product != null)
            {
                product.ProductoImpuestos.Clear();
                foreach (var taxId in taxIds)
                {
                    product.ProductoImpuestos.Add(new ProductoImpuesto { ProductoId = productId, ImpuestoId = taxId });
                }
                await _context.SaveChangesAsync();
            }
        }

        public async Task DeleteProductAsync(Guid id)
        {
            var product = await _context.Productos.FindAsync(id);
            if (product != null)
            {
                product.EstaActivo = false;
                await _context.SaveChangesAsync();
            }
        }

        public async Task<ProductStockDto?> GetProductStockDetailsAsync(Guid productId)
        {
            var product = await _context.Productos
                .Include(p => p.Lotes)
                .FirstOrDefaultAsync(p => p.Id == productId);

            if (product == null)
            {
                return null;
            }

            var stockDetails = new ProductStockDto
            {
                ProductId = product.Id,
                ProductName = product.Nombre,
                ManejaLotes = product.ManejaLotes
            };

            if (product.ManejaLotes)
            {
                var lotesActivos = product.Lotes.Where(l => l.CantidadDisponible > 0).ToList();
                stockDetails.TotalStock = lotesActivos.Sum(l => l.CantidadDisponible);
                stockDetails.Lotes = lotesActivos.Select(l => new LoteDto
                {
                    Id = l.Id,
                    CantidadDisponible = l.CantidadDisponible,
                    PrecioCompraUnitario = l.PrecioCompraUnitario,
                    FechaCaducidad = l.FechaCaducidad
                }).ToList();
            }
            else
            {
                stockDetails.TotalStock = product.StockTotal;
            }

            return stockDetails;
        }
    }
}