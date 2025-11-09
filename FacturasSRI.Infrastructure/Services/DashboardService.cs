using FacturasSRI.Application.Dtos;
using FacturasSRI.Application.Interfaces;
using FacturasSRI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace FacturasSRI.Infrastructure.Services
{
    public class DashboardService : IDashboardService
    {
        private readonly FacturasSRIDbContext _context;

        public DashboardService(FacturasSRIDbContext context)
        {
            _context = context;
        }

        public async Task<DashboardStatsDto> GetDashboardStatsAsync()
        {
            var totalFacturas = await _context.Facturas.CountAsync();
            var totalClientes = await _context.Clientes.CountAsync(c => c.EstaActivo);

            var hoy = DateTime.UtcNow;
            var primerDiaDelMes = new DateTime(hoy.Year, hoy.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var primerDiaDelSiguienteMes = primerDiaDelMes.AddMonths(1);

            var ingresosMes = await _context.Facturas
                .Where(f => f.FechaEmision >= primerDiaDelMes && f.FechaEmision < primerDiaDelSiguienteMes)
                .SumAsync(f => f.Total);

            return new DashboardStatsDto
            {
                TotalFacturasEmitidas = totalFacturas,
                TotalClientesRegistrados = totalClientes,
                IngresosEsteMes = ingresosMes
            };
        }
    }
}