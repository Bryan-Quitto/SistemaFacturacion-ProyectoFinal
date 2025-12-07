using FacturasSRI.Application.Dtos;
using FacturasSRI.Application.Interfaces;
using FacturasSRI.Domain.Entities;
using FacturasSRI.Domain.Enums;
using FacturasSRI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Supabase;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace FacturasSRI.Infrastructure.Services
{
    public class PurchaseService : IPurchaseService
    {
        private readonly IDbContextFactory<FacturasSRIDbContext> _contextFactory;
        private readonly ILogger<PurchaseService> _logger;
        private readonly Client _supabase;
        private readonly IAjusteInventarioService _ajusteInventarioService;
        private readonly DataCacheService _dataCacheService;

        public PurchaseService(IDbContextFactory<FacturasSRIDbContext> contextFactory, ILogger<PurchaseService> logger, Client supabase, IAjusteInventarioService ajusteInventarioService, DataCacheService dataCacheService)
        {
            _contextFactory = contextFactory;
            _logger = logger;
            _supabase = supabase;
            _ajusteInventarioService = ajusteInventarioService;
            _dataCacheService = dataCacheService;
        }
        
        public async Task MarcarComprasVencidasAsync()
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            var ahoraUtc = DateTime.UtcNow;
            var comprasPendientes = await context.CuentasPorPagar
                .Where(c => c.Estado == EstadoCompra.Pendiente && c.FechaVencimiento.HasValue && c.FechaVencimiento.Value < ahoraUtc)
                .ToListAsync();

            if (comprasPendientes.Any())
            {
                foreach (var compra in comprasPendientes)
                {
                    compra.Estado = EstadoCompra.Vencida;
                }
                await context.SaveChangesAsync();
                _logger.LogInformation($"Se marcaron {comprasPendientes.Count} compras como vencidas.");
            }
        }

        public async Task<bool> CreatePurchaseAsync(PurchaseDto purchaseDto)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            using var transaction = await context.Database.BeginTransactionAsync();
            try
            {
                var producto = await context.Productos.Include(p => p.Lotes).FirstOrDefaultAsync(p => p.Id == purchaseDto.ProductoId);
                if (producto == null) throw new InvalidOperationException("El producto no existe.");
                if (!producto.ManejaInventario) throw new InvalidOperationException("No se puede registrar una compra para un producto que no maneja inventario.");

                if (purchaseDto.FormaDePago == FormaDePago.Credito && !purchaseDto.FechaVencimiento.HasValue)
                {
                    throw new InvalidOperationException("Para compras a crédito, la fecha de vencimiento es obligatoria.");
                }
                if (purchaseDto.FechaVencimiento.HasValue && purchaseDto.FechaVencimiento.Value.ToUniversalTime() < DateTime.UtcNow)
                {
                    throw new InvalidOperationException("La fecha de vencimiento debe ser una fecha futura.");
                }

                var tax = await context.Impuestos.FindAsync(purchaseDto.ImpuestoId) ?? throw new InvalidOperationException("Impuesto no válido.");
                var taxRate = tax.Porcentaje / 100m;
                var montoTotalConIva = (purchaseDto.Cantidad * purchaseDto.PrecioCompraUnitario) * (1 + taxRate);
                
                var currentStock = producto.StockTotal;
                var currentAverageCost = producto.PrecioCompraPromedioPonderado;
                var newQuantity = purchaseDto.Cantidad;
                var newPurchasePrice = purchaseDto.PrecioCompraUnitario;

                _logger.LogInformation("--- Iniciando Cálculo de PMP para Producto ID: {ProductId} ---", producto.Id);
                _logger.LogInformation("Stock Actual: {CurrentStock}, Costo Promedio Actual: {CurrentAvgCost}", currentStock, currentAverageCost);
                _logger.LogInformation("Cantidad Nueva: {NewQty}, Precio de Compra Nuevo: {NewPrice}", newQuantity, newPurchasePrice);

                var totalStock = currentStock + newQuantity;
                if (totalStock > 0)
                {
                    var newAverageCost = ((currentStock * currentAverageCost) + (newQuantity * newPurchasePrice)) / totalStock;
                    producto.PrecioCompraPromedioPonderado = newAverageCost;
                }
                else
                {
                    producto.PrecioCompraPromedioPonderado = newPurchasePrice;
                }

                producto.StockTotal = totalStock;

                var secuencial = await context.Set<Secuencial>().FirstOrDefaultAsync();
                if (secuencial == null)
                {
                    // Si no existe tabla secuencial (raro), créala o lanza error.
                    // Asumiremos que existe por tus otras tablas.
                    secuencial = new Secuencial { UltimoSecuencialCompra = 0 };
                    context.Set<Secuencial>().Add(secuencial);
                }

                secuencial.UltimoSecuencialCompra++;
                var nuevoNumeroInterno = secuencial.UltimoSecuencialCompra;

                var cuenta = new CuentaPorPagar
                {
                    Id = Guid.NewGuid(),
                    ProductoId = purchaseDto.ProductoId,
                    NombreProveedor = purchaseDto.NombreProveedor,
                    FacturaCompraPath = purchaseDto.FacturaCompraPath,
                    FechaEmision = DateTime.UtcNow,
                    MontoTotal = montoTotalConIva,
                    Cantidad = purchaseDto.Cantidad,
                    Estado = purchaseDto.FormaDePago == FormaDePago.Credito ? EstadoCompra.Pendiente : EstadoCompra.Pagada,
                    FormaDePago = purchaseDto.FormaDePago,
                    FechaVencimiento = purchaseDto.FormaDePago == FormaDePago.Credito ? purchaseDto.FechaVencimiento!.Value.ToUniversalTime() : null,
                    FechaPago = purchaseDto.FormaDePago == FormaDePago.Contado ? (DateTime?)DateTime.UtcNow : null,
                    NumeroCompraInterno = nuevoNumeroInterno,
                    NumeroFacturaProveedor = purchaseDto.NumeroFacturaProveedor,
                    UsuarioIdCreador = purchaseDto.UsuarioIdCreador,
                    FechaCreacion = DateTime.UtcNow
                };

                if (producto.ManejaLotes)
                {
                    var lote = new Lote
                    {
                        Id = Guid.NewGuid(),
                        ProductoId = purchaseDto.ProductoId,
                        CantidadComprada = purchaseDto.Cantidad,
                        CantidadDisponible = purchaseDto.Cantidad,
                        PrecioCompraUnitario = purchaseDto.PrecioCompraUnitario,
                        FechaCompra = DateTime.UtcNow,
                        FechaCaducidad = purchaseDto.FechaCaducidad?.ToUniversalTime(),
                        UsuarioIdCreador = purchaseDto.UsuarioIdCreador,
                        FechaCreacion = DateTime.UtcNow
                    };
                    context.Lotes.Add(lote);
                    cuenta.LoteId = lote.Id;
                }

                context.CuentasPorPagar.Add(cuenta);
                await context.SaveChangesAsync();
                await transaction.CommitAsync();
                _dataCacheService.ClearProductsCache(); // Invalidate product cache
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "EXCEPCIÓN al crear la compra. Revirtiendo transacción.");
                await transaction.RollbackAsync();
                return false;
            }
        }
        
        public async Task AnularCompraAsync(Guid compraId, Guid usuarioId, Stream notaCreditoStream, string notaCreditoFileName)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            var compra = await context.CuentasPorPagar
                .Include(c => c.Lote)
                .Include(c => c.Producto) 
                .FirstOrDefaultAsync(c => c.Id == compraId);

            if (compra == null) throw new InvalidOperationException("La compra no existe.");
            if (compra.Estado != EstadoCompra.Pendiente && compra.Estado != EstadoCompra.Vencida) 
            {
                throw new InvalidOperationException("Solo se pueden anular compras que estén Pendientes o Vencidas (No Pagadas).");
            }
            if (compra.Producto == null) throw new InvalidOperationException("El producto asociado es inválido.");

            if (compra.Producto.ManejaLotes)
            {
                if (compra.Lote != null && compra.Lote.CantidadDisponible < compra.Lote.CantidadComprada)
                {
                    throw new InvalidOperationException("PROHIBIDO: No se puede anular. El stock de este lote ya ha sido vendido parcial o totalmente. Debe realizar el pago.");
                }
                if (compra.Lote == null)
                {
                     throw new InvalidOperationException("Error de datos: La compra es de un producto con lotes pero no tiene lote asignado.");
                }
            }
            else
            {
                if (compra.Producto.StockTotal < compra.Cantidad)
                {
                    throw new InvalidOperationException($"PROHIBIDO: No hay suficiente stock total ({compra.Producto.StockTotal}) para devolver esta compra ({compra.Cantidad}). Parte de la mercadería ya fue vendida.");
                }
            }

            try 
            {
                var fileExtension = Path.GetExtension(notaCreditoFileName);
                var newFileName = $"NC_{Guid.NewGuid()}{fileExtension}";
                var bucketPath = $"{usuarioId}/{newFileName}";

                await using var memoryStream = new MemoryStream();
                await notaCreditoStream.CopyToAsync(memoryStream);
                
                await _supabase.Storage.From("comprobantes-compra").Upload(memoryStream.ToArray(), bucketPath);
                
                compra.NotaCreditoPath = bucketPath;
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, "Error al subir la nota de crédito a Supabase");
                throw new Exception("No se pudo subir el archivo de la Nota de Crédito. La anulación no se procesó.");
            }

            var cantidadAjustar = compra.Producto.ManejaLotes ? compra.Lote!.CantidadDisponible : compra.Cantidad;
            string identificadorCompra = !string.IsNullOrEmpty(compra.NumeroFacturaProveedor) 
                ? $"Fac. Prov. {compra.NumeroFacturaProveedor}" 
                : $"Compra #{compra.NumeroCompraInterno}";

            var ajusteDto = new AjusteInventarioDto
            {
                ProductoId = compra.ProductoId,
                LoteId = compra.LoteId, 
                CantidadAjustada = cantidadAjustar,
                Tipo = TipoAjusteInventario.AnulacionCompra,
                Motivo = $"Anulación de {identificadorCompra} (Interno #{compra.NumeroCompraInterno}) con Nota de Crédito.",
                UsuarioIdAutoriza = usuarioId
            };

            await _ajusteInventarioService.CreateAdjustmentAsync(ajusteDto);

            compra.Estado = EstadoCompra.Cancelada;
            await context.SaveChangesAsync();
        }

        public async Task<PaginatedList<PurchaseListItemDto>> GetPurchasesAsync(int pageNumber, int pageSize, string? searchTerm, EstadoCompra? status, FormaDePago? formaDePago, string? supplierName)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            var query = context.CuentasPorPagar
                .Include(p => p.Producto)
                .Include(p => p.Lote)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                query = query.Where(p => 
                    (p.Producto.Nombre != null && p.Producto.Nombre.Contains(searchTerm)) ||
                    (p.NombreProveedor != null && p.NombreProveedor.Contains(searchTerm)));
            }

            if (status.HasValue)
            {
                query = query.Where(p => p.Estado == status.Value);
            }
            
            if (formaDePago.HasValue)
            {
                query = query.Where(p => p.FormaDePago == formaDePago.Value);
            }

            if (!string.IsNullOrWhiteSpace(supplierName))
            {
                query = query.Where(p => p.NombreProveedor == supplierName);
            }

            var finalQuery = query
                .OrderByDescending(p => p.FechaCreacion)
                .Select(p => new PurchaseListItemDto
                {
                    Id = p.Id,
                    ProductName = p.Producto.Nombre,
                    NombreProveedor = p.NombreProveedor,
                    Cantidad = p.Cantidad,
                    MontoTotal = p.MontoTotal,
                    PrecioCompraUnitario = p.Lote != null 
                                            ? p.Lote.PrecioCompraUnitario 
                                            : ((p.Cantidad > 0) ? (p.MontoTotal / p.Cantidad) : 0),
                    Estado = p.Estado,
                    FechaEmision = p.FechaEmision,
                    FechaVencimiento = p.FechaVencimiento,
                    FechaPago = p.FechaPago,
                    FacturaCompraPath = p.FacturaCompraPath,
                    ComprobantePagoPath = p.ComprobantePagoPath,
                    NotaCreditoPath = p.NotaCreditoPath,
                    FormaDePago = p.FormaDePago
                });

            return await PaginatedList<PurchaseListItemDto>.CreateAsync(finalQuery, pageNumber, pageSize);
        }

        public async Task<List<string>> GetAllProveedoresAsync()
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            return await context.CuentasPorPagar
                .Select(p => p.NombreProveedor)
                .Where(p => !string.IsNullOrEmpty(p))
                .Distinct()
                .OrderBy(p => p)
                .ToListAsync();
        }
        
        public async Task<PurchaseListItemDto?> GetPurchaseByIdAsync(Guid id)
{
    await using var context = await _contextFactory.CreateDbContextAsync();
    return await context.CuentasPorPagar
        .Include(p => p.Lote)
        .Where(p => p.Id == id)
        .Select(p => new PurchaseListItemDto
        {
            Id = p.Id,
            ProductName = p.Producto.Nombre,
            NombreProveedor = p.NombreProveedor,
            Cantidad = p.Cantidad,
            MontoTotal = p.MontoTotal,

            PrecioCompraUnitario = p.Lote != null 
                                    ? p.Lote.PrecioCompraUnitario 
                                    : ((p.Cantidad > 0) ? (p.MontoTotal / p.Cantidad) : 0),

            Estado = p.Estado,
            FechaEmision = p.FechaEmision,
            FechaVencimiento = p.FechaVencimiento,
            FechaPago = p.FechaPago,
            FacturaCompraPath = p.FacturaCompraPath,
            ComprobantePagoPath = p.ComprobantePagoPath,
            NotaCreditoPath = p.NotaCreditoPath,
            FormaDePago = p.FormaDePago
        })
        .FirstOrDefaultAsync();
}

        public async Task<bool> RegisterPaymentAsync(RegisterPaymentDto paymentDto, Stream fileStream, string fileName)
        {
            try
            {
                await using var context = await _contextFactory.CreateDbContextAsync();
                var purchase = await context.CuentasPorPagar.FindAsync(paymentDto.PurchaseId);
                if (purchase == null) throw new InvalidOperationException("La compra no existe.");

                if (purchase.Estado != EstadoCompra.Pendiente && purchase.Estado != EstadoCompra.Vencida)
                {
                    throw new InvalidOperationException("Solo se pueden registrar pagos para compras pendientes o vencidas.");
                }

                var fileExtension = Path.GetExtension(fileName);
                var newFileName = $"{Guid.NewGuid()}{fileExtension}";
                var bucketPath = $"{paymentDto.UsuarioId}/{newFileName}";

                await using var memoryStream = new MemoryStream();
                await fileStream.CopyToAsync(memoryStream);
                
                await _supabase.Storage.From("comprobantes-compra").Upload(memoryStream.ToArray(), bucketPath);

                purchase.Estado = EstadoCompra.Pagada;
                purchase.FechaPago = DateTime.UtcNow;
                purchase.ComprobantePagoPath = bucketPath;

                await context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "EXCEPCIÓN al registrar el pago.");
                return false;
            }
        }
    }
}
