using FacturasSRI.Application.Dtos.Reports;
using FacturasSRI.Application.Interfaces;
using FacturasSRI.Domain.Enums;
using FacturasSRI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FacturasSRI.Infrastructure.Services
{
    public class ReportService : IReportService
    {
        private readonly FacturasSRIDbContext _context;
        private readonly ReportPdfGeneratorService _pdfGeneratorService;

        public ReportService(FacturasSRIDbContext context, ReportPdfGeneratorService pdfGeneratorService)
        {
            _context = context;
            _pdfGeneratorService = pdfGeneratorService;
        }

        // ----------------------- REPORTES DE VENTAS -----------------------

        public async Task<IEnumerable<VentasPorPeriodoDto>> GetVentasPorPeriodoAsync(DateTime fechaInicio, DateTime fechaFin, Guid? userId)
        {
            var startDate = DateTime.SpecifyKind(fechaInicio.Date, DateTimeKind.Utc);
            var endDate = DateTime.SpecifyKind(fechaFin.Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc);

            var query = _context.Facturas.AsNoTracking();

            if (userId.HasValue)
            {
                query = query.Where(f => f.UsuarioIdCreador == userId.Value);
            }

            var reportData = await query
                .Where(f => f.FechaEmision >= startDate && f.FechaEmision <= endDate && f.Estado != EstadoFactura.Cancelada)
                .Join(_context.Usuarios,
                    factura => factura.UsuarioIdCreador,
                    usuario => usuario.Id,
                    (factura, usuario) => new { factura, usuario })
                .GroupBy(x => new { x.factura.FechaEmision.Date, Vendedor = x.usuario.PrimerNombre + " " + x.usuario.PrimerApellido })
                .Select(g => new VentasPorPeriodoDto
                {
                    Fecha = g.Key.Date,
                    Vendedor = g.Key.Vendedor,
                    CantidadFacturas = g.Count(),
                    Subtotal = g.Sum(x => x.factura.SubtotalSinImpuestos),
                    TotalIva = g.Sum(x => x.factura.TotalIVA),
                    Total = g.Sum(x => x.factura.Total)
                })
                .OrderBy(dto => dto.Fecha)
                .ThenBy(dto => dto.Vendedor)
                .ToListAsync();

            return reportData;
        }

        public async Task<byte[]> GetVentasPorPeriodoAsPdfAsync(DateTime fechaInicio, DateTime fechaFin, Guid? userId)
        {
            var data = await GetVentasPorPeriodoAsync(fechaInicio, fechaFin, userId);
            if (data == null || !data.Any()) return Array.Empty<byte>();
            return _pdfGeneratorService.GenerateVentasPorPeriodoPdf(data, fechaInicio, fechaFin);
        }

        public async Task<IEnumerable<VentasPorProductoDto>> GetVentasPorProductoAsync(DateTime fechaInicio, DateTime fechaFin, Guid? userId)
        {
            var startDate = DateTime.SpecifyKind(fechaInicio.Date, DateTimeKind.Utc);
            var endDate = DateTime.SpecifyKind(fechaFin.Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc);

            var query = _context.FacturaDetalles
                .AsNoTracking()
                .Where(d => d.Factura.FechaEmision >= startDate && d.Factura.FechaEmision <= endDate && d.Factura.Estado != EstadoFactura.Cancelada)
                .Join(_context.Facturas,
                    detalle => detalle.FacturaId,
                    factura => factura.Id,
                    (detalle, factura) => new { detalle, factura })
                .Join(_context.Usuarios,
                    df => df.factura.UsuarioIdCreador,
                    usuario => usuario.Id,
                    (df, usuario) => new { df.detalle, df.factura, usuario });

            if (userId.HasValue)
            {
                query = query.Where(x => x.factura.UsuarioIdCreador == userId.Value);
            }

            var reportData = await query
                .GroupBy(x => new {
                    x.detalle.ProductoId,
                    x.detalle.Producto.CodigoPrincipal,
                    x.detalle.Producto.Nombre,
                    Fecha = x.factura.FechaEmision.Date,
                    Vendedor = x.usuario.PrimerNombre + " " + x.usuario.PrimerApellido
                })
                .Select(g => new VentasPorProductoDto
                {
                    Fecha = g.Key.Fecha,
                    Vendedor = g.Key.Vendedor,
                    CodigoProducto = g.Key.CodigoPrincipal,
                    NombreProducto = g.Key.Nombre,
                    CantidadVendida = g.Sum(d => d.detalle.Cantidad),
                    TotalVendido = g.Sum(d => d.detalle.Subtotal),
                    PrecioPromedio = g.Sum(d => d.detalle.Cantidad) > 0 ? g.Sum(d => d.detalle.Subtotal) / g.Sum(d => d.detalle.Cantidad) : 0
                })
                .OrderBy(dto => dto.Fecha)
                .ThenBy(dto => dto.Vendedor)
                .ThenByDescending(dto => dto.TotalVendido)
                .ToListAsync();

            return reportData;
        }

        public async Task<byte[]> GetVentasPorProductoAsPdfAsync(DateTime fechaInicio, DateTime fechaFin, Guid? userId)
        {
            var data = await GetVentasPorProductoAsync(fechaInicio, fechaFin, userId);
            if (data == null || !data.Any()) return Array.Empty<byte>();
            return _pdfGeneratorService.GenerateVentasPorProductoPdf(data, fechaInicio, fechaFin);
        }

        public async Task<IEnumerable<ClienteActividadDto>> GetActividadClientesAsync(DateTime fechaInicio, DateTime fechaFin, Guid? userId)
        {
            var startDate = DateTime.SpecifyKind(fechaInicio.Date, DateTimeKind.Utc);
            var endDate = DateTime.SpecifyKind(fechaFin.Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc);

            var queryFacturas = _context.Facturas
                .AsNoTracking()
                .Where(f => f.FechaEmision >= startDate && f.FechaEmision <= endDate && f.Estado != EstadoFactura.Cancelada && f.ClienteId != null);

            if (userId.HasValue)
            {
                queryFacturas = queryFacturas.Where(f => f.UsuarioIdCreador == userId.Value);
            }

            var facturasEnRango = await queryFacturas
                .Select(f => new { f.ClienteId, f.FechaEmision, f.Total })
                .ToListAsync();

            var clientes = await _context.Clientes.AsNoTracking().ToListAsync();
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
                    DiasDesdeUltimaCompra = ultimaCompra.HasValue ? (int)(today - ultimaCompra.Value.Date).TotalDays : -1,
                    NumeroDeCompras = comprasCliente.Count,
                    TotalComprado = comprasCliente.Sum(f => f.Total)
                };
            })
            .OrderByDescending(c => c.TotalComprado)
            .ToList();

            return reportData;
        }

        public async Task<byte[]> GetActividadClientesAsPdfAsync(DateTime fechaInicio, DateTime fechaFin, Guid? userId)
        {
            var data = await GetActividadClientesAsync(fechaInicio, fechaFin, userId);
            if (data == null || !data.Any()) return Array.Empty<byte>();
            return _pdfGeneratorService.GenerateActividadClientesPdf(data, fechaInicio, fechaFin);
        }

        public async Task<IEnumerable<CuentasPorCobrarDto>> GetCuentasPorCobrarAsync(Guid? userId)
        {
            var today = DateTime.UtcNow.Date;

            var query = _context.Facturas
                .AsNoTracking()
                .Where(f => f.Estado != EstadoFactura.Cancelada && f.ClienteId != null && f.Total > f.Cobros.Sum(c => c.Monto));

            if (userId.HasValue)
            {
                query = query.Where(f => f.UsuarioIdCreador == userId.Value);
            }

            var queryWithIncludes = query.Include(f => f.UsuarioCreador);

            var reportData = await queryWithIncludes
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
                    SaldoPendiente = f.Total - f.Cobros.Sum(c => c.Monto),
                    Vendedor = f.UsuarioCreador.PrimerNombre + " " + f.UsuarioCreador.PrimerApellido
                })
                .OrderBy(dto => dto.DiasVencida)
                .ToListAsync();

            reportData.ForEach(r => r.DiasVencida = Math.Max(0, r.DiasVencida));

            return reportData;
        }

        public async Task<byte[]> GetCuentasPorCobrarAsPdfAsync(Guid? userId)
        {
            var data = await GetCuentasPorCobrarAsync(userId);
            if (data == null || !data.Any()) return Array.Empty<byte>();
            return _pdfGeneratorService.GenerateCuentasPorCobrarPdf(data);
        }

        public async Task<IEnumerable<NotasDeCreditoReportDto>> GetNotasDeCreditoAsync(DateTime fechaInicio, DateTime fechaFin, Guid? userId)
        {
            var startDate = DateTime.SpecifyKind(fechaInicio.Date, DateTimeKind.Utc);
            var endDate = DateTime.SpecifyKind(fechaFin.Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc);

            var query = _context.NotasDeCredito
                .AsNoTracking()
                .Where(nc => nc.FechaEmision >= startDate && nc.FechaEmision <= endDate);

            if (userId.HasValue)
            {
                query = query.Where(nc => nc.UsuarioIdCreador == userId.Value);
            }

            var queryWithIncludes = query.Include(nc => nc.UsuarioCreador);

            var reportData = await queryWithIncludes
                .Select(nc => new NotasDeCreditoReportDto
                {
                    NumeroNotaCredito = nc.NumeroNotaCredito,
                    FechaEmision = nc.FechaEmision,
                    NombreCliente = nc.Cliente.RazonSocial,
                    FacturaModificada = nc.Factura.NumeroFactura,
                    Motivo = nc.RazonModificacion,
                    ValorTotal = nc.Total,
                    Vendedor = nc.UsuarioCreador.PrimerNombre + " " + nc.UsuarioCreador.PrimerApellido
                })
                .OrderByDescending(dto => dto.FechaEmision)
                .ToListAsync();

            return reportData;
        }

        public async Task<byte[]> GetNotasDeCreditoAsPdfAsync(DateTime fechaInicio, DateTime fechaFin, Guid? userId)
        {
            var data = await GetNotasDeCreditoAsync(fechaInicio, fechaFin, userId);
            if (data == null || !data.Any()) return Array.Empty<byte>();
            return _pdfGeneratorService.GenerateNotasDeCreditoPdf(data, fechaInicio, fechaFin);
        }

        // ----------------------- REPORTES DE BODEGA -----------------------

        public async Task<IEnumerable<StockActualDto>> GetStockActualAsync(Guid? userId, bool hideZeroStock)
        {
            var query = _context.Productos.AsNoTracking().Where(p => p.EstaActivo);

            if (hideZeroStock)
            {
                query = query.Where(p => p.StockTotal != 0);
            }

            var reportData = await query
                .Select(p => new StockActualDto
                {
                    CodigoPrincipal = p.CodigoPrincipal,
                    NombreProducto = p.Nombre,
                    Categoria = p.Categoria.Nombre,
                    StockTotal = p.StockTotal,
                    PrecioCompraPromedioPonderado = p.PrecioCompraPromedioPonderado,
                    ValorInventario = 0
                })
                .OrderBy(p => p.NombreProducto)
                .ToListAsync();

            foreach (var item in reportData)
            {
                item.ValorInventario = (decimal)item.StockTotal * item.PrecioCompraPromedioPonderado;
            }

            return reportData;
        }

        public async Task<byte[]> GetStockActualAsPdfAsync(Guid? userId, bool hideZeroStock)
        {
            var data = await GetStockActualAsync(userId, hideZeroStock);
            if (data == null || !data.Any()) return Array.Empty<byte>();
            return _pdfGeneratorService.GenerateStockActualPdf(data, hideZeroStock);
        }

        // CORREGIDO: Método de Movimientos con JOINS explícitos para evitar errores de navegación y warnings
        public async Task<IEnumerable<MovimientoInventarioDto>> GetMovimientosInventarioAsync(DateTime fechaInicio, DateTime fechaFin, Guid? userId)
        {
            var startDate = DateTime.SpecifyKind(fechaInicio.Date, DateTimeKind.Utc);
            var endDate = DateTime.SpecifyKind(fechaFin.Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc);

            // 1. Ventas (Usamos Include porque Factura sí tiene navegación, pero un Join es más seguro para el select)
            var salesMovements = await _context.FacturaDetalles
                .AsNoTracking()
                .Include(d => d.Factura).ThenInclude(f => f.UsuarioCreador) // Aseguramos carga de usuario
                .Include(d => d.Producto)
                .Where(d => d.Factura.FechaEmision >= startDate && d.Factura.FechaEmision <= endDate && d.Factura.Estado != EstadoFactura.Cancelada)
                .Select(d => new MovimientoInventarioDto
                {
                    Fecha = d.Factura.FechaEmision,
                    ProductoNombre = d.Producto.Nombre,
                    TipoMovimiento = "Venta",
                    DocumentoReferencia = d.Factura.NumeroFactura,
                    Entrada = 0,
                    Salida = d.Cantidad,
                    UsuarioResponsable = d.Factura.UsuarioCreador.PrimerNombre + " " + d.Factura.UsuarioCreador.PrimerApellido
                }).ToListAsync();

            // 2. Compras (CORREGIDO: Usamos JOIN explícito porque CuentaPorPagar no tiene navegación UsuarioCreador)
            var purchaseMovements = await _context.CuentasPorPagar
                .AsNoTracking()
                .Where(c => c.FechaEmision >= startDate && c.FechaEmision <= endDate && c.Estado != EstadoCompra.Cancelada)
                .Join(_context.Usuarios,
                      c => c.UsuarioIdCreador,
                      u => u.Id,
                      (c, u) => new { c, u })
                .Join(_context.Productos,
                      cu => cu.c.ProductoId,
                      p => p.Id,
                      (cu, p) => new MovimientoInventarioDto
                      {
                          Fecha = cu.c.FechaEmision,
                          ProductoNombre = p.Nombre,
                          TipoMovimiento = "Compra",
                          DocumentoReferencia = string.IsNullOrEmpty(cu.c.NumeroFacturaProveedor) ? $"Int #{cu.c.NumeroCompraInterno}" : $"Prov {cu.c.NumeroFacturaProveedor}",
                          Entrada = cu.c.Cantidad,
                          Salida = 0,
                          UsuarioResponsable = cu.u.PrimerNombre + " " + cu.u.PrimerApellido
                      })
                .ToListAsync();

            // 3. Ajustes
            var adjustmentQuery = await _context.AjustesInventario
                .AsNoTracking()
                .Where(a => a.Fecha >= startDate && a.Fecha <= endDate)
                .Join(_context.Productos, a => a.ProductoId, p => p.Id, (a, p) => new { a, p })
                .Join(_context.Usuarios, x => x.a.UsuarioIdAutoriza, u => u.Id, (x, u) => new { x.a, x.p, u })
                .Select(x => new
                {
                    x.a.Fecha,
                    Producto = x.p.Nombre,
                    x.a.Tipo,
                    x.a.Motivo,
                    x.a.CantidadAjustada,
                    Usuario = x.u.PrimerNombre + " " + x.u.PrimerApellido
                })
                .ToListAsync();

            var mappedAdjustments = adjustmentQuery.Select(x => {
                bool esSalida = x.Tipo == TipoAjusteInventario.Daño || x.Tipo == TipoAjusteInventario.Perdida || x.Tipo == TipoAjusteInventario.AnulacionCompra;
                return new MovimientoInventarioDto
                {
                    Fecha = x.Fecha,
                    ProductoNombre = x.Producto,
                    TipoMovimiento = "Ajuste " + x.Tipo.ToString(),
                    DocumentoReferencia = x.Motivo,
                    Entrada = esSalida ? 0 : x.CantidadAjustada,
                    Salida = esSalida ? x.CantidadAjustada : 0,
                    UsuarioResponsable = x.Usuario
                };
            }).ToList();

            // Unimos todo en una lista fuertemente tipada para evitar warning CS8619
            List<MovimientoInventarioDto> allMovements = new List<MovimientoInventarioDto>();
            allMovements.AddRange(salesMovements);
            allMovements.AddRange(purchaseMovements);
            allMovements.AddRange(mappedAdjustments);

            return allMovements.OrderBy(m => m.Fecha).ToList();
        }

        public async Task<byte[]> GetMovimientosInventarioAsPdfAsync(DateTime fechaInicio, DateTime fechaFin, Guid? userId)
        {
            var data = await GetMovimientosInventarioAsync(fechaInicio, fechaFin, userId);
            if (data == null || !data.Any()) return Array.Empty<byte>();
            return _pdfGeneratorService.GenerateMovimientosInventarioPdf(data, fechaInicio, fechaFin);
        }

        public async Task<IEnumerable<ComprasPorPeriodoDto>> GetComprasPorPeriodoAsync(DateTime fechaInicio, DateTime fechaFin, Guid? userId)
        {
            var startDate = DateTime.SpecifyKind(fechaInicio.Date, DateTimeKind.Utc);
            var endDate = DateTime.SpecifyKind(fechaFin.Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc);

            var query = from c in _context.CuentasPorPagar.AsNoTracking()
                        join p in _context.Productos on c.ProductoId equals p.Id
                        join u in _context.Usuarios on c.UsuarioIdCreador equals u.Id
                        join l in _context.Lotes on c.LoteId equals l.Id into lJoin
                        from l in lJoin.DefaultIfEmpty()
                        where c.FechaEmision >= startDate && c.FechaEmision <= endDate
                        && c.Estado != EstadoCompra.Cancelada
                        select new ComprasPorPeriodoDto
                        {
                            Fecha = c.FechaEmision,
                            ProductoNombre = p.Nombre,
                            NombreProveedor = c.NombreProveedor,
                            NumeroFacturaProveedor = c.NumeroFacturaProveedor,
                            NumeroCompraInterno = c.NumeroCompraInterno,
                            CantidadComprada = (decimal)c.Cantidad,
                            CostoTotal = c.MontoTotal,
                            CostoUnitario = l != null ? l.PrecioCompraUnitario : (c.Cantidad > 0 ? c.MontoTotal / c.Cantidad : 0),
                            UsuarioResponsable = u.PrimerNombre + " " + u.PrimerApellido
                        };

            return await query.OrderBy(c => c.Fecha).ToListAsync();
        }

        public async Task<byte[]> GetComprasPorPeriodoAsPdfAsync(DateTime fechaInicio, DateTime fechaFin, Guid? userId)
        {
            var data = await GetComprasPorPeriodoAsync(fechaInicio, fechaFin, userId);
            if (data == null || !data.Any()) return Array.Empty<byte>();
            return _pdfGeneratorService.GenerateComprasPorPeriodoPdf(data, fechaInicio, fechaFin);
        }

        public async Task<IEnumerable<ProductoStockMinimoDto>> GetProductosBajoStockMinimoAsync(Guid? userId)
        {
            var reportData = await _context.Productos
                .AsNoTracking()
                .Where(p => p.EstaActivo && p.ManejaInventario && p.StockTotal < p.StockMinimo)
                .Select(p => new ProductoStockMinimoDto
                {
                    CodigoPrincipal = p.CodigoPrincipal,
                    NombreProducto = p.Nombre,
                    StockActual = p.StockTotal,
                    StockMinimo = p.StockMinimo,
                    CantidadFaltante = p.StockMinimo - p.StockTotal,
                    CostoPromedioUnitario = p.PrecioCompraPromedioPonderado
                })
                .OrderBy(p => p.NombreProducto)
                .ToListAsync();

            foreach (var item in reportData)
            {
                item.CostoEstimadoReposicion = item.CantidadFaltante * item.CostoPromedioUnitario;
            }

            return reportData;
        }

        public async Task<byte[]> GetProductosBajoStockMinimoAsPdfAsync(Guid? userId)
        {
            var data = await GetProductosBajoStockMinimoAsync(userId);
            if (data == null || !data.Any()) return Array.Empty<byte>();
            return _pdfGeneratorService.GenerateProductosBajoStockMinimoPdf(data);
        }

        public async Task<IEnumerable<AjusteInventarioReportDto>> GetAjustesInventarioAsync(DateTime fechaInicio, DateTime fechaFin, Guid? userId)
        {
            var startDate = DateTime.SpecifyKind(fechaInicio.Date, DateTimeKind.Utc);
            var endDate = DateTime.SpecifyKind(fechaFin.Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc);

            var query = from ajuste in _context.AjustesInventario.AsNoTracking()
                        join producto in _context.Productos on ajuste.ProductoId equals producto.Id
                        join usuario in _context.Usuarios on ajuste.UsuarioIdAutoriza equals usuario.Id
                        where ajuste.Fecha >= startDate && ajuste.Fecha <= endDate
                        select new AjusteInventarioReportDto
                        {
                            Fecha = ajuste.Fecha,
                            ProductoNombre = producto.Nombre,
                            TipoAjuste = ajuste.Tipo.ToString(),
                            CantidadAjustada = ajuste.CantidadAjustada,
                            Motivo = ajuste.Motivo,
                            UsuarioResponsable = usuario.PrimerNombre + " " + usuario.PrimerApellido
                        };

            return await query.OrderBy(a => a.Fecha).ToListAsync();
        }

        public async Task<byte[]> GetAjustesInventarioAsPdfAsync(DateTime fechaInicio, DateTime fechaFin, Guid? userId)
        {
            var data = await GetAjustesInventarioAsync(fechaInicio, fechaFin, userId);
            if (data == null || !data.Any()) return Array.Empty<byte>();
            return _pdfGeneratorService.GenerateAjustesInventarioPdf(data, fechaInicio, fechaFin);
        }
    }
}