using FacturasSRI.Application.Interfaces;
using FacturasSRI.Domain.Entities;
using FacturasSRI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;

namespace FacturasSRI.Infrastructure.Repositories
{
    public class LoteRepository : ILoteRepository
    {
        private readonly FacturasSRIDbContext _db;

        public LoteRepository(FacturasSRIDbContext db)
        {
            _db = db;
        }

        public async Task<IEnumerable<Lote>> GetAllAsync()
        {
            return await _db.Lotes
                .AsNoTracking()
                .Include(l => l.Producto)
                .OrderByDescending(l => l.FechaCompra)
                .ToListAsync();
        }

        public async Task<Lote> AddAsync(Lote lote)
        {
            _db.Lotes.Add(lote);
            await _db.SaveChangesAsync();
            return lote;
        }

        public async Task<Lote?> GetByIdAsync(Guid id)
        {
            return await _db.Lotes.AsNoTracking().FirstOrDefaultAsync(l => l.Id == id);
        }

        public async Task<bool> ProductoExistsAsync(Guid productoId)
        {
            return await _db.Productos.AsNoTracking().AnyAsync(p => p.Id == productoId);
        }
    }
}
