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
                private readonly ReportPdfGeneratorService _pdfGeneratorService;
        
                public ReportService(FacturasSRIDbContext context, ReportPdfGeneratorService pdfGeneratorService)
                {
                    _context = context;
                    _pdfGeneratorService = pdfGeneratorService;
                }
        
                public async Task<IEnumerable<VentasPorPeriodoDto>> GetVentasPorPeriodoAsync(DateTime fechaInicio, DateTime fechaFin, Guid? userId)
                {
                    // The dates are UTC from the endpoint. Using .Date resets the Kind to Unspecified.
                    // We must explicitly set it back to Utc for Npgsql to work correctly.
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
                    if (data == null || !data.Any())
                    {
                        return Array.Empty<byte>();
                    }
                    return _pdfGeneratorService.GenerateVentasPorPeriodoPdf(data, fechaInicio, fechaFin);
                }
        
                public async Task<IEnumerable<VentasPorProductoDto>> GetVentasPorProductoAsync(DateTime fechaInicio, DateTime fechaFin, Guid? userId)
                {
                    var startDate = DateTime.SpecifyKind(fechaInicio.Date, DateTimeKind.Utc);
                    var endDate = DateTime.SpecifyKind(fechaFin.Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc);
        
                    var query = _context.FacturaDetalles
                        .AsNoTracking()
                        .Where(d => d.Factura.FechaEmision >= startDate && d.Factura.FechaEmision <= endDate && d.Factura.Estado != EstadoFactura.Cancelada)
                        .Join(_context.Facturas, // Join with Facturas
                            detalle => detalle.FacturaId,
                            factura => factura.Id,
                            (detalle, factura) => new { detalle, factura })
                        .Join(_context.Usuarios, // Join with Usuarios
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
                            Fecha = x.factura.FechaEmision.Date, // Group by date
                            Vendedor = x.usuario.PrimerNombre + " " + x.usuario.PrimerApellido // Group by seller
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
                        .OrderBy(dto => dto.Fecha) // Order by date first
                        .ThenBy(dto => dto.Vendedor) // Then by seller
                        .ThenByDescending(dto => dto.TotalVendido)
                        .ToListAsync();
        
                    return reportData;
                }
        
                public async Task<byte[]> GetVentasPorProductoAsPdfAsync(DateTime fechaInicio, DateTime fechaFin, Guid? userId)
                {
                    var data = await GetVentasPorProductoAsync(fechaInicio, fechaFin, userId);
                    if (data == null || !data.Any())
                    {
                        return Array.Empty<byte>();
                    }
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
        
                public async Task<byte[]> GetActividadClientesAsPdfAsync(DateTime fechaInicio, DateTime fechaFin, Guid? userId)
                {
                    var data = await GetActividadClientesAsync(fechaInicio, fechaFin, userId);
                    if (data == null || !data.Any())
                    {
                        return Array.Empty<byte>();
                    }
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
                            Vendedor = f.UsuarioCreador.PrimerNombre + " " + f.UsuarioCreador.PrimerApellido // Add Vendedor
                        })
                        .OrderBy(dto => dto.DiasVencida)
                        .ToListAsync();
                    
                    // Filter out negative days vencida which means it's not due yet.
                    reportData.ForEach(r => r.DiasVencida = Math.Max(0, r.DiasVencida));
        
                    return reportData;
                }
        
                public async Task<byte[]> GetCuentasPorCobrarAsPdfAsync(Guid? userId)
                {
                    var data = await GetCuentasPorCobrarAsync(userId);
                    if (data == null || !data.Any())
                    {
                        return Array.Empty<byte>();
                    }
                    return _pdfGeneratorService.GenerateCuentasPorCobrarPdf(data);
                }
        
                public async Task<IEnumerable<StockActualDto>> GetStockActualAsync(Guid? userId)
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
        
                public async Task<byte[]> GetStockActualAsPdfAsync(Guid? userId)
                {
                    var data = await GetStockActualAsync(userId);
                    if (data == null || !data.Any())
                    {
                        return Array.Empty<byte>();
                    }
                    return _pdfGeneratorService.GenerateStockActualPdf(data);
                }
        
                public async Task<IEnumerable<MovimientoInventarioDto>> GetMovimientosInventarioAsync(DateTime fechaInicio, DateTime fechaFin, Guid? userId)
                {
                    var startDate = DateTime.SpecifyKind(fechaInicio.Date, DateTimeKind.Utc);
                    var endDate = DateTime.SpecifyKind(fechaFin.Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc);
        
                    // 1. Sales (Salidas)
                    var salesMovements = await _context.FacturaDetalles
                        .AsNoTracking()
                        .Where(d => d.Factura.FechaEmision >= startDate && d.Factura.FechaEmision <= endDate && d.Factura.Estado != EstadoFactura.Cancelada)
                        .Select(d => new MovimientoInventarioDto
                        {
                            Fecha = d.Factura.FechaEmision,
                            ProductoNombre = d.Producto.Nombre,
                            TipoMovimiento = "Venta",
                            DocumentoReferencia = d.Factura.NumeroFactura,
                            Cantidad = -d.Cantidad // Negative for outgoing stock
                        }).ToListAsync();
        
                    // 2. Purchases (Entradas)
                    var purchaseMovements = await _context.Lotes
                        .AsNoTracking()
                        .Where(l => l.FechaCompra >= startDate && l.FechaCompra <= endDate)
                        .Select(l => new MovimientoInventarioDto
                        {
                            Fecha = l.FechaCompra,
                            ProductoNombre = l.Producto.Nombre,
                            TipoMovimiento = "Compra",
                            DocumentoReferencia = "Compra",
                            Cantidad = l.CantidadComprada
                        }).ToListAsync();
                    
                    // 3. Adjustments (Entradas/Salidas)
                    var adjustmentMovements = await _context.AjustesInventario
                        .AsNoTracking()
                        .Where(a => a.Fecha >= startDate && a.Fecha <= endDate)
                        .Join(_context.Productos, // Manual join
                            ajuste => ajuste.ProductoId,
                            producto => producto.Id,
                            (ajuste, producto) => new MovimientoInventarioDto
                            {
                                Fecha = ajuste.Fecha,
                                ProductoNombre = producto.Nombre,
                                TipoMovimiento = "Ajuste " + ajuste.Tipo.ToString(),
                                DocumentoReferencia = ajuste.Motivo,
                                // Assume Daño, Perdida, AnulacionCompra are negative adjustments
                                Cantidad = (ajuste.Tipo == TipoAjusteInventario.Daño || ajuste.Tipo == TipoAjusteInventario.Perdida || ajuste.Tipo == TipoAjusteInventario.AnulacionCompra)
                                           ? -ajuste.CantidadAjustada
                                           : ajuste.CantidadAjustada // Treat Conteo, Inicial, Otro as positive for now
                            })
                        .ToListAsync();
        
                    // Combine all movements and order by date
                    var allMovements = salesMovements
                        .Concat(purchaseMovements)
                        .Concat(adjustmentMovements)
                        .OrderBy(m => m.Fecha)
                        .ToList();
                    
                    return allMovements;
                }
        
                public async Task<byte[]> GetMovimientosInventarioAsPdfAsync(DateTime fechaInicio, DateTime fechaFin, Guid? userId)
                {
                    var data = await GetMovimientosInventarioAsync(fechaInicio, fechaFin, userId);
                    if (data == null || !data.Any())
                    {
                        return Array.Empty<byte>();
                    }
                    return _pdfGeneratorService.GenerateMovimientosInventarioPdf(data, fechaInicio, fechaFin);
                }
        
                public async Task<IEnumerable<ComprasPorPeriodoDto>> GetComprasPorPeriodoAsync(DateTime fechaInicio, DateTime fechaFin, Guid? userId)
                {
                    var startDate = DateTime.SpecifyKind(fechaInicio.Date, DateTimeKind.Utc);
                    var endDate = DateTime.SpecifyKind(fechaFin.Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc);
        
                    var rawLotesData = await _context.Lotes
                        .AsNoTracking()
                        .Where(l => l.FechaCompra >= startDate && l.FechaCompra <= endDate)
                        .Select(l => new 
                        {
                            ProductoNombre = l.Producto.Nombre,
                            l.CantidadComprada,
                            l.PrecioCompraUnitario
                        })
                        .ToListAsync();
        
                    var reportData = rawLotesData
                        .GroupBy(l => l.ProductoNombre)
                        .Select(g => new ComprasPorPeriodoDto
                        {
                            ProductoNombre = g.Key,
                            CantidadComprada = (decimal)g.Sum(l => l.CantidadComprada),
                            CostoTotal = g.Sum(l => (decimal)l.CantidadComprada * l.PrecioCompraUnitario),
                            CostoPromedio = g.Sum(l => (decimal)l.CantidadComprada * l.PrecioCompraUnitario) / (decimal)g.Sum(l => l.CantidadComprada)
                        })
                        .OrderBy(r => r.ProductoNombre)
                        .ToList();
        
                    return reportData;
                }
        
                public async Task<byte[]> GetComprasPorPeriodoAsPdfAsync(DateTime fechaInicio, DateTime fechaFin, Guid? userId)
                {
                    var data = await GetComprasPorPeriodoAsync(fechaInicio, fechaFin, userId);
                    if (data == null || !data.Any())
                    {
                        return Array.Empty<byte>();
                    }
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
                            CantidadFaltante = p.StockMinimo - p.StockTotal
                        })
                        .OrderBy(p => p.NombreProducto)
                        .ToListAsync();
        
                    return reportData;
                }
        
                public async Task<byte[]> GetProductosBajoStockMinimoAsPdfAsync(Guid? userId)
                {
                    var data = await GetProductosBajoStockMinimoAsync(userId);
                    if (data == null || !data.Any())
                    {
                        return Array.Empty<byte>();
                    }
                    return _pdfGeneratorService.GenerateProductosBajoStockMinimoPdf(data);
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
                            Vendedor = nc.UsuarioCreador.PrimerNombre + " " + nc.UsuarioCreador.PrimerApellido // Add Vendedor
                        })
                        .OrderByDescending(dto => dto.FechaEmision)
                        .ToListAsync();
        
                    return reportData;
                }
        
                public async Task<byte[]> GetNotasDeCreditoAsPdfAsync(DateTime fechaInicio, DateTime fechaFin, Guid? userId)
                {
                    var data = await GetNotasDeCreditoAsync(fechaInicio, fechaFin, userId);
                    if (data == null || !data.Any())
                    {
                        return Array.Empty<byte>();
                    }
                    return _pdfGeneratorService.GenerateNotasDeCreditoPdf(data, fechaInicio, fechaFin);
                }
        
                public async Task<IEnumerable<AjusteInventarioReportDto>> GetAjustesInventarioAsync(DateTime fechaInicio, DateTime fechaFin, Guid? userId)
                {
                    var startDate = DateTime.SpecifyKind(fechaInicio.Date, DateTimeKind.Utc);
                    var endDate = DateTime.SpecifyKind(fechaFin.Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc);
        
                    var reportData = await _context.AjustesInventario
                        .AsNoTracking()
                        .Where(a => a.Fecha >= startDate && a.Fecha <= endDate)
                        .Join(_context.Productos, // Manual join
                            ajuste => ajuste.ProductoId,
                            producto => producto.Id,
                            (ajuste, producto) => new AjusteInventarioReportDto
                            {
                                Fecha = ajuste.Fecha,
                                ProductoNombre = producto.Nombre,
                                TipoAjuste = ajuste.Tipo.ToString(),
                                CantidadAjustada = ajuste.CantidadAjustada,
                                Motivo = ajuste.Motivo
                            })
                        .OrderBy(a => a.Fecha)
                        .ToListAsync();
        
                    return reportData;
                }
        
                public async Task<byte[]> GetAjustesInventarioAsPdfAsync(DateTime fechaInicio, DateTime fechaFin, Guid? userId)
                {
                    var data = await GetAjustesInventarioAsync(fechaInicio, fechaFin, userId);
                    if (data == null || !data.Any())
                    {
                        return Array.Empty<byte>();
                    }
                    return _pdfGeneratorService.GenerateAjustesInventarioPdf(data, fechaInicio, fechaFin);
                }
            }
        }
        