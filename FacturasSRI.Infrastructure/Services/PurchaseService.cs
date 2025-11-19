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
            if (purchaseDto.ProveedorId == Guid.Empty)
            {
                throw new InvalidOperationException("Debe seleccionar un proveedor para registrar la compra.");
            }

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
                            ProveedorId = purchaseDto.ProveedorId, // Added
                            CantidadComprada = purchaseDto.Cantidad,
                            CantidadDisponible = purchaseDto.Cantidad,
                            PrecioCompraUnitario = purchaseDto.PrecioCosto,
                            FechaCompra = DateTime.UtcNow,
                            FechaCaducidad = purchaseDto.FechaCaducidad?.ToUniversalTime(),
                            UsuarioIdCreador = purchaseDto.UsuarioIdCreador,
                            FechaCreacion = DateTime.UtcNow
                        };
                        _context.Lotes.Add(lote);

                        var proveedor = await _context.Proveedores.FindAsync(purchaseDto.ProveedorId); // Fetch Proveedor
                        var proveedorRazonSocial = proveedor?.RazonSocial ?? "Desconocido";

                        var cuentaPorPagarLote = new CuentaPorPagar
                        {
                            Id = Guid.NewGuid(),
                            LoteId = lote.Id, // Link to the new Lote
                            Proveedor = proveedorRazonSocial, // Populate Proveedor with RazonSocial
                            NumeroFactura = purchaseDto.NumeroFactura, // Populate NumeroFactura
                            FechaEmision = DateTime.UtcNow,
                            FechaVencimiento = DateTime.UtcNow.AddDays(30),
                            MontoTotal = purchaseDto.Cantidad * purchaseDto.PrecioCosto,
                            SaldoPendiente = 0, // Set to 0 as it's paid
                            Pagada = true, // Set to true as per requirement
                            UsuarioIdCreador = purchaseDto.UsuarioIdCreador,
                            FechaCreacion = DateTime.UtcNow
                        };
                        _context.CuentasPorPagar.Add(cuentaPorPagarLote);
                    }
                    else
                    {
                        producto.StockTotal += purchaseDto.Cantidad;
                        
                        var proveedor = await _context.Proveedores.FindAsync(purchaseDto.ProveedorId); // Fetch Proveedor
                        var proveedorRazonSocial = proveedor?.RazonSocial ?? "Desconocido";

                        var cuentaPorPagarGeneral = new CuentaPorPagar
                        {
                            Id = Guid.NewGuid(),
                            LoteId = null,
                            Proveedor = proveedorRazonSocial, // Populate Proveedor with RazonSocial
                            NumeroFactura = purchaseDto.NumeroFactura,
                            FechaEmision = DateTime.UtcNow,
                            FechaVencimiento = DateTime.UtcNow.AddDays(30),
                            MontoTotal = purchaseDto.Cantidad * purchaseDto.PrecioCosto,
                            SaldoPendiente = 0, // Set to 0 as it's paid
                            Pagada = true, // Set to true as per requirement
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
            var purchases = await (from lote in _context.Lotes
                                   join producto in _context.Productos on lote.ProductoId equals producto.Id
                                   join proveedor in _context.Proveedores on lote.ProveedorId equals proveedor.Id into proveedorJoin // Added
                                   from prov in proveedorJoin.DefaultIfEmpty() // Added
                                   join cuentaPorPagar in _context.CuentasPorPagar on lote.Id equals cuentaPorPagar.LoteId into cpps
                                   from cpp in cpps.DefaultIfEmpty()
                                   join usuario in _context.Usuarios on lote.UsuarioIdCreador equals usuario.Id into usuarioJoin
                                   from user in usuarioJoin.DefaultIfEmpty() // Changed alias to user to avoid conflict
                                   orderby lote.FechaCompra descending
                                   select new PurchaseListItemDto
                                   {
                                       LoteId = lote.Id,
                                       ProductName = producto.Nombre,
                                       CantidadComprada = lote.CantidadComprada,
                                       CantidadDisponible = lote.CantidadDisponible,
                                       PrecioCompraUnitario = lote.PrecioCompraUnitario,
                                       ValorTotalCompra = lote.CantidadComprada * lote.PrecioCompraUnitario,
                                       FechaCompra = lote.FechaCompra,
                                       FechaCaducidad = lote.FechaCaducidad,
                                       Proveedor = prov != null ? prov.RazonSocial : "N/A", // Modified
                                       CreadoPor = user != null ? user.PrimerNombre + " " + user.PrimerApellido : "Usuario no encontrado" // Modified
                                   }).ToListAsync();

            return purchases;
        }
    }
}