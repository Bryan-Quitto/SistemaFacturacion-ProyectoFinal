using FacturasSRI.Application.Dtos;
using FacturasSRI.Application.Interfaces;
using FacturasSRI.Domain.Entities;
using FacturasSRI.Domain.Enums;
using FacturasSRI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
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

        public InvoiceService(FacturasSRIDbContext context, ILogger<InvoiceService> logger)
        {
            _context = context;
            _logger = logger;
        }
        
        public async Task<InvoiceDto> CreateInvoiceAsync(CreateInvoiceDto invoiceDto)
{
            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    var puntoEmision = await _context.PuntosEmision.Include(p => p.Establecimiento).FirstAsync();
                    var numeroFactura = $"{puntoEmision.Establecimiento.Codigo}-{puntoEmision.Codigo}-{puntoEmision.SecuencialFactura:D9}";
                    var invoice = new Factura
                    {
                        Id = Guid.NewGuid(),
                        ClienteId = invoiceDto.ClienteId,
                        FechaEmision = DateTime.UtcNow,
                        NumeroFactura = numeroFactura,
                        Estado = EstadoFactura.Generada,
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
                        var impuestoIva = producto.ProductoImpuestos.FirstOrDefault(pi => pi.Impuesto.CodigoSRI == "2"); // '2' es el código para IVA
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

                        invoice.Detalles.Add(detalle);
                        subtotalSinImpuestos += detalle.Subtotal;
                        totalIva += valorIvaItem;
                    }

                    invoice.SubtotalSinImpuestos = subtotalSinImpuestos;
                    invoice.TotalIVA = totalIva;
                    invoice.Total = subtotalSinImpuestos + totalIva;

                    puntoEmision.SecuencialFactura++;
                    _context.Facturas.Add(invoice);

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