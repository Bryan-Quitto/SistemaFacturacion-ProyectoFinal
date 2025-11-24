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
        private readonly FacturasSRIDbContext _context;
        private readonly ILogger<PurchaseService> _logger;
        private readonly Client _supabase;
        private readonly IAjusteInventarioService _ajusteInventarioService;

        public PurchaseService(FacturasSRIDbContext context, ILogger<PurchaseService> logger, Client supabase, IAjusteInventarioService ajusteInventarioService)
        {
            _context = context;
            _logger = logger;
            _supabase = supabase;
            _ajusteInventarioService = ajusteInventarioService;
        }
        
        public async Task MarcarComprasVencidasAsync()
        {
            var ahoraUtc = DateTime.UtcNow;
            var comprasPendientes = await _context.CuentasPorPagar
                .Where(c => c.Estado == EstadoCompra.Pendiente && c.FechaVencimiento.HasValue && c.FechaVencimiento.Value < ahoraUtc)
                .ToListAsync();

            if (comprasPendientes.Any())
            {
                foreach (var compra in comprasPendientes)
                {
                    compra.Estado = EstadoCompra.Vencida;
                }
                await _context.SaveChangesAsync();
                _logger.LogInformation($"Se marcaron {comprasPendientes.Count} compras como vencidas.");
            }
        }

        public async Task<bool> CreatePurchaseAsync(PurchaseDto purchaseDto)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var producto = await _context.Productos.FindAsync(purchaseDto.ProductoId);
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

                var cuenta = new CuentaPorPagar
                {
                    Id = Guid.NewGuid(),
                    ProductoId = purchaseDto.ProductoId,
                    NombreProveedor = purchaseDto.NombreProveedor,
                    FacturaCompraPath = purchaseDto.FacturaCompraPath,
                    FechaEmision = DateTime.UtcNow,
                    MontoTotal = purchaseDto.MontoTotal,
                    Cantidad = purchaseDto.Cantidad,
                    Estado = purchaseDto.FormaDePago == FormaDePago.Credito ? EstadoCompra.Pendiente : EstadoCompra.Pagada,
                    FormaDePago = purchaseDto.FormaDePago,
                    FechaVencimiento = purchaseDto.FormaDePago == FormaDePago.Credito ? purchaseDto.FechaVencimiento!.Value.ToUniversalTime() : null,
                    FechaPago = purchaseDto.FormaDePago == FormaDePago.Contado ? (DateTime?)DateTime.UtcNow : null,
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
                        PrecioCompraUnitario = (purchaseDto.Cantidad > 0) ? (purchaseDto.MontoTotal / purchaseDto.Cantidad) : 0,
                        FechaCompra = DateTime.UtcNow,
                        FechaCaducidad = purchaseDto.FechaCaducidad?.ToUniversalTime(),
                        UsuarioIdCreador = purchaseDto.UsuarioIdCreador,
                        FechaCreacion = DateTime.UtcNow
                    };
                    _context.Lotes.Add(lote);
                    cuenta.LoteId = lote.Id;
                }
                else
                {
                    producto.StockTotal += purchaseDto.Cantidad;
                }

                _context.CuentasPorPagar.Add(cuenta);
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
        
        // MÉTODO ACTUALIZADO: Recibe el archivo, lo sube y luego ajusta el inventario
        public async Task AnularCompraAsync(Guid compraId, Guid usuarioId, Stream notaCreditoStream, string notaCreditoFileName)
{
    var compra = await _context.CuentasPorPagar
        .Include(c => c.Lote)
        .Include(c => c.Producto) // Necesitamos datos del producto
        .FirstOrDefaultAsync(c => c.Id == compraId);

    if (compra == null) throw new InvalidOperationException("La compra no existe.");
    if (compra.Estado != EstadoCompra.Vencida) throw new InvalidOperationException("Solo se pueden anular compras vencidas.");
    if (compra.Producto == null) throw new InvalidOperationException("El producto asociado es inválido.");

    // --- LÓGICA DE VALIDACIÓN DE STOCK ---

    // CASO 1: Producto CON LOTES
    if (compra.Producto.ManejaLotes)
    {
        // Validamos si el lote específico tiene todo el stock intacto
        if (compra.Lote != null && compra.Lote.CantidadDisponible < compra.Lote.CantidadComprada)
        {
            throw new InvalidOperationException("PROHIBIDO: No se puede anular. El stock de este lote ya ha sido vendido parcial o totalmente. Debe realizar el pago.");
        }
        // Si no tiene lote asociado (caso raro de corrupción de datos), no dejamos continuar
        if (compra.Lote == null)
        {
             throw new InvalidOperationException("Error de datos: La compra es de un producto con lotes pero no tiene lote asignado.");
        }
    }
    // CASO 2: Producto SIN LOTES
    else
    {
        // Validamos contra el STOCK TOTAL global del producto
        if (compra.Producto.StockTotal < compra.Cantidad)
        {
            throw new InvalidOperationException($"PROHIBIDO: No hay suficiente stock total ({compra.Producto.StockTotal}) para devolver esta compra ({compra.Cantidad}). Parte de la mercadería ya fue vendida.");
        }
    }

    // --- PROCESO DE SUBIDA DE ARCHIVO ---
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

    // --- PROCESO DE AJUSTE DE INVENTARIO ---
    
    // Preparamos el DTO (Funciona tanto para Lotes como para Sin Lotes)
    // Nota: Si es SIN lotes, LoteId va null, y el servicio de ajustes sabe manejarlo descontando del StockTotal.
    var cantidadAjustar = compra.Producto.ManejaLotes ? compra.Lote!.CantidadDisponible : compra.Cantidad;

    var ajusteDto = new AjusteInventarioDto
    {
        ProductoId = compra.ProductoId,
        LoteId = compra.LoteId, // Será null si no maneja lotes
        CantidadAjustada = cantidadAjustar,
        Tipo = TipoAjusteInventario.AnulacionCompra,
        Motivo = $"Anulación de compra vencida ID: {compra.Id} con Nota de Crédito.",
        UsuarioIdAutoriza = usuarioId
    };

    // Ejecutamos el ajuste
    await _ajusteInventarioService.CreateAdjustmentAsync(ajusteDto);

    // --- FINALIZAR ---
    compra.Estado = EstadoCompra.Cancelada;
    await _context.SaveChangesAsync();
}

        public async Task<List<PurchaseListItemDto>> GetPurchasesAsync()
        {
            return await _context.CuentasPorPagar
                .Include(p => p.Producto)
                .OrderByDescending(p => p.FechaCreacion)
                .Select(p => new PurchaseListItemDto
                {
                    Id = p.Id,
                    ProductName = p.Producto.Nombre,
                    NombreProveedor = p.NombreProveedor,
                    Cantidad = p.Cantidad,
                    MontoTotal = p.MontoTotal,
                    Estado = p.Estado,
                    FechaEmision = p.FechaEmision,
                    FechaVencimiento = p.FechaVencimiento,
                    FechaPago = p.FechaPago,
                    FacturaCompraPath = p.FacturaCompraPath,
                    ComprobantePagoPath = p.ComprobantePagoPath,
                    NotaCreditoPath = p.NotaCreditoPath, // Agregado al mapeo
                    FormaDePago = p.FormaDePago
                })
                .ToListAsync();
        }
        
        public async Task<PurchaseListItemDto?> GetPurchaseByIdAsync(Guid id)
        {
            return await _context.CuentasPorPagar
                .Where(p => p.Id == id)
                .Select(p => new PurchaseListItemDto
                {
                    Id = p.Id,
                    ProductName = p.Producto.Nombre,
                    NombreProveedor = p.NombreProveedor,
                    Cantidad = p.Cantidad,
                    MontoTotal = p.MontoTotal,
                    Estado = p.Estado,
                    FechaEmision = p.FechaEmision,
                    FechaVencimiento = p.FechaVencimiento,
                    FechaPago = p.FechaPago,
                    FacturaCompraPath = p.FacturaCompraPath,
                    ComprobantePagoPath = p.ComprobantePagoPath,
                    NotaCreditoPath = p.NotaCreditoPath, // Agregado al mapeo
                    FormaDePago = p.FormaDePago
                })
                .FirstOrDefaultAsync();
        }

        public async Task<bool> RegisterPaymentAsync(RegisterPaymentDto paymentDto, Stream fileStream, string fileName)
        {
            try
            {
                var purchase = await _context.CuentasPorPagar.FindAsync(paymentDto.PurchaseId);
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

                await _context.SaveChangesAsync();
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