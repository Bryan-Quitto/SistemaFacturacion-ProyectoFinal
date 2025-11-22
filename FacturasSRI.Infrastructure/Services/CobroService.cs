using FacturasSRI.Application.Dtos;
using FacturasSRI.Application.Interfaces;
using FacturasSRI.Domain.Entities;
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
    public class CobroService : ICobroService
    {
        private readonly FacturasSRIDbContext _context;
        private readonly Client _supabase;
        private readonly ILogger<CobroService> _logger;

        public CobroService(FacturasSRIDbContext context, Client supabase, ILogger<CobroService> logger)
        {
            _context = context;
            _supabase = supabase;
            _logger = logger;
        }

        public async Task<List<CobroDto>> GetAllCobrosAsync()
        {
            return await _context.Cobros
                .Include(c => c.Factura)
                    .ThenInclude(f => f.Cliente)
                .Include(c => c.UsuarioCreador) // Use Include for the creator
                .OrderByDescending(c => c.FechaCobro)
                .Select(c => new CobroDto
                {
                    Id = c.Id,
                    FacturaId = c.FacturaId,
                    NumeroFactura = c.Factura.NumeroFactura,
                    ClienteNombre = c.Factura.Cliente.RazonSocial,
                    FechaCobro = c.FechaCobro,
                    Monto = c.Monto,
                    MetodoDePago = c.MetodoDePago,
                    Referencia = c.Referencia,
                    ComprobantePagoPath = c.ComprobantePagoPath,
                    CreadoPor = c.UsuarioCreador != null ? $"{c.UsuarioCreador.PrimerNombre} {c.UsuarioCreador.PrimerApellido}" : "N/A"
                })
                .ToListAsync();
        }

        public async Task<List<CobroDto>> GetCobrosByFacturaIdAsync(Guid facturaId)
        {
            return await _context.Cobros
                .Where(c => c.FacturaId == facturaId)
                .Include(c => c.Factura)
                    .ThenInclude(f => f.Cliente)
                .Include(c => c.UsuarioCreador) // Use Include for the creator
                .OrderByDescending(c => c.FechaCobro)
                .Select(c => new CobroDto
                {
                    Id = c.Id,
                    FacturaId = c.FacturaId,
                    NumeroFactura = c.Factura.NumeroFactura,
                    ClienteNombre = c.Factura.Cliente.RazonSocial,
                    FechaCobro = c.FechaCobro,
                    Monto = c.Monto,
                    MetodoDePago = c.MetodoDePago,
                    Referencia = c.Referencia,
                    ComprobantePagoPath = c.ComprobantePagoPath,
                    CreadoPor = c.UsuarioCreador != null ? $"{c.UsuarioCreador.PrimerNombre} {c.UsuarioCreador.PrimerApellido}" : "N/A"
                })
                .ToListAsync();
        }

        public async Task<CobroDto> RegistrarCobroAsync(RegistrarCobroDto cobroDto, Stream fileStream, string fileName)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var cuentaPorCobrar = await _context.CuentasPorCobrar
                    .Include(cpc => cpc.Factura)
                    .ThenInclude(f => f.Cliente)
                    .FirstOrDefaultAsync(cpc => cpc.FacturaId == cobroDto.FacturaId);

                if (cuentaPorCobrar == null)
                {
                    throw new InvalidOperationException("No se encontró una cuenta por cobrar para la factura especificada.");
                }

                if (cuentaPorCobrar.Pagada)
                {
                    throw new InvalidOperationException("La factura ya se encuentra totalmente pagada.");
                }

                if (cobroDto.Monto <= 0)
                {
                    throw new ArgumentException("El monto del cobro debe ser positivo.", nameof(cobroDto.Monto));
                }
                
                if (cobroDto.Monto > cuentaPorCobrar.SaldoPendiente)
                {
                    throw new ArgumentException($"El monto del cobro (${cobroDto.Monto}) no puede ser mayor al saldo pendiente (${cuentaPorCobrar.SaldoPendiente}).", nameof(cobroDto.Monto));
                }

                // 1. Upload file to Supabase
                var fileExtension = Path.GetExtension(fileName);
                var newFileName = $"{Guid.NewGuid()}{fileExtension}";
                var bucketPath = $"{cobroDto.UsuarioIdCreador}/{newFileName}";

                await using var memoryStream = new MemoryStream();
                await fileStream.CopyToAsync(memoryStream);
                
                await _supabase.Storage.From("comprobantes-facturas-emitidas").Upload(memoryStream.ToArray(), bucketPath);

                // 2. Create Cobro entity
                var cobro = new Cobro
                {
                    Id = Guid.NewGuid(),
                    FacturaId = cobroDto.FacturaId,
                    FechaCobro = cobroDto.FechaCobro,
                    Monto = cobroDto.Monto,
                    MetodoDePago = cobroDto.MetodoDePago,
                    Referencia = cobroDto.Referencia,
                    ComprobantePagoPath = bucketPath,
                    UsuarioIdCreador = cobroDto.UsuarioIdCreador,
                    FechaCreacion = DateTime.UtcNow
                };

                _context.Cobros.Add(cobro);

                // 3. Update CuentaPorCobrar
                cuentaPorCobrar.SaldoPendiente -= cobroDto.Monto;
                if (cuentaPorCobrar.SaldoPendiente <= 0.009m) // Use a small tolerance for floating point issues
                {
                    cuentaPorCobrar.SaldoPendiente = 0;
                    cuentaPorCobrar.Pagada = true;
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // 4. Return DTO
                var creador = await _context.Usuarios.FindAsync(cobro.UsuarioIdCreador);
                return new CobroDto
                {
                    Id = cobro.Id,
                    FacturaId = cobro.FacturaId,
                    NumeroFactura = cuentaPorCobrar.Factura.NumeroFactura,
                    ClienteNombre = cuentaPorCobrar.Factura.Cliente.RazonSocial,
                    FechaCobro = cobro.FechaCobro,
                    Monto = cobro.Monto,
                    MetodoDePago = cobro.MetodoDePago,
                    Referencia = cobro.Referencia,
                    ComprobantePagoPath = cobro.ComprobantePagoPath,
                    CreadoPor = creador != null ? $"{creador.PrimerNombre} {creador.PrimerApellido}" : "N/A"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "EXCEPCIÓN al registrar el cobro. Revirtiendo transacción.");
                await transaction.RollbackAsync();
                throw;
            }
        }
    }
}
