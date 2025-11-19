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
            // The file upload logic will be fully implemented in Phase 2.
            // For now, this just makes the project buildable.

            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    var producto = await _context.Productos.FindAsync(purchaseDto.ProductoId);
                    if (producto == null) throw new InvalidOperationException("El producto no existe.");
                    if (!producto.ManejaInventario) throw new InvalidOperationException("No se puede registrar una compra para un producto que no maneja inventario.");

                    decimal montoTotal = purchaseDto.Cantidad * purchaseDto.PrecioCosto;

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
                            UsuarioIdCreador = purchaseDto.UsuarioIdCreador,
                            FechaCreacion = DateTime.UtcNow
                        };
                        _context.Lotes.Add(lote);

                        var cuentaPorPagarLote = new CuentaPorPagar
                        {
                            Id = Guid.NewGuid(),
                            LoteId = lote.Id,
                            ProductoId = purchaseDto.ProductoId,
                            NombreProveedor = purchaseDto.NombreProveedor,
                            ComprobantePath = purchaseDto.ComprobantePath,
                            FechaEmision = DateTime.UtcNow,
                            FechaVencimiento = DateTime.UtcNow.AddDays(30),
                            MontoTotal = montoTotal,
                            Cantidad = purchaseDto.Cantidad,
                            SaldoPendiente = montoTotal, // Saldo pendiente es el total al crear
                            Pagada = false, // La cuenta se crea como no pagada
                            UsuarioIdCreador = purchaseDto.UsuarioIdCreador,
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
                            ProductoId = purchaseDto.ProductoId,
                            NombreProveedor = purchaseDto.NombreProveedor,
                            ComprobantePath = purchaseDto.ComprobantePath,
                            FechaEmision = DateTime.UtcNow,
                            FechaVencimiento = DateTime.UtcNow.AddDays(30),
                            MontoTotal = montoTotal,
                            Cantidad = purchaseDto.Cantidad,
                            SaldoPendiente = montoTotal, // Saldo pendiente es el total al crear
                            Pagada = false, // La cuenta se crea como no pagada
                            UsuarioIdCreador = purchaseDto.UsuarioIdCreador,
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
            var purchases = await (
                from cpp in _context.CuentasPorPagar
                join producto in _context.Productos on cpp.ProductoId equals producto.Id
                join usuario in _context.Usuarios on cpp.UsuarioIdCreador equals usuario.Id into usuarioJoin
                from user in usuarioJoin.DefaultIfEmpty()
                // LEFT JOIN to Lotes
                join lote in _context.Lotes on cpp.LoteId equals lote.Id into loteJoin
                from lote in loteJoin.DefaultIfEmpty()
                orderby cpp.FechaCreacion descending
                select new PurchaseListItemDto
                {
                    CuentaPorPagarId = cpp.Id,
                    LoteId = lote != null ? lote.Id : Guid.Empty,
                    ProductName = producto.Nombre,
                    CantidadComprada = cpp.Cantidad,
                    CantidadDisponible = lote != null ? lote.CantidadDisponible : producto.StockTotal,
                    PrecioCompraUnitario = (cpp.Cantidad > 0) ? (cpp.MontoTotal / cpp.Cantidad) : 0,
                    ValorTotalCompra = cpp.MontoTotal,
                    FechaCompra = cpp.FechaEmision,
                    FechaCaducidad = lote != null ? lote.FechaCaducidad : null,
                    NombreProveedor = cpp.NombreProveedor,
                    ComprobantePath = cpp.ComprobantePath,
                    CreadoPor = user != null ? user.PrimerNombre + " " + user.PrimerApellido : "N/A"
                }
            ).ToListAsync();

            return purchases;
        }
    }
}
