using FacturasSRI.Application.Dtos.Reports;
using FacturasSRI.Application.Interfaces;
using FacturasSRI.Domain.Enums;
using FacturasSRI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FacturasSRI.Infrastructure.Services
{
    public class ReportService : IReportService
    {
        private readonly FacturasSRIDbContext _context;

        public ReportService(FacturasSRIDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<VentasPorPeriodoDto>> GetVentasPorPeriodoAsync(DateTime fechaInicio, DateTime fechaFin)
        {
            // The dates are UTC from the endpoint. Using .Date resets the Kind to Unspecified.
            // We must explicitly set it back to Utc for Npgsql to work correctly.
            var startDate = DateTime.SpecifyKind(fechaInicio.Date, DateTimeKind.Utc);
            var endDate = DateTime.SpecifyKind(fechaFin.Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc);

            var reportData = await _context.Facturas
                .AsNoTracking()
                .Where(f => f.FechaEmision >= startDate && f.FechaEmision <= endDate && f.Estado != EstadoFactura.Cancelada)
                .GroupBy(f => f.FechaEmision.Date)
                .Select(g => new VentasPorPeriodoDto
                {
                    Fecha = g.Key,
                    CantidadFacturas = g.Count(),
                    Subtotal = g.Sum(f => f.SubtotalSinImpuestos),
                    TotalIva = g.Sum(f => f.TotalIVA),
                    Total = g.Sum(f => f.Total)
                })
                .OrderBy(dto => dto.Fecha)
                .ToListAsync();

            return reportData;
        }

        public async Task<IEnumerable<VentasPorProductoDto>> GetVentasPorProductoAsync(DateTime fechaInicio, DateTime fechaFin)
        {
            var startDate = DateTime.SpecifyKind(fechaInicio.Date, DateTimeKind.Utc);
            var endDate = DateTime.SpecifyKind(fechaFin.Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc);

            var reportData = await _context.FacturaDetalles
                .AsNoTracking()
                .Where(d => d.Factura.FechaEmision >= startDate && d.Factura.FechaEmision <= endDate && d.Factura.Estado != EstadoFactura.Cancelada)
                .GroupBy(d => new { d.ProductoId, d.Producto.CodigoPrincipal, d.Producto.Nombre })
                .Select(g => new VentasPorProductoDto
                {
                    CodigoProducto = g.Key.CodigoPrincipal,
                    NombreProducto = g.Key.Nombre,
                    CantidadVendida = g.Sum(d => d.Cantidad),
                    TotalVendido = g.Sum(d => d.Subtotal),
                    PrecioPromedio = g.Sum(d => d.Subtotal) / g.Sum(d => d.Cantidad)
                })
                .OrderByDescending(dto => dto.TotalVendido)
                .ToListAsync();

            return reportData;
        }

        public async Task<IEnumerable<ClienteActividadDto>> GetActividadClientesAsync(DateTime fechaInicio, DateTime fechaFin)
        {
            var startDate = DateTime.SpecifyKind(fechaInicio.Date, DateTimeKind.Utc);
            var endDate = DateTime.SpecifyKind(fechaFin.Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc);

            // Get all invoices in the date range to avoid multiple DB calls in a loop
            var facturasEnRango = await _context.Facturas
                .AsNoTracking()
                .Where(f => f.FechaEmision >= startDate && f.FechaEmision <= endDate && f.Estado != EstadoFactura.Cancelada && f.ClienteId != null)
                .Select(f => new { f.ClienteId, f.FechaEmision, f.Total })
                .ToListAsync();

            // Get all customers
            var clientes = await _context.Clientes
                .AsNoTracking()
                .ToListAsync();

            var today = DateTime.UtcNow.Date;

            var reportData = clientes.Select(c =>
            {
                var comprasCliente = facturasEnRango.Where(f => f.ClienteId == c.Id).ToList();
                var ultimaCompra = comprasCliente.Any() ? comprasCliente.Max(f => f.FechaEmision) : (DateTime?)null;

                return new ClienteActividadDto
                {
                    NombreCliente = c.RazonSocial,
                    Identificacion = c.NumeroIdentificacion,
                    UltimaCompra = ultimaCompra,
                    DiasDesdeUltimaCompra = ultimaCompra.HasValue ? (int)(today - ultimaCompra.Value.Date).TotalDays : -1, // -1 or some indicator for no purchase
                    NumeroDeCompras = comprasCliente.Count,
                    TotalComprado = comprasCliente.Sum(f => f.Total)
                };
            })
            .OrderByDescending(c => c.TotalComprado)
            .ToList();

            return reportData;
        }

        public async Task<IEnumerable<CuentasPorCobrarDto>> GetCuentasPorCobrarAsync()
        {
            var today = DateTime.UtcNow.Date;

            var reportData = await _context.Facturas
                .AsNoTracking()
                .Where(f => f.Estado != EstadoFactura.Cancelada && f.ClienteId != null && f.Total > f.Cobros.Sum(c => c.Monto))
                .Select(f => new CuentasPorCobrarDto
                {
                    NombreCliente = f.Cliente.RazonSocial,
                    NumeroFactura = f.NumeroFactura,
                    FechaEmision = f.FechaEmision,
                    DiasVencida = (f.FormaDePago == FormaDePago.Credito && f.DiasCredito.HasValue) 
                                 ? (int)(today - f.FechaEmision.AddDays(f.DiasCredito.Value)).TotalDays
                                 : (int)(today - f.FechaEmision).TotalDays,
                    MontoFactura = f.Total,
                    MontoPagado = f.Cobros.Sum(c => c.Monto),
                    SaldoPendiente = f.Total - f.Cobros.Sum(c => c.Monto)
                })
                .OrderBy(dto => dto.DiasVencida)
                .ToListAsync();
            
            // Filter out negative days vencida which means it's not due yet.
            reportData.ForEach(r => r.DiasVencida = Math.Max(0, r.DiasVencida));

            return reportData;
        }

        public async Task<IEnumerable<StockActualDto>> GetStockActualAsync()
        {
            var reportData = await _context.Productos
                .AsNoTracking()
                .Where(p => p.EstaActivo)
                .Select(p => new StockActualDto
                {
                    CodigoPrincipal = p.CodigoPrincipal,
                    NombreProducto = p.Nombre,
                    Categoria = p.Categoria.Nombre,
                    StockTotal = p.StockTotal,
                    PrecioCompraPromedioPonderado = p.PrecioCompraPromedioPonderado,
                    ValorInventario = 0 // Placeholder, will be calculated on the client side
                })
                .OrderBy(p => p.NombreProducto)
                .ToListAsync();

            // Perform calculation on the client side to avoid DB overflow
            foreach (var item in reportData)
            {
                item.ValorInventario = (decimal)item.StockTotal * item.PrecioCompraPromedioPonderado;
            }

            return reportData;
        }

        public async Task<IEnumerable<NotasDeCreditoReportDto>> GetNotasDeCreditoAsync(DateTime fechaInicio, DateTime fechaFin)
        {
            var startDate = DateTime.SpecifyKind(fechaInicio.Date, DateTimeKind.Utc);
            var endDate = DateTime.SpecifyKind(fechaFin.Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc);

            var reportData = await _context.NotasDeCredito
                .AsNoTracking()
                .Where(nc => nc.FechaEmision >= startDate && nc.FechaEmision <= endDate)
                .Select(nc => new NotasDeCreditoReportDto
                {
                    NumeroNotaCredito = nc.NumeroNotaCredito,
                    FechaEmision = nc.FechaEmision,
                    NombreCliente = nc.Cliente.RazonSocial,
                    FacturaModificada = nc.Factura.NumeroFactura,
                    Motivo = nc.RazonModificacion,
                    ValorTotal = nc.Total
                })
                .OrderByDescending(dto => dto.FechaEmision)
                .ToListAsync();

            return reportData;
        }
    }
}
