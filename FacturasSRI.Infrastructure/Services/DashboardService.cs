using FacturasSRI.Application.Dtos;
using FacturasSRI.Application.Interfaces;
using FacturasSRI.Domain.Enums;
using FacturasSRI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace FacturasSRI.Infrastructure.Services
{
    public class DashboardService : IDashboardService
    {
        private readonly IDbContextFactory<FacturasSRIDbContext> _contextFactory;

        public DashboardService(IDbContextFactory<FacturasSRIDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public async Task<DashboardStatsDto> GetDashboardStatsAsync(Guid userId, bool isAdmin, bool isBodeguero)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            var stats = new DashboardStatsDto();

            // Configuración de Fechas
            var hoy = DateTime.UtcNow;
            var primerDiaDelMes = new DateTime(hoy.Year, hoy.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var primerDiaDelSiguienteMes = primerDiaDelMes.AddMonths(1);

            // ==========================================
            // LÓGICA PARA ADMIN Y VENDEDOR (Ventas)
            // ==========================================
            if (!isBodeguero) 
            {
                var queryFacturas = context.Facturas.AsNoTracking().Where(f => f.Estado == EstadoFactura.Autorizada);
                var queryNotas = context.NotasDeCredito.AsNoTracking().Where(nc => nc.Estado == EstadoNotaDeCredito.Autorizada);
                var queryDetalles = context.FacturaDetalles.AsNoTracking().Where(d => d.Factura.Estado == EstadoFactura.Autorizada);

                // Si NO es Admin (es Vendedor), filtramos por SU usuario
                if (!isAdmin)
                {
                    queryFacturas = queryFacturas.Where(f => f.UsuarioIdCreador == userId);
                    queryNotas = queryNotas.Where(nc => nc.UsuarioIdCreador == userId);
                    queryDetalles = queryDetalles.Where(d => d.Factura.UsuarioIdCreador == userId);
                }

                // 1. Contadores Generales
                stats.TotalFacturasEmitidas = await queryFacturas.CountAsync();
                
                // Clientes (Solo Admin ve el total global, Vendedor ve "sus" clientes o todos según política, aquí dejamos todos para vendedor también para que pueda vender)
                stats.TotalClientesRegistrados = await context.Clientes.CountAsync(c => c.EstaActivo);

                // 2. Ingresos del Mes (Facturas - NotasCredito)
                var ingresosFacturas = await queryFacturas
                    .Where(f => f.FechaEmision >= primerDiaDelMes && f.FechaEmision < primerDiaDelSiguienteMes)
                    .SumAsync(f => f.Total); // Usamos Total facturado para el vendedor/admin como métrica de rendimiento

                var devoluciones = await queryNotas
                    .Where(nc => nc.FechaEmision >= primerDiaDelMes && nc.FechaEmision < primerDiaDelSiguienteMes)
                    .SumAsync(nc => nc.Total);

                stats.IngresosEsteMes = ingresosFacturas - devoluciones;

                // 3. Listados Recientes
                stats.RecentInvoices = await queryFacturas
                    .Include(f => f.Cliente)
                    .OrderByDescending(f => f.FechaEmision)
                    .Take(5)
                    .Select(f => new RecentInvoiceDto
                    {
                        Id = f.Id,
                        NumeroFactura = f.NumeroFactura,
                        ClienteNombre = f.Cliente != null ? f.Cliente.RazonSocial : "Consumidor Final",
                        FechaEmision = f.FechaEmision,
                        Total = f.Total,
                        Estado = f.Estado.ToString()
                    }).ToListAsync();

                stats.RecentCreditNotes = await queryNotas
                    .Include(nc => nc.Cliente)
                    .OrderByDescending(nc => nc.FechaEmision)
                    .Take(5)
                    .Select(nc => new RecentCreditNoteDto
                    {
                        Id = nc.Id,
                        NumeroNotaCredito = nc.NumeroNotaCredito,
                        ClienteNombre = nc.Cliente.RazonSocial,
                        FechaEmision = nc.FechaEmision,
                        Total = nc.Total,
                        Estado = nc.Estado.ToString()
                    }).ToListAsync();

                // 4. Top Productos
                stats.TopProducts = await queryDetalles
                    .GroupBy(d => new { d.ProductoId, d.Producto.Nombre })
                    .Select(g => new TopProductDto
                    {
                        ProductName = g.Key.Nombre,
                        QuantitySold = g.Sum(d => d.Cantidad),
                        TotalRevenue = g.Sum(d => d.Subtotal)
                    })
                    .OrderByDescending(p => p.QuantitySold)
                    .Take(5) // Reducido a 5 para el diseño
                    .ToListAsync();
            }

            // ==========================================
            // LÓGICA PARA ADMIN Y BODEGUERO (Inventario)
            // ==========================================
            if (isAdmin || isBodeguero)
            {
                // 1. Productos Bajo Stock
                var lowStockQuery = context.Productos
                    .AsNoTracking()
                    .Where(p => p.EstaActivo && p.ManejaInventario && p.StockTotal < p.StockMinimo);

                stats.TotalProductosBajoStock = await lowStockQuery.CountAsync();

                stats.LowStockProducts = await lowStockQuery
                    .OrderBy(p => p.StockTotal) // Prioridad a los que tienen menos
                    .Take(5)
                    .Select(p => new LowStockProductWidgetDto
                    {
                        ProductName = p.Nombre,
                        StockActual = p.StockTotal,
                        StockMinimo = p.StockMinimo
                    }).ToListAsync();

                // 2. Compras del Mes
                stats.TotalComprasMes = await context.CuentasPorPagar
                    .CountAsync(c => c.FechaEmision >= primerDiaDelMes && 
                                     c.FechaEmision < primerDiaDelSiguienteMes && 
                                     c.Estado != EstadoCompra.Cancelada);
            }

            return stats;
        }
    }
}