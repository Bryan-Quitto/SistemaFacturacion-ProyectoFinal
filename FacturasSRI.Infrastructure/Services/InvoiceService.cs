using FacturasSRI.Application.Dtos;
using FacturasSRI.Application.Interfaces;
using FacturasSRI.Domain.Entities;
using FacturasSRI.Domain.Enums;
using FacturasSRI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FacturasSRI.Infrastructure.Services
{
    public class InvoiceService : IInvoiceService
    {
        private readonly FacturasSRIDbContext _context;
        private readonly ILogger<InvoiceService> _logger;
        private readonly IConfiguration _configuration;

        public InvoiceService(FacturasSRIDbContext context, ILogger<InvoiceService> logger, IConfiguration configuration)
        {
            _context = context;
            _logger = logger;
            _configuration = configuration;
        }
        
        public async Task<InvoiceDto> CreateInvoiceAsync(CreateInvoiceDto invoiceDto)
        {
            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    var establishmentCode = _configuration["CompanyInfo:EstablishmentCode"];
                    var emissionPointCode = _configuration["CompanyInfo:EmissionPointCode"];

                    var secuencial = await _context.Secuenciales
                        .FirstOrDefaultAsync(s => s.Establecimiento == establishmentCode && s.PuntoEmision == emissionPointCode);

                    if (secuencial == null)
                    {
                        secuencial = new Secuencial { 
                            Id = Guid.NewGuid(),
                            Establecimiento = establishmentCode!, 
                            PuntoEmision = emissionPointCode!, 
                            UltimoSecuencialFactura = 0 
                        };
                        _context.Secuenciales.Add(secuencial);
                    }
                    
                    secuencial.UltimoSecuencialFactura++;
                    var numeroFactura = $"{establishmentCode}-{emissionPointCode}-{secuencial.UltimoSecuencialFactura:D9}";
                    
                    var invoice = new Factura
                    {
                        Id = Guid.NewGuid(),
                        ClienteId = invoiceDto.ClienteId,
                        FechaEmision = DateTime.UtcNow,
                        NumeroFactura = numeroFactura,
                        Estado = EstadoFactura.Generada,
                        UsuarioIdCreador = Guid.Parse("1252d27c-ea79-4b10-94ee-607e9bac4658"), 
                        FechaCreacion = DateTime.UtcNow
                    };

                    decimal subtotalSinImpuestos = 0;
                    decimal totalIva = 0;

                    foreach (var item in invoiceDto.Items)
                    {
                        var producto = await _context.Productos
                            .Include(p => p.ProductoImpuestos)
                            .ThenInclude(pi => pi.Impuesto)
                            .SingleAsync(p => p.Id == item.ProductoId);

                        decimal valorIvaItem = 0;
                        var impuestoIva = producto.ProductoImpuestos.FirstOrDefault(pi => pi.Impuesto.CodigoSRI == "2");
                        if (impuestoIva != null)
                        {
                            valorIvaItem = (producto.PrecioVentaUnitario * (impuestoIva.Impuesto.Porcentaje / 100)) * item.Cantidad;
                        }

                        var detalle = new FacturaDetalle
                        {
                            Id = Guid.NewGuid(),
                            FacturaId = invoice.Id,
                            ProductoId = item.ProductoId,
                            Cantidad = item.Cantidad,
                            PrecioVentaUnitario = producto.PrecioVentaUnitario,
                            Subtotal = item.Cantidad * producto.PrecioVentaUnitario,
                            ValorIVA = valorIvaItem,
                        };

                        if (producto.ManejaInventario)
                        {
                            if (producto.ManejaLotes)
                            {
                                await DescontarStockDeLotes(detalle);
                            }
                            else
                            {
                                await DescontarStockGeneral(producto, detalle.Cantidad);
                            }
                        }

                        invoice.Detalles.Add(detalle);
                        subtotalSinImpuestos += detalle.Subtotal;
                        totalIva += valorIvaItem;
                    }

                    invoice.SubtotalSinImpuestos = subtotalSinImpuestos;
                    invoice.TotalIVA = totalIva;
                    invoice.Total = subtotalSinImpuestos + totalIva;
                    
                    var cuentaPorCobrar = new CuentaPorCobrar
                    {
                        Id = Guid.NewGuid(),
                        FacturaId = invoice.Id,
                        ClienteId = invoice.ClienteId,
                        FechaEmision = invoice.FechaEmision,
                        FechaVencimiento = invoice.FechaEmision.AddDays(30),
                        MontoTotal = invoice.Total,
                        SaldoPendiente = invoice.Total,
                        UsuarioIdCreador = Guid.Parse("1252d27c-ea79-4b10-94ee-607e9bac4658"),
                        FechaCreacion = DateTime.UtcNow
                    };
                    
                    _context.Facturas.Add(invoice);
                    _context.CuentasPorCobrar.Add(cuentaPorCobrar);
                    
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    var resultDto = await GetInvoiceByIdAsync(invoice.Id);
                    return resultDto!;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error al crear la factura.");
                    await transaction.RollbackAsync();
                    throw;
                }
            }   
        }

        private async Task DescontarStockGeneral(Producto producto, int cantidadADescontar)
        {
            if (producto.StockTotal < cantidadADescontar)
            {
                throw new InvalidOperationException($"No hay stock suficiente para '{producto.Nombre}'. Stock disponible: {producto.StockTotal}, se requieren: {cantidadADescontar}.");
            }
            producto.StockTotal -= cantidadADescontar;
        }

        private async Task DescontarStockDeLotes(FacturaDetalle detalle)
        {
            var cantidadADescontar = detalle.Cantidad;
            var lotesDisponibles = await _context.Lotes
                .Where(l => l.ProductoId == detalle.ProductoId && l.CantidadDisponible > 0)
                .OrderBy(l => l.FechaCompra)
                .ToListAsync();

            var stockTotal = lotesDisponibles.Sum(l => l.CantidadDisponible);
            if (stockTotal < cantidadADescontar)
            {
                var producto = await _context.Productos.FindAsync(detalle.ProductoId);
                var nombreProducto = producto?.Nombre ?? $"Producto ID {detalle.ProductoId}";
                throw new InvalidOperationException($"No hay stock suficiente para '{nombreProducto}'. Stock disponible: {stockTotal}, se requieren: {cantidadADescontar}.");
            }

            foreach (var lote in lotesDisponibles)
            {
                if (cantidadADescontar <= 0) break;

                int cantidadConsumida = Math.Min(lote.CantidadDisponible, cantidadADescontar);

                lote.CantidadDisponible -= cantidadConsumida;
                cantidadADescontar -= cantidadConsumida;

                var consumoDeLote = new FacturaDetalleConsumoLote
                {
                    Id = Guid.NewGuid(),
                    FacturaDetalleId = detalle.Id,
                    LoteId = lote.Id,
                    CantidadConsumida = cantidadConsumida
                };
                _context.FacturaDetalleConsumoLotes.Add(consumoDeLote);
            }
        }

        public async Task<InvoiceDto?> GetInvoiceByIdAsync(Guid id)
        {
             var invoice = await _context.Facturas
                .Include(i => i.Detalles)
                .ThenInclude(d => d.Producto)
                .FirstOrDefaultAsync(i => i.Id == id);

            if (invoice == null) return null;

            return new InvoiceDto
            {
                Id = invoice.Id,
                FechaEmision = invoice.FechaEmision,
                NumeroFactura = invoice.NumeroFactura,
                ClienteId = invoice.ClienteId,
                SubtotalSinImpuestos = invoice.SubtotalSinImpuestos,
                TotalDescuento = invoice.TotalDescuento,
                TotalIVA = invoice.TotalIVA,
                Total = invoice.Total,
                Detalles = invoice.Detalles.Select(d => new InvoiceDetailDto
                {
                    Id = d.Id,
                    ProductoId = d.ProductoId,
                    Cantidad = d.Cantidad,
                    PrecioVentaUnitario = d.PrecioVentaUnitario,
                    Descuento = d.Descuento,
                    Subtotal = d.Subtotal
                }).ToList()
            };
        }

        public async Task<List<InvoiceDto>> GetInvoicesAsync()
        {
            return await _context.Facturas
                .OrderByDescending(i => i.FechaCreacion)
                .Select(invoice => new InvoiceDto
                {
                    Id = invoice.Id,
                    FechaEmision = invoice.FechaEmision,
                    NumeroFactura = invoice.NumeroFactura,
                    ClienteId = invoice.ClienteId,
                    Total = invoice.Total
                }).ToListAsync();
        }
    }
}