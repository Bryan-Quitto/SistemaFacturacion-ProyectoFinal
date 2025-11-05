using FacturasSRI.Application.Interfaces;
using FacturasSRI.Domain.Entities;
using FacturasSRI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FacturasSRI.Infrastructure.Repositories
{
    public class ProductoRepository : IProductoRepository
    {
        private readonly FacturasSRIDbContext _context;

        public ProductoRepository(FacturasSRIDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Producto>> GetAllProductsAsync()
        {
            return await _context.Productos
                .Where(p => p.EstaActivo)
                .AsNoTracking()
                .ToListAsync();
        }



        public async Task<Producto?> GetProductByIdAsync(Guid id)
        {
            return await _context.Productos.FindAsync(id);
        }

        public async Task<Producto> CreateProductAsync(Producto producto)
        {
            _context.Productos.Add(producto);
            await _context.SaveChangesAsync();
            return producto;
        }
        
        public async Task UpdateProductAsync(Producto producto)
        {
            _context.Entry(producto).State = EntityState.Modified;
            await _context.SaveChangesAsync();
        }

        public async Task DeactivateProductAsync(Guid id)
        {
            var producto = await _context.Productos.FindAsync(id);
            if (producto != null)
            {
                producto.EstaActivo = false;
                await _context.SaveChangesAsync();
            }
        }
        
        public async Task ActivateProductAsync(Guid id)
        {
            var producto = await _context.Productos.FindAsync(id);
            if (producto != null)
            {
                producto.EstaActivo = true;
                await _context.SaveChangesAsync();
            }
        }
    }
}