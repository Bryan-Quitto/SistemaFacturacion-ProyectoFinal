using FacturasSRI.Application.Dtos;
using FacturasSRI.Application.Interfaces;
using FacturasSRI.Domain.Entities;
using FacturasSRI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FacturasSRI.Infrastructure.Services
{
    public class PurchaseService : IPurchaseService
    {
        private readonly FacturasSRIDbContext _context;
        private readonly ILogger<PurchaseService> _logger;

        public PurchaseService(FacturasSRIDbContext context, ILogger<PurchaseService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<bool> CreatePurchaseAsync(PurchaseDto purchaseDto)
        {
            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    var producto = await _context.Productos.FindAsync(purchaseDto.ProductoId);
                    if (producto == null) throw new InvalidOperationException("El producto no existe.");
                    if (!producto.ManejaInventario) throw new InvalidOperationException("No se puede registrar una compra para un producto que no maneja inventario.");

                    if (producto.ManejaLotes)
                    {
                        var lote = new Lote
                        {
                            Id = Guid.NewGuid(),
                            ProductoId = purchaseDto.ProductoId,
                            CantidadComprada = purchaseDto.Cantidad,
                            CantidadDisponible = purchaseDto.Cantidad,
                            PrecioCompraUnitario = purchaseDto.PrecioCosto,
                            FechaCompra = DateTime.UtcNow,
                            FechaCaducidad = purchaseDto.FechaCaducidad?.ToUniversalTime(),
                            FechaCreacion = DateTime.UtcNow
                        };
                        _context.Lotes.Add(lote);

                        var cuentaPorPagarLote = new CuentaPorPagar
                        {
                            Id = Guid.NewGuid(),
                            LoteId = lote.Id,
                            Proveedor = purchaseDto.Proveedor,
                            NumeroFactura = purchaseDto.NumeroFactura,
                            FechaEmision = DateTime.UtcNow,
                            FechaVencimiento = DateTime.UtcNow.AddDays(30),
                            MontoTotal = purchaseDto.Cantidad * purchaseDto.PrecioCosto,
                            SaldoPendiente = purchaseDto.Cantidad * purchaseDto.PrecioCosto,
                            FechaCreacion = DateTime.UtcNow
                        };
                        _context.CuentasPorPagar.Add(cuentaPorPagarLote);
                    }
                    else
                    {
                        producto.StockTotal += purchaseDto.Cantidad;
                        
                        var cuentaPorPagarGeneral = new CuentaPorPagar
                        {
                            Id = Guid.NewGuid(),
                            LoteId = null,
                            Proveedor = purchaseDto.Proveedor,
                            NumeroFactura = purchaseDto.NumeroFactura,
                            FechaEmision = DateTime.UtcNow,
                            FechaVencimiento = DateTime.UtcNow.AddDays(30),
                            MontoTotal = purchaseDto.Cantidad * purchaseDto.PrecioCosto,
                            SaldoPendiente = purchaseDto.Cantidad * purchaseDto.PrecioCosto,
                            FechaCreacion = DateTime.UtcNow
                        };
                        _context.CuentasPorPagar.Add(cuentaPorPagarGeneral);
                    }
                    
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                    return true;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "EXCEPCIÓN al crear la compra. Revirtiendo transacción.");
                    await transaction.RollbackAsync();
                    return false;
                }
            }
        }
        
        public async Task<List<PurchaseListItemDto>> GetPurchasesAsync()
        {
            var purchases = await _context.Lotes
                .Include(lote => lote.Producto)
                .Select(lote => new 
                {
                    Lote = lote,
                    CuentaPorPagar = _context.CuentasPorPagar.FirstOrDefault(c => c.LoteId == lote.Id)
                })
                .OrderByDescending(x => x.Lote.FechaCompra)
                .Select(x => new PurchaseListItemDto
                {
                    LoteId = x.Lote.Id,
                    ProductName = x.Lote.Producto.Nombre,
                    CantidadComprada = x.Lote.CantidadComprada,
                    CantidadDisponible = x.Lote.CantidadDisponible,
                    PrecioCompraUnitario = x.Lote.PrecioCompraUnitario,
                    ValorTotalCompra = x.Lote.CantidadComprada * x.Lote.PrecioCompraUnitario,
                    FechaCompra = x.Lote.FechaCompra,
                    FechaCaducidad = x.Lote.FechaCaducidad,
                    Proveedor = x.CuentaPorPagar != null ? x.CuentaPorPagar.Proveedor : "N/A"
                })
                .ToListAsync();

            return purchases;
        }
    }
}