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
        private readonly IDbContextFactory<FacturasSRIDbContext> _contextFactory;
        private readonly Client _supabase;
        private readonly ILogger<CobroService> _logger;

        public CobroService(IDbContextFactory<FacturasSRIDbContext> contextFactory, Client supabase, ILogger<CobroService> logger)
        {
            _contextFactory = contextFactory;
            _supabase = supabase;
            _logger = logger;
        }

        public async Task<List<CobroDto>> GetAllCobrosAsync()
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            return await context.Cobros
                .Include(c => c.Factura)
                    .ThenInclude(f => f.Cliente)
                .Include(c => c.UsuarioCreador)
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
            await using var context = await _contextFactory.CreateDbContextAsync();
            return await context.Cobros
                .Where(c => c.FacturaId == facturaId)
                .Include(c => c.Factura)
                    .ThenInclude(f => f.Cliente)
                .Include(c => c.UsuarioCreador)
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
            await using var context = await _contextFactory.CreateDbContextAsync();
            using var transaction = await context.Database.BeginTransactionAsync();
            try
            {
                var cuentaPorCobrar = await context.CuentasPorCobrar
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

                var fileExtension = Path.GetExtension(fileName);
                var newFileName = $"{Guid.NewGuid()}{fileExtension}";
                var bucketPath = $"{cobroDto.UsuarioIdCreador}/{newFileName}";

                await using var memoryStream = new MemoryStream();
                await fileStream.CopyToAsync(memoryStream);
                
                await _supabase.Storage.From("comprobantes-facturas-emitidas").Upload(memoryStream.ToArray(), bucketPath);

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

                context.Cobros.Add(cobro);

                cuentaPorCobrar.SaldoPendiente -= cobroDto.Monto;
                if (cuentaPorCobrar.SaldoPendiente <= 0.009m)
                {
                    cuentaPorCobrar.SaldoPendiente = 0;
                    cuentaPorCobrar.Pagada = true;
                }

                await context.SaveChangesAsync();
                await transaction.CommitAsync();

                var creador = await context.Usuarios.FindAsync(cobro.UsuarioIdCreador);
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

        public async Task<List<FacturasConPagosDto>> GetFacturasConPagosAsync()
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            var facturasConPagos = await (from f in context.Facturas
                                        join c in context.Clientes on f.ClienteId equals c.Id into clienteJoin
                                        from cliente in clienteJoin.DefaultIfEmpty()
                                        join cpc in context.CuentasPorCobrar on f.Id equals cpc.FacturaId into cpcJoin
                                        from cuentaPorCobrar in cpcJoin.DefaultIfEmpty()
                                        where f.Cobros.Any()
                                        select new FacturasConPagosDto
                                        {
                                            FacturaId = f.Id,
                                            NumeroFactura = f.NumeroFactura,
                                            ClienteNombre = cliente != null ? cliente.RazonSocial : "N/A",
                                            TotalFactura = f.Total,
                                            SaldoPendiente = cuentaPorCobrar != null ? cuentaPorCobrar.SaldoPendiente : 0,
                                            TotalPagado = f.Cobros.Sum(c => c.Monto),
                                            FormaDePago = f.FormaDePago,
                                            EstadoFactura = f.Estado
                                        })
                                        .ToListAsync();

            return facturasConPagos;
        }
    }
}