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
    public class CobroService : ICobroService
    {
        private readonly IDbContextFactory<FacturasSRIDbContext> _contextFactory;
        private readonly Client _supabase;
        private readonly ILogger<CobroService> _logger;
        private readonly IEmailService _emailService;
        private readonly ITimeZoneHelper _timeZoneHelper;

        public CobroService(
            IDbContextFactory<FacturasSRIDbContext> contextFactory, 
            Client supabase, 
            ILogger<CobroService> logger,
            IEmailService emailService,
            ITimeZoneHelper timeZoneHelper)
        {
            _contextFactory = contextFactory;
            _supabase = supabase;
            _logger = logger;
            _emailService = emailService;
            _timeZoneHelper = timeZoneHelper;
        }

        public async Task<PaginatedList<CobroDto>> GetAllCobrosAsync(int pageNumber, int pageSize, string? searchTerm)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            var query = context.Cobros
                .Include(c => c.Factura)
                    .ThenInclude(f => f.Cliente)
                .Include(c => c.UsuarioCreador)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                query = query.Where(c => 
                    (c.Factura.NumeroFactura != null && c.Factura.NumeroFactura.Contains(searchTerm)) ||
                    (c.Factura.Cliente.RazonSocial != null && c.Factura.Cliente.RazonSocial.Contains(searchTerm)));
            }

            var finalQuery = query
                .OrderByDescending(c => c.FechaCobro)
                .Select(c => new CobroDto
                {
                    Id = c.Id,
                    FacturaId = c.FacturaId,
                    NumeroFactura = c.Factura.NumeroFactura,
                    ClienteNombre = c.Factura.Cliente != null ? c.Factura.Cliente.RazonSocial : "N/A",
                    FechaCobro = c.FechaCobro,
                    Monto = c.Monto,
                    MetodoDePago = c.MetodoDePago,
                    Referencia = c.Referencia,
                    ComprobantePagoPath = c.ComprobantePagoPath,
                    // CAMBIO VISUAL: Si es usuario interno, muestra su nombre. Si es NULL, muestra la Razón Social del cliente.
                    CreadoPor = c.UsuarioCreador != null 
                        ? $"{c.UsuarioCreador.PrimerNombre} {c.UsuarioCreador.PrimerApellido}" 
                        : (c.Factura.Cliente != null ? c.Factura.Cliente.RazonSocial : "Cliente")
                });
            
            return await PaginatedList<CobroDto>.CreateAsync(finalQuery, pageNumber, pageSize);
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
                    ClienteNombre = c.Factura.Cliente != null ? c.Factura.Cliente.RazonSocial : "N/A",
                    FechaCobro = c.FechaCobro,
                    Monto = c.Monto,
                    MetodoDePago = c.MetodoDePago,
                    Referencia = c.Referencia,
                    ComprobantePagoPath = c.ComprobantePagoPath,
                    // CAMBIO VISUAL: Muestra la Razón Social si no hay usuario creador (pago autoservicio)
                    CreadoPor = c.UsuarioCreador != null 
                        ? $"{c.UsuarioCreador.PrimerNombre} {c.UsuarioCreador.PrimerApellido}" 
                        : (c.Factura.Cliente != null ? c.Factura.Cliente.RazonSocial : "Cliente")
                })
                .ToListAsync();
        }

        public async Task<CobroDto> RegistrarCobroAsync(RegistrarCobroDto cobroDto, Stream? fileStream, string? fileName)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            using var transaction = await context.Database.BeginTransactionAsync();
            try
            {
                var cuentaPorCobrar = await context.CuentasPorCobrar
                    .Include(cpc => cpc.Factura)
                    .ThenInclude(f => f.Cliente)
                    .FirstOrDefaultAsync(cpc => cpc.FacturaId == cobroDto.FacturaId);

                if (cuentaPorCobrar == null) throw new InvalidOperationException("No se encontró una cuenta por cobrar para la factura especificada.");
                if (cuentaPorCobrar.Pagada) throw new InvalidOperationException("La factura ya se encuentra totalmente pagada.");
                if (cobroDto.Monto <= 0) throw new ArgumentException("El monto del cobro debe ser positivo.", nameof(cobroDto.Monto));
                
                if (cobroDto.Monto > cuentaPorCobrar.SaldoPendiente + 0.01m)
                {
                      throw new ArgumentException($"El monto del cobro (${cobroDto.Monto}) no puede ser mayor al saldo pendiente (${cuentaPorCobrar.SaldoPendiente}).", nameof(cobroDto.Monto));
                }

                string? bucketPath = null;
                if (fileStream != null && !string.IsNullOrEmpty(fileName))
                {
                    var fileExtension = Path.GetExtension(fileName);
                    var newFileName = $"{Guid.NewGuid()}{fileExtension}";
                    
                    var folderName = cobroDto.UsuarioIdCreador.HasValue 
                                     ? cobroDto.UsuarioIdCreador.ToString() 
                                     : "portal-clientes";
                                     
                    bucketPath = $"{folderName}/{newFileName}";

                    await using var memoryStream = new MemoryStream();
                    await fileStream.CopyToAsync(memoryStream);
                    
                    await _supabase.Storage.From("comprobantes-facturas-emitidas").Upload(memoryStream.ToArray(), bucketPath);
                }

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

                _ = Task.Run(async () => 
                {
                    try
                    {
                        if (!string.IsNullOrEmpty(cuentaPorCobrar.Factura.Cliente.Email))
                        {
                            await _emailService.SendPaymentConfirmationEmailAsync(
                                cuentaPorCobrar.Factura.Cliente.Email,
                                cuentaPorCobrar.Factura.Cliente.RazonSocial,
                                cuentaPorCobrar.Factura.NumeroFactura,
                                cobro.Monto,
                                _timeZoneHelper.ConvertUtcToEcuadorTime(cobro.FechaCobro).ToString("dd/MM/yyyy HH:mm"),
                                cobro.Referencia ?? "Sin referencia"
                            );
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error enviando correo de confirmación para cobro {CobroId}", cobro.Id);
                    }
                });

                Usuario? creador = null;
                if(cobro.UsuarioIdCreador.HasValue)
                {
                    creador = await context.Usuarios.FindAsync(cobro.UsuarioIdCreador);
                }

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
                    // CAMBIO VISUAL EN EL RETORNO TAMBIÉN
                    CreadoPor = creador != null 
                        ? $"{creador.PrimerNombre} {creador.PrimerApellido}" 
                        : cuentaPorCobrar.Factura.Cliente.RazonSocial
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "EXCEPCIÓN al registrar el cobro. Revirtiendo transacción.");
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<PaginatedList<FacturasConPagosDto>> GetFacturasConPagosAsync(int pageNumber, int pageSize, string? searchTerm, FormaDePago? formaDePago, EstadoFactura? estadoFactura)
        {
             await using var context = await _contextFactory.CreateDbContextAsync();
            var query = from f in context.Facturas
                        join c in context.Clientes on f.ClienteId equals c.Id into clienteJoin
                        from cliente in clienteJoin.DefaultIfEmpty()
                        join cpc in context.CuentasPorCobrar on f.Id equals cpc.FacturaId into cpcJoin
                        from cuentaPorCobrar in cpcJoin.DefaultIfEmpty()
                        where f.Cobros.Any()
                        select new
                        {
                            Factura = f,
                            Cliente = cliente,
                            CuentaPorCobrar = cuentaPorCobrar
                        };

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                query = query.Where(x => 
                    x.Factura.NumeroFactura.Contains(searchTerm) ||
                    (x.Cliente != null && x.Cliente.RazonSocial.Contains(searchTerm)));
            }

            if (formaDePago.HasValue)
            {
                query = query.Where(x => x.Factura.FormaDePago == formaDePago.Value);
            }

            if (estadoFactura.HasValue)
            {
                query = query.Where(x => x.Factura.Estado == estadoFactura.Value);
            }

            var finalQuery = query
                .OrderByDescending(x => x.Factura.FechaEmision)
                .Select(x => new FacturasConPagosDto
                {
                    FacturaId = x.Factura.Id,
                    NumeroFactura = x.Factura.NumeroFactura,
                    ClienteNombre = x.Cliente != null ? x.Cliente.RazonSocial : "N/A",
                    TotalFactura = x.Factura.Total,
                    SaldoPendiente = x.CuentaPorCobrar != null ? x.CuentaPorCobrar.SaldoPendiente : 0,
                    TotalPagado = x.Factura.Cobros.Sum(c => c.Monto),
                    FormaDePago = x.Factura.FormaDePago,
                    EstadoFactura = x.Factura.Estado,
                    FechaEmision = x.Factura.FechaEmision
                });

            return await PaginatedList<FacturasConPagosDto>.CreateAsync(finalQuery, pageNumber, pageSize);
        }

        public async Task<PaginatedList<FacturasConPagosDto>> GetFacturasConPagosByClientIdAsync(Guid clienteId, int pageNumber, int pageSize, string? searchTerm, DateTime? startDate, DateTime? endDate)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            var query = from f in context.Facturas
                                    .Include(f => f.Cobros) 
                                    .Include(f => f.Cliente) 
                        join cpc in context.CuentasPorCobrar on f.Id equals cpc.FacturaId into cpcJoin
                        from cuentaPorCobrar in cpcJoin.DefaultIfEmpty() 
                        where f.ClienteId == clienteId 
                        select new
                        {
                            Factura = f,
                            CuentaPorCobrar = cuentaPorCobrar
                        };

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                query = query.Where(x => x.Factura.NumeroFactura.Contains(searchTerm));
            }

            if (startDate.HasValue)
            {
                query = query.Where(x => x.Factura.FechaEmision >= startDate.Value);
            }

            if (endDate.HasValue)
            {
                var endOfDay = endDate.Value.Date.AddDays(1);
                query = query.Where(x => x.Factura.FechaEmision < endOfDay);
            }

            var finalQuery = query
                .OrderByDescending(x => x.Factura.FechaEmision)
                .Select(x => new FacturasConPagosDto
                {
                    FacturaId = x.Factura.Id,
                    NumeroFactura = x.Factura.NumeroFactura,
                    ClienteNombre = x.Factura.Cliente != null ? x.Factura.Cliente.RazonSocial : "N/A", 
                    TotalFactura = x.Factura.Total,
                    // CORRECCIÓN: Se agrega verificación de nulidad para Cobros antes de llamar a Sum()
                    TotalPagado = x.Factura.Cobros != null ? x.Factura.Cobros.Sum(c => c.Monto) : 0, 
                    SaldoPendiente = x.CuentaPorCobrar != null ? x.CuentaPorCobrar.SaldoPendiente : 0,
                    FormaDePago = x.Factura.FormaDePago,
                    EstadoFactura = x.Factura.Estado,
                    FechaEmision = x.Factura.FechaEmision
                });

            return await PaginatedList<FacturasConPagosDto>.CreateAsync(finalQuery, pageNumber, pageSize);
        }

        public async Task<PaginatedList<CobroDto>> GetCobrosByClientIdAsync(Guid clienteId, int pageNumber, int pageSize, string? searchTerm, DateTime? startDate, DateTime? endDate, string? paymentMethod)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            var query = context.Cobros
                .Where(c => c.Factura.ClienteId == clienteId)
                .Include(c => c.Factura)
                .Include(c => c.UsuarioCreador)
                .AsQueryable();

            if (startDate.HasValue)
                query = query.Where(c => c.FechaCobro >= startDate.Value);

            if (endDate.HasValue)
            {
                var endOfDay = endDate.Value.Date.AddDays(1);
                query = query.Where(c => c.FechaCobro < endOfDay);
            }

            if (!string.IsNullOrEmpty(paymentMethod) && paymentMethod != "All")
            {
                query = query.Where(c => c.MetodoDePago.Contains(paymentMethod));
            }

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var term = searchTerm.Trim();
                bool buscaStripe = term.Contains("Pago", StringComparison.OrdinalIgnoreCase) || 
                                   term.Contains("Online", StringComparison.OrdinalIgnoreCase) ||
                                   term.Contains("Stripe", StringComparison.OrdinalIgnoreCase);
                bool buscaNA = term.Equals("N/A", StringComparison.OrdinalIgnoreCase);

                query = query.Where(c => 
                    c.Factura.NumeroFactura.Contains(term) ||
                    (c.Referencia != null && c.Referencia.Contains(term)) ||
                    (buscaStripe && c.Referencia != null && c.Referencia.Contains("Stripe")) ||
                    (buscaNA && (c.Referencia == null || c.Referencia == ""))
                );
            }

            var finalQuery = query
                .OrderByDescending(c => c.FechaCobro)
                .Select(c => new CobroDto
                {
                    Id = c.Id,
                    FacturaId = c.FacturaId,
                    NumeroFactura = c.Factura != null ? c.Factura.NumeroFactura : "N/A",
                    ClienteNombre = c.Factura != null && c.Factura.Cliente != null ? c.Factura.Cliente.RazonSocial : "N/A",
                    FechaCobro = c.FechaCobro,
                    Monto = c.Monto,
                    MetodoDePago = c.MetodoDePago,
                    Referencia = c.Referencia,
                    ComprobantePagoPath = c.ComprobantePagoPath,
                    // CAMBIO VISUAL
                    CreadoPor = c.UsuarioCreador != null 
                        ? $"{c.UsuarioCreador.PrimerNombre} {c.UsuarioCreador.PrimerApellido}" 
                        : (c.Factura.Cliente != null ? c.Factura.Cliente.RazonSocial : "Cliente")
                });

            return await PaginatedList<CobroDto>.CreateAsync(finalQuery, pageNumber, pageSize);
        }

        public async Task<PaginatedList<CobroDto>> GetCobrosByFacturaIdAndClientIdAsync(Guid facturaId, Guid clienteId, int pageNumber, int pageSize, string? searchTerm, DateTime? startDate, DateTime? endDate, string? paymentMethod)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            var query = context.Cobros
                .Where(c => c.FacturaId == facturaId && c.Factura.ClienteId == clienteId)
                .Include(c => c.Factura)
                .Include(c => c.UsuarioCreador)
                .AsQueryable();

            if (startDate.HasValue)
                query = query.Where(c => c.FechaCobro >= startDate.Value);

            if (endDate.HasValue)
            {
                var endOfDay = endDate.Value.Date.AddDays(1);
                query = query.Where(c => c.FechaCobro < endOfDay);
            }

            if (!string.IsNullOrEmpty(paymentMethod) && paymentMethod != "All")
            {
                query = query.Where(c => c.MetodoDePago.Contains(paymentMethod));
            }

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var term = searchTerm.Trim();
                query = query.Where(c => 
                    c.Referencia != null && c.Referencia.Contains(term)
                );
            }

            var finalQuery = query
                .OrderByDescending(c => c.FechaCobro)
                .Select(c => new CobroDto
                {
                    Id = c.Id,
                    FacturaId = c.FacturaId,
                    NumeroFactura = c.Factura != null ? c.Factura.NumeroFactura : "N/A",
                    ClienteNombre = c.Factura != null && c.Factura.Cliente != null ? c.Factura.Cliente.RazonSocial : "N/A",
                    FechaCobro = c.FechaCobro,
                    Monto = c.Monto,
                    MetodoDePago = c.MetodoDePago,
                    Referencia = c.Referencia,
                    ComprobantePagoPath = c.ComprobantePagoPath,
                    // CAMBIO VISUAL (Aquí siempre es el cliente porque es su vista)
                    CreadoPor = "N/A"
                });

            return await PaginatedList<CobroDto>.CreateAsync(finalQuery, pageNumber, pageSize);
        }
    }
}