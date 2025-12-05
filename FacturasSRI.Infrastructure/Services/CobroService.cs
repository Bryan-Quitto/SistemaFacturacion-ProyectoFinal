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
            ITimeZoneHelper timeZoneHelper) // <--- 2. INYECTAR
        {
            _contextFactory = contextFactory;
            _supabase = supabase;
            _logger = logger;
            _emailService = emailService; // <--- 3. ASIGNAR
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
                    CreadoPor = c.UsuarioCreador != null ? $"{c.UsuarioCreador.PrimerNombre} {c.UsuarioCreador.PrimerApellido}" : "N/A"
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
                    CreadoPor = c.UsuarioCreador != null ? $"{c.UsuarioCreador.PrimerNombre} {c.UsuarioCreador.PrimerApellido}" : "N/A"
                })
                .ToListAsync();
        }

        // EL MÉTODO IMPORTANTE A MODIFICAR
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
                
                // Pequeña tolerancia para errores de redondeo decimal
                if (cobroDto.Monto > cuentaPorCobrar.SaldoPendiente + 0.01m)
                {
                     throw new ArgumentException($"El monto del cobro (${cobroDto.Monto}) no puede ser mayor al saldo pendiente (${cuentaPorCobrar.SaldoPendiente}).", nameof(cobroDto.Monto));
                }

                string? bucketPath = null;
                if (fileStream != null && !string.IsNullOrEmpty(fileName))
                {
                    var fileExtension = Path.GetExtension(fileName);
                    var newFileName = $"{Guid.NewGuid()}{fileExtension}";
                    bucketPath = $"{cobroDto.UsuarioIdCreador}/{newFileName}";

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

                // 4. LOGICA NUEVA: ENVÍO DE CORREO (Fire and Forget)
                // Ejecutamos en segundo plano para no hacer esperar al usuario en la UI
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
                        // Solo logueamos error de correo, no revertimos la transacción del cobro
                        _logger.LogError(ex, "Error enviando correo de confirmación para cobro {CobroId}", cobro.Id);
                    }
                });

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
        // ... (Resto de métodos GetAllCobros, etc... quedan igual)
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
                                    .Include(f => f.Cobros) // Explicitly include Cobros here
                                    .Include(f => f.Cliente) // Explicitly include Cliente for ClienteNombre
                        join cpc in context.CuentasPorCobrar on f.Id equals cpc.FacturaId into cpcJoin
                        from cuentaPorCobrar in cpcJoin.DefaultIfEmpty() // Left join with CuentasPorCobrar
                        where f.ClienteId == clienteId // Filter by client ID
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
                    ClienteNombre = x.Factura.Cliente != null ? x.Factura.Cliente.RazonSocial : "N/A", // Cliente is now eagerly loaded
                    TotalFactura = x.Factura.Total,
                    TotalPagado = x.Factura.Cobros.Sum(c => c.Monto), // Cobros are now eagerly loaded
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

            // 1. Filtros de Fecha
            if (startDate.HasValue)
                query = query.Where(c => c.FechaCobro >= startDate.Value);

            if (endDate.HasValue)
            {
                var endOfDay = endDate.Value.Date.AddDays(1);
                query = query.Where(c => c.FechaCobro < endOfDay);
            }

            // 2. Filtro de Método de Pago (NUEVO)
            if (!string.IsNullOrEmpty(paymentMethod) && paymentMethod != "All")
            {
                // Usamos Contains para que "Tarjeta" encuentre "Tarjeta de Crédito/Débito"
                query = query.Where(c => c.MetodoDePago.Contains(paymentMethod));
            }

            // 3. Filtro de Texto Inteligente
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
                    CreadoPor = c.UsuarioCreador != null ? $"{c.UsuarioCreador.PrimerNombre} {c.UsuarioCreador.PrimerApellido}" : "N/A"
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

            // 1. Filtros de Fecha
            if (startDate.HasValue)
                query = query.Where(c => c.FechaCobro >= startDate.Value);

            if (endDate.HasValue)
            {
                var endOfDay = endDate.Value.Date.AddDays(1);
                query = query.Where(c => c.FechaCobro < endOfDay);
            }

            // 2. Filtro de Método de Pago
            if (!string.IsNullOrEmpty(paymentMethod) && paymentMethod != "All")
            {
                query = query.Where(c => c.MetodoDePago.Contains(paymentMethod));
            }

            // 3. Filtro de Texto (Referencia)
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
                    // For customer view, CreadoPor is not needed, so it can be "N/A" or omitted
                    CreadoPor = "N/A" 
                });

            return await PaginatedList<CobroDto>.CreateAsync(finalQuery, pageNumber, pageSize);
        }
    }
}