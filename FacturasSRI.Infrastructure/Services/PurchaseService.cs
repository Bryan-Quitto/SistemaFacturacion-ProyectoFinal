using FacturasSRI.Application.Dtos;
using FacturasSRI.Application.Interfaces;
using FacturasSRI.Domain.Entities;
using FacturasSRI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
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

        public async Task<bool> CreatePurchaseAsync(PurchaseDto purchaseDto, Guid userId)
        {
            _logger.LogInformation("--- Inicio de CreatePurchaseAsync ---");
            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
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
                        UsuarioIdCreador = userId,
                        FechaCreacion = DateTime.UtcNow
                    };
                    _logger.LogInformation("Entidad Lote creada en memoria. ID: {LoteId}", lote.Id);

                    var cuentaPorPagar = new CuentaPorPagar
                    {
                        Id = Guid.NewGuid(),
                        LoteId = lote.Id,
                        Proveedor = purchaseDto.Proveedor,
                        NumeroFactura = purchaseDto.NumeroFactura,
                        FechaEmision = DateTime.UtcNow,
                        FechaVencimiento = DateTime.UtcNow.AddDays(30),
                        MontoTotal = purchaseDto.Cantidad * purchaseDto.PrecioCosto,
                        SaldoPendiente = purchaseDto.Cantidad * purchaseDto.PrecioCosto,
                        Pagada = false,
                        UsuarioIdCreador = userId,
                        FechaCreacion = DateTime.UtcNow
                    };
                    _logger.LogInformation("Entidad CuentaPorPagar creada en memoria. ID: {CxpId}", cuentaPorPagar.Id);
                    
                    _context.Lotes.Add(lote);
                    _context.CuentasPorPagar.Add(cuentaPorPagar);
                    _logger.LogInformation("Entidades añadidas al DbContext. Intentando guardar cambios...");

                    var recordsSaved = await _context.SaveChangesAsync();
                    _logger.LogInformation("SaveChangesAsync() completado. Registros guardados: {RecordsCount}", recordsSaved);
                    
                    _logger.LogInformation("Intentando confirmar transacción...");
                    await transaction.CommitAsync();
                    _logger.LogInformation("Transacción confirmada exitosamente.");

                    return true;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "EXCEPCIÓN al crear la compra. Revirtiendo transacción. DETALLES: {ExceptionDetails}", ex.ToString());
                    await transaction.RollbackAsync();
                    return false;
                }
            }
        }
    }
}