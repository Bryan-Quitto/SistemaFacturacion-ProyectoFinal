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
            if (await _context.Productos.AnyAsync(p => p.Nombre.ToLower() == productDto.Nombre.ToLower()))
            {
                throw new InvalidOperationException("Ya existe un producto con el mismo nombre.");
            }
            if (await _context.Productos.AnyAsync(p => p.CodigoPrincipal.ToLower() == productDto.CodigoPrincipal.ToLower()))
            {
                throw new InvalidOperationException("Ya existe un producto con el mismo código principal.");
            }
            var product = new Producto
            {
                Id = Guid.NewGuid(),
                CodigoPrincipal = productDto.CodigoPrincipal,
                Nombre = productDto.Nombre,
                Descripcion = productDto.Descripcion,
                PrecioVentaUnitario = productDto.PrecioVentaUnitario,
                ManejaInventario = productDto.ManejaInventario,
                ManejaLotes = productDto.ManejaLotes,
                UsuarioIdCreador = productDto.UsuarioIdCreador,
                FechaCreacion = DateTime.UtcNow,
                Marca = productDto.Marca,
                CategoriaId = productDto.CategoriaId
            };
            _context.Productos.Add(product);
            await _context.SaveChangesAsync();
            productDto.Id = product.Id;
            return productDto;
        }

        public async Task<ProductDto?> GetProductByIdAsync(Guid id)
        {
            return await (from product in _context.Productos
                          where product.Id == id
                          join usuario in _context.Usuarios on product.UsuarioIdCreador equals usuario.Id into usuarioJoin
                          from usuario in usuarioJoin.DefaultIfEmpty()
                          join categoria in _context.Categorias on product.CategoriaId equals categoria.Id
                          select new ProductDto
                          {
                              Id = product.Id,
                              CodigoPrincipal = product.CodigoPrincipal,
                              Nombre = product.Nombre,
                              Descripcion = product.Descripcion,
                              PrecioVentaUnitario = product.PrecioVentaUnitario,
                              ManejaInventario = product.ManejaInventario,
                              ManejaLotes = product.ManejaLotes,
                              StockTotal = product.ManejaLotes ? product.Lotes.Sum(l => l.CantidadDisponible) : product.StockTotal,
                              CreadoPor = usuario != null ? usuario.PrimerNombre + " " + usuario.PrimerApellido : "Usuario no encontrado",
                              IsActive = product.EstaActivo,
                              FechaCreacion = product.FechaCreacion,
                              FechaModificacion = product.FechaModificacion,
                              Marca = product.Marca, // --- CAMBIO ---
                              CategoriaId = product.CategoriaId, // --- CAMBIO ---
                              CategoriaNombre = categoria.Nombre
                          }).FirstOrDefaultAsync();
        }

        public async Task<List<ProductDto>> GetProductsAsync()
        {
            return await (from product in _context.Productos
                          join usuario in _context.Usuarios on product.UsuarioIdCreador equals usuario.Id into usuarioJoin
                          from usuario in usuarioJoin.DefaultIfEmpty()
                          join categoria in _context.Categorias on product.CategoriaId equals categoria.Id

                          select new ProductDto
                          {
                              Id = product.Id,
                              CodigoPrincipal = product.CodigoPrincipal,
                              Nombre = product.Nombre,
                              Descripcion = product.Descripcion,
                              PrecioVentaUnitario = product.PrecioVentaUnitario,
                              ManejaInventario = product.ManejaInventario,
                              ManejaLotes = product.ManejaLotes,
                              StockTotal = product.ManejaLotes ? product.Lotes.Sum(l => l.CantidadDisponible) : product.StockTotal,
                              CreadoPor = usuario != null ? usuario.PrimerNombre + " " + usuario.PrimerApellido : "Usuario no encontrado",
                              IsActive = product.EstaActivo,
                              Marca = product.Marca,
                              CategoriaId = product.CategoriaId,
                              CategoriaNombre = categoria.Nombre
                          }).ToListAsync();
        }

        public async Task<List<ProductDto>> GetActiveProductsAsync()
        {
            return await (from product in _context.Productos
                          join usuario in _context.Usuarios on product.UsuarioIdCreador equals usuario.Id into usuarioJoin
                          from usuario in usuarioJoin.DefaultIfEmpty()
                          join categoria in _context.Categorias on product.CategoriaId equals categoria.Id
                          where product.EstaActivo == true // Filter for active products
                          select new ProductDto
                          {
                              Id = product.Id,
                              CodigoPrincipal = product.CodigoPrincipal,
                              Nombre = product.Nombre,
                              Descripcion = product.Descripcion,
                              PrecioVentaUnitario = product.PrecioVentaUnitario,
                              ManejaInventario = product.ManejaInventario,
                              ManejaLotes = product.ManejaLotes,
                              StockTotal = product.ManejaLotes ? product.Lotes.Sum(l => l.CantidadDisponible) : product.StockTotal,
                              CreadoPor = usuario != null ? usuario.PrimerNombre + " " + usuario.PrimerApellido : "Usuario no encontrado",
                              IsActive = product.EstaActivo,
                              Marca = product.Marca,
                              CategoriaId = product.CategoriaId,
                              CategoriaNombre = categoria.Nombre
                          }).ToListAsync();
        }

        public async Task UpdateProductAsync(ProductDto productDto)
        {
            var product = await _context.Productos.FindAsync(productDto.Id);
            if (product != null)
            {
                if (await _context.Productos.AnyAsync(p => p.Id != productDto.Id && p.Nombre.ToLower() == productDto.Nombre.ToLower()))
                {
                    throw new InvalidOperationException("Ya existe otro producto con el mismo nombre.");
                }
                if (await _context.Productos.AnyAsync(p => p.Id != productDto.Id && p.CodigoPrincipal.ToLower() == productDto.CodigoPrincipal.ToLower()))
                {
                    throw new InvalidOperationException("Ya existe otro producto con el mismo código principal.");
                }
                product.CodigoPrincipal = productDto.CodigoPrincipal;
                product.Nombre = productDto.Nombre;
                product.Descripcion = productDto.Descripcion;
                product.PrecioVentaUnitario = productDto.PrecioVentaUnitario;
                product.ManejaInventario = productDto.ManejaInventario;
                product.ManejaLotes = productDto.ManejaLotes;
                product.EstaActivo = productDto.IsActive;
                product.Marca = productDto.Marca;
                product.CategoriaId = productDto.CategoriaId;
                await _context.SaveChangesAsync();
            }
        }

        public async Task DeleteProductAsync(Guid id)
        {
            var product = await _context.Productos.FindAsync(id);
            if (product != null)
            {
                product.EstaActivo = !product.EstaActivo; // Toggle the active status
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

        public async Task ApplyTaxToAllProductsAsync(Guid taxId)
        {
            // This is a destructive operation, so be careful.
            // It removes all existing tax assignments and applies the new one.

            // 1. Remove all existing product-tax relationships
            var allProductTaxes = await _context.ProductoImpuestos.ToListAsync();
            _context.ProductoImpuestos.RemoveRange(allProductTaxes);

            // 2. Get all product IDs
            var allProductIds = await _context.Productos.Select(p => p.Id).ToListAsync();

            // 3. Create new assignments
            var newAssignments = allProductIds.Select(productId => new ProductoImpuesto
            {
                ProductoId = productId,
                ImpuestoId = taxId
            });

            await _context.ProductoImpuestos.AddRangeAsync(newAssignments);

            // 4. Save changes
            await _context.SaveChangesAsync();
        }

        public async Task<TaxDto?> GetCurrentGlobalTaxAsync()
        {
            var firstProductTax = await _context.ProductoImpuestos
                .Include(pi => pi.Impuesto)
                .FirstOrDefaultAsync();

            if (firstProductTax == null)
            {
                return null;
            }

            return new TaxDto
            {
                Id = firstProductTax.Impuesto.Id,
                Nombre = firstProductTax.Impuesto.Nombre,
                Porcentaje = firstProductTax.Impuesto.Porcentaje,
                CodigoSRI = firstProductTax.Impuesto.CodigoSRI,
                EstaActivo = firstProductTax.Impuesto.EstaActivo
            };
        }

        public async Task<ProductDetailDto?> GetProductDetailsByIdAsync(Guid id)
        {
            var product = await _context.Productos
                .Where(p => p.Id == id)
                .Select(p => new ProductDetailDto
                {
                    Id = p.Id,
                    CodigoPrincipal = p.CodigoPrincipal,
                    Nombre = p.Nombre,
                    Descripcion = p.Descripcion,
                    PrecioVentaUnitario = p.PrecioVentaUnitario,
                    ManejaInventario = p.ManejaInventario,
                    ManejaLotes = p.ManejaLotes,
                    IsActive = p.EstaActivo,
                    StockTotal = p.ManejaLotes ? p.Lotes.Sum(l => l.CantidadDisponible) : p.StockTotal,
                    Taxes = p.ProductoImpuestos.Select(pi => new TaxDto
                    {
                        Id = pi.Impuesto.Id,
                        Nombre = pi.Impuesto.Nombre,
                        CodigoSRI = pi.Impuesto.CodigoSRI,
                        Porcentaje = pi.Impuesto.Porcentaje,
                        EstaActivo = pi.Impuesto.EstaActivo
                    }).ToList()
                }).FirstOrDefaultAsync();

            return product;
        }

        public async Task<List<CategoriaDto>> GetAllCategoriasAsync()
        {

            try
            {
                var categoriasDesdeDb = await _context.Categorias
                    .OrderBy(c => c.Nombre)
                    .Select(c => new CategoriaDto
                    {
                        Id = c.Id,
                        Nombre = c.Nombre
                    })
                    .ToListAsync();
                return categoriasDesdeDb;
            }
            catch (Exception ex)
            {
                return new List<CategoriaDto>(); // Devolver lista vacía si hay error
            }
        }
    }
}