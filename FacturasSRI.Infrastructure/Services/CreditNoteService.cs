using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FacturasSRI.Application.Dtos;
using FacturasSRI.Application.Interfaces;
using FacturasSRI.Core.Models;
using FacturasSRI.Core.Services;
using FacturasSRI.Domain.Entities;
using FacturasSRI.Domain.Enums;
using FacturasSRI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FacturasSRI.Infrastructure.Services
{
    public class CreditNoteService : ICreditNoteService
    {
        private readonly FacturasSRIDbContext _context;
        private readonly ILogger<CreditNoteService> _logger;
        private readonly IConfiguration _configuration;
        private readonly XmlGeneratorService _xmlGeneratorService;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly SriApiClientService _sriApiClientService;
        private readonly SriResponseParserService _sriResponseParserService;
        private readonly IEmailService _emailService;
        private readonly PdfGeneratorService _pdfGenerator;
        private static readonly SemaphoreSlim _ncSemaphore = new SemaphoreSlim(1, 1);
        private static readonly ConcurrentDictionary<Guid, byte> _processingCreditNotes = new ConcurrentDictionary<Guid, byte>();

        public CreditNoteService(
            FacturasSRIDbContext context,
            ILogger<CreditNoteService> logger,
            IConfiguration configuration,
            XmlGeneratorService xmlGeneratorService,
            IServiceScopeFactory serviceScopeFactory,
            SriApiClientService sriApiClientService,
            SriResponseParserService sriResponseParserService,
            IEmailService emailService,
            PdfGeneratorService pdfGenerator)
        {
            _context = context;
            _logger = logger;
            _configuration = configuration;
            _xmlGeneratorService = xmlGeneratorService;
            _serviceScopeFactory = serviceScopeFactory;
            _sriApiClientService = sriApiClientService;
            _sriResponseParserService = sriResponseParserService;
            _emailService = emailService;
            _pdfGenerator = pdfGenerator;
        }

        public Task ResendCreditNoteEmailAsync(Guid creditNoteId)
        {
            Task.Run(async () =>
            {
                using var scope = _serviceScopeFactory.CreateScope();
                var scopedContext = scope.ServiceProvider.GetRequiredService<FacturasSRIDbContext>();
                var scopedEmail = scope.ServiceProvider.GetRequiredService<IEmailService>();
                var scopedPdf = scope.ServiceProvider.GetRequiredService<PdfGeneratorService>();
                var scopedService = scope.ServiceProvider.GetRequiredService<ICreditNoteService>();

                var creditNote = await scopedService.GetCreditNoteDetailByIdAsync(creditNoteId);
                var creditNoteEntity = await scopedContext.NotasDeCredito
                    .Include(i => i.InformacionSRI)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(i => i.Id == creditNoteId);

                if (creditNote == null || creditNoteEntity == null) return;
                if (string.IsNullOrEmpty(creditNote.ClienteEmail)) return;

                try
                {
                    var pdfBytes = scopedPdf.GenerarNotaCreditoPdf(creditNote);
                    var xmlFirmado = creditNoteEntity.InformacionSRI?.XmlFirmado ?? "";

                    await scopedEmail.SendCreditNoteEmailAsync(
                        creditNote.ClienteEmail,
                        creditNote.ClienteNombre,
                        creditNote.NumeroNotaCredito,
                        creditNote.Id,
                        pdfBytes,
                        xmlFirmado
                    );
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error enviando email de nota de crédito en background");
                }
            });
            return Task.CompletedTask;
        }

        public async Task<List<CreditNoteDto>> GetCreditNotesAsync()
        {
            return await _context.NotasDeCredito
                .AsNoTracking()
                .Include(nc => nc.Cliente)
                .Include(nc => nc.Factura)
                .OrderByDescending(nc => nc.FechaEmision)
                .Select(nc => new CreditNoteDto
                {
                    Id = nc.Id,
                    NumeroNotaCredito = nc.NumeroNotaCredito,
                    FechaEmision = nc.FechaEmision,
                    ClienteNombre = nc.Cliente != null ? nc.Cliente.RazonSocial : "N/A",
                    NumeroFacturaModificada = nc.Factura != null ? nc.Factura.NumeroFactura : "N/A",
                    Total = nc.Total,
                    Estado = nc.Estado,
                    RazonModificacion = nc.RazonModificacion
                })
                .ToListAsync();
        }

        public async Task<CreditNoteDetailViewDto?> GetCreditNoteDetailByIdAsync(Guid id)
        {
            await CheckSriStatusAsync(id);

            var nc = await _context.NotasDeCredito
                .Include(n => n.Cliente)
                .Include(n => n.Factura)
                .Include(n => n.InformacionSRI)
                .Include(n => n.Detalles)
                    .ThenInclude(d => d.Producto)
                    .ThenInclude(p => p.ProductoImpuestos)
                    .ThenInclude(pi => pi.Impuesto)
                .FirstOrDefaultAsync(n => n.Id == id);

            if (nc == null) return null;

            var itemsDto = nc.Detalles.Select(d => new CreditNoteItemDetailDto
            {
                ProductoId = d.ProductoId,
                ProductCode = d.Producto?.CodigoPrincipal ?? "N/A",
                ProductName = d.Producto?.Nombre ?? "N/A",
                Cantidad = d.Cantidad,
                PrecioVentaUnitario = d.PrecioVentaUnitario,
                Subtotal = d.Subtotal
            }).ToList();

            var taxSummaries = nc.Detalles
                .SelectMany(d => d.Producto?.ProductoImpuestos.Select(pi => new
                {
                    d.Subtotal,
                    TaxName = pi.Impuesto?.Nombre ?? "N/A",
                    TaxRate = pi.Impuesto?.Porcentaje ?? 0
                }) ?? Enumerable.Empty<object>().Select(x => new { Subtotal = 0m, TaxName = "", TaxRate = 0m }))
                .GroupBy(x => new { x.TaxName, x.TaxRate })
                .Select(g => new TaxSummary
                {
                    TaxName = g.Key.TaxName,
                    TaxRate = g.Key.TaxRate,
                    Amount = g.Sum(x => x.Subtotal * (x.TaxRate / 100m))
                })
                .Where(x => x.Amount > 0 || x.TaxRate == 0)
                .ToList();

            return new CreditNoteDetailViewDto
            {
                Id = nc.Id,
                NumeroNotaCredito = nc.NumeroNotaCredito,
                FechaEmision = nc.FechaEmision,
                ClienteNombre = nc.Cliente?.RazonSocial ?? "N/A",
                ClienteIdentificacion = nc.Cliente?.NumeroIdentificacion ?? "N/A",
                ClienteDireccion = nc.Cliente?.Direccion ?? "",
                ClienteEmail = nc.Cliente?.Email ?? "",
                FacturaId = nc.FacturaId,
                NumeroFacturaModificada = nc.Factura?.NumeroFactura ?? "N/A",
                FechaEmisionFacturaModificada = nc.Factura?.FechaEmision ?? DateTime.MinValue,
                RazonModificacion = nc.RazonModificacion,
                SubtotalSinImpuestos = nc.SubtotalSinImpuestos,
                TotalIVA = nc.TotalIVA,
                Total = nc.Total,
                Estado = nc.Estado,
                ClaveAcceso = nc.InformacionSRI?.ClaveAcceso,
                NumeroAutorizacion = nc.InformacionSRI?.NumeroAutorizacion,
                RespuestaSRI = nc.InformacionSRI?.RespuestaSRI,
                Items = itemsDto,
                TaxSummaries = taxSummaries
            };
        }

        public async Task<NotaDeCredito> CreateCreditNoteAsync(CreateCreditNoteDto dto)
        {
            await _ncSemaphore.WaitAsync();
            try
            {
                var factura = await _context.Facturas
                    .Include(f => f.Cliente)
                    .Include(f => f.Detalles).ThenInclude(d => d.Producto).ThenInclude(p => p.ProductoImpuestos).ThenInclude(pi => pi.Impuesto)
                    .FirstOrDefaultAsync(f => f.Id == dto.FacturaId);

                if (factura == null) throw new InvalidOperationException("La factura original no existe.");

                // Evitamos error de Stale Data
                await _context.Entry(factura).ReloadAsync();

                if (factura.Estado != EstadoFactura.Autorizada) throw new InvalidOperationException("Solo se pueden emitir Notas de Crédito a facturas AUTORIZADAS.");

                var establishmentCode = _configuration["CompanyInfo:EstablishmentCode"] ?? throw new InvalidOperationException("Falta 'CompanyInfo:EstablishmentCode'");
                var emissionPointCode = _configuration["CompanyInfo:EmissionPointCode"] ?? throw new InvalidOperationException("Falta 'CompanyInfo:EmissionPointCode'");
                var rucEmisor = _configuration["CompanyInfo:Ruc"] ?? throw new InvalidOperationException("Falta 'CompanyInfo:Ruc'");
                var environmentType = _configuration["CompanyInfo:EnvironmentType"] ?? throw new InvalidOperationException("Falta 'CompanyInfo:EnvironmentType'");
                var certPath = _configuration["CompanyInfo:CertificatePath"] ?? throw new InvalidOperationException("Falta 'CompanyInfo:CertificatePath'");
                var certPass = _configuration["CompanyInfo:CertificatePassword"] ?? throw new InvalidOperationException("Falta 'CompanyInfo:CertificatePassword'");

                var secuencialEntity = await _context.Secuenciales.FirstOrDefaultAsync(s => s.Establecimiento == establishmentCode && s.PuntoEmision == emissionPointCode);
                if (secuencialEntity == null)
                {
                    secuencialEntity = new Secuencial { Id = Guid.NewGuid(), Establecimiento = establishmentCode, PuntoEmision = emissionPointCode, UltimoSecuencialFactura = 0, UltimoSecuencialNotaCredito = 0 };
                    _context.Secuenciales.Add(secuencialEntity);
                }
                secuencialEntity.UltimoSecuencialNotaCredito++;
                string numeroSecuencialStr = secuencialEntity.UltimoSecuencialNotaCredito.ToString("D9");

                if (factura.ClienteId == null) throw new InvalidOperationException("La factura no tiene un cliente asignado.");

                var nc = new NotaDeCredito
                {
                    Id = Guid.NewGuid(),
                    FacturaId = factura.Id,
                    ClienteId = factura.ClienteId.Value,
                    FechaEmision = DateTime.UtcNow,
                    NumeroNotaCredito = numeroSecuencialStr,
                    // === CORRECCIÓN DE ESTADO INICIAL ===
                    // Se crea como PENDIENTE, igual que una Factura.
                    // Si el envío falla por red, se queda aquí y permite reintentar.
                    Estado = dto.EsBorrador ? EstadoNotaDeCredito.Borrador : EstadoNotaDeCredito.Pendiente,
                    RazonModificacion = dto.RazonModificacion,
                    UsuarioIdCreador = dto.UsuarioIdCreador,
                    FechaCreacion = DateTime.UtcNow
                };

                decimal subtotalAccum = 0;
                decimal ivaAccum = 0;

                foreach (var itemDto in dto.Items)
                {
                    if (itemDto.CantidadDevolucion <= 0) continue;

                    var detalleFactura = factura.Detalles.FirstOrDefault(d => d.ProductoId == itemDto.ProductoId);
                    if (detalleFactura == null || detalleFactura.Producto == null) throw new Exception($"Producto ID {itemDto.ProductoId} inválido.");
                    if (itemDto.CantidadDevolucion > (detalleFactura.Cantidad - detalleFactura.CantidadDevuelta)) throw new Exception($"La cantidad a devolver para '{detalleFactura.Producto.Nombre}' excede la cantidad disponible ({detalleFactura.Cantidad - detalleFactura.CantidadDevuelta}).");

                    decimal precioUnit = detalleFactura.PrecioVentaUnitario;
                    decimal subtotalItem = itemDto.CantidadDevolucion * precioUnit;
                    decimal valorIvaItem = 0;
                    var impuestoIva = detalleFactura.Producto.ProductoImpuestos.FirstOrDefault(pi => pi.Impuesto != null && pi.Impuesto.Porcentaje > 0);
                    if (impuestoIva != null && impuestoIva.Impuesto != null) valorIvaItem = subtotalItem * (impuestoIva.Impuesto.Porcentaje / 100);

                    var detalleNc = new NotaDeCreditoDetalle
                    {
                        Id = Guid.NewGuid(),
                        NotaDeCreditoId = nc.Id,
                        ProductoId = itemDto.ProductoId,
                        Producto = detalleFactura.Producto, 
                        Cantidad = itemDto.CantidadDevolucion,
                        PrecioVentaUnitario = precioUnit,
                        DescuentoAplicado = 0,
                        Subtotal = subtotalItem,
                        ValorIVA = valorIvaItem
                    };
            
                    nc.Detalles.Add(detalleNc);

                    subtotalAccum += subtotalItem;
                    ivaAccum += valorIvaItem;
                }

                nc.SubtotalSinImpuestos = subtotalAccum;
                nc.TotalIVA = ivaAccum;
                nc.Total = subtotalAccum + ivaAccum;

                _context.NotasDeCredito.Add(nc);

                var fechaEcuador = GetEcuadorTime(nc.FechaEmision);
                string claveAcceso = GenerarClaveAcceso(fechaEcuador, "04", rucEmisor, establishmentCode, emissionPointCode, numeroSecuencialStr, environmentType);

                var ncSri = new NotaDeCreditoSRI
                {
                    NotaDeCreditoId = nc.Id,
                    ClaveAcceso = claveAcceso
                };
                _context.NotasDeCreditoSRI.Add(ncSri);

                if (!dto.EsBorrador)
                {
                    if (factura.Cliente == null) throw new InvalidOperationException("Cliente es nulo en la factura original.");

                    var (xmlGenerado, xmlFirmadoBytes) = _xmlGeneratorService.GenerarYFirmarNotaCredito(claveAcceso, nc, factura.Cliente, factura, certPath, certPass);

                    ncSri.XmlGenerado = xmlGenerado;
                    ncSri.XmlFirmado = Encoding.UTF8.GetString(xmlFirmadoBytes);
            
                    // NO cambiamos el estado aquí. Se queda en Pendiente.
                    // El Background Task se encargará de moverlo a EnviadaSRI si tiene éxito.

                    await _context.SaveChangesAsync();
                    _ = Task.Run(() => EnviarNcAlSriEnFondoAsync(nc.Id, xmlFirmadoBytes));
                }
                else
                {
                    await _context.SaveChangesAsync();
                }

                return nc;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creando Nota de Crédito");
                throw;
            }
            finally
            {
                _ncSemaphore.Release();
            }
        }

        private async Task EnviarNcAlSriEnFondoAsync(Guid ncId, byte[] xmlFirmadoBytes)
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var scopedSriClient = scope.ServiceProvider.GetRequiredService<SriApiClientService>();
            var scopedParser = scope.ServiceProvider.GetRequiredService<SriResponseParserService>();
            var scopedContext = scope.ServiceProvider.GetRequiredService<FacturasSRIDbContext>();

            try
            {
                var nc = await scopedContext.NotasDeCredito
                    .Include(n => n.InformacionSRI)
                    .FirstOrDefaultAsync(n => n.Id == ncId);
                    
                if (nc == null || nc.InformacionSRI == null) return;

                string respuestaRecepcionXml = await scopedSriClient.EnviarRecepcionAsync(xmlFirmadoBytes);
                var respuestaRecepcion = scopedParser.ParsearRespuestaRecepcion(respuestaRecepcionXml);

                bool esClaveRepetida = respuestaRecepcion.Estado == "DEVUELTA" && respuestaRecepcion.Errores.Any(e => e.Identificador == "43");

                if (respuestaRecepcion.Estado == "DEVUELTA" && !esClaveRepetida)
                {
                    nc.Estado = EstadoNotaDeCredito.RechazadaSRI;
                    nc.InformacionSRI.RespuestaSRI = JsonSerializer.Serialize(respuestaRecepcion.Errores);
                    await scopedContext.SaveChangesAsync();
                    return;
                }
                else 
                {
                    // Si se recibió correctamente (o era repetida), cambiamos a EnviadaSRI
                    if(nc.Estado == EstadoNotaDeCredito.Pendiente)
                    {
                        nc.Estado = EstadoNotaDeCredito.EnviadaSRI;
                        await scopedContext.SaveChangesAsync();
                    }
                }
                
                RespuestaAutorizacion? respuestaAutorizacion = null;
                for (int i = 0; i < 4; i++)
                {
                    await Task.Delay(new[] { 2500, 5000, 10000, 15000 }[i]);
                    if (string.IsNullOrEmpty(nc.InformacionSRI.ClaveAcceso)) break;

                    string respuestaAutorizacionXml = await scopedSriClient.ConsultarAutorizacionAsync(nc.InformacionSRI.ClaveAcceso);
                    respuestaAutorizacion = scopedParser.ParsearRespuestaAutorizacion(respuestaAutorizacionXml);
                    if (respuestaAutorizacion.Estado != "PROCESANDO") break;
                }

                if (respuestaAutorizacion != null)
                {
                    await FinalizeAuthorizationAsync(ncId, respuestaAutorizacion);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[BG-NC] Error crítico en EnviarNcAlSriEnFondoAsync para NC {NcId}", ncId);
                
                // === LÓGICA DE FALLO POR RED ===
                // Si falla la conexión, nos aseguramos de que el estado sea PENDIENTE.
                // Así, 'CheckSriStatusAsync' lo detectará y reintentará.
                // También guardamos el error en RespuestaSRI para que el usuario sepa qué pasó.
                try 
                {
                    // Recargamos por si el contexto anterior murió
                    var ncError = await scopedContext.NotasDeCredito.Include(n => n.InformacionSRI).FirstOrDefaultAsync(n => n.Id == ncId);
                    if(ncError != null)
                    {
                        ncError.Estado = EstadoNotaDeCredito.Pendiente;
                        if(ncError.InformacionSRI != null) 
                        {
                            ncError.InformacionSRI.RespuestaSRI = $"Error de red (Reintentando...): {ex.Message}";
                        }
                        await scopedContext.SaveChangesAsync();
                    }
                }
                catch(Exception dbEx)
                {
                     _logger.LogError(dbEx, "Error guardando estado de fallo de red en BD");
                }
            }
        }

        public async Task CheckSriStatusAsync(Guid ncId)
        {
            var nc = await _context.NotasDeCredito.Include(n => n.InformacionSRI).AsNoTracking().FirstOrDefaultAsync(n => n.Id == ncId);
            
            if (nc == null || nc.InformacionSRI == null) return;
            
            if (nc.Estado == EstadoNotaDeCredito.Autorizada || nc.Estado == EstadoNotaDeCredito.Cancelada) return;
            
            try
            {
                // Si está PENDIENTE, forzamos el envío (Recepción) porque significa que falló antes.
                bool forzarRecepcion = nc.Estado == EstadoNotaDeCredito.Pendiente;

                if (!forzarRecepcion)
                {
                    if (string.IsNullOrEmpty(nc.InformacionSRI.ClaveAcceso)) return;
                    
                    string authXml = await _sriApiClientService.ConsultarAutorizacionAsync(nc.InformacionSRI.ClaveAcceso);
                    var respAuth = _sriResponseParserService.ParsearRespuestaAutorizacion(authXml);

                    if (respAuth.Estado != "PROCESANDO")
                    {
                        await FinalizeAuthorizationAsync(ncId, respAuth);
                        return;
                    }
                    else
                    {
                        // Si sigue procesando (o no existe en SRI), intentamos re-enviar por si acaso
                        forzarRecepcion = true;
                    }
                }

                if (forzarRecepcion)
                {
                    if (string.IsNullOrEmpty(nc.InformacionSRI.XmlFirmado)) return;

                    byte[] xmlBytes = Encoding.UTF8.GetBytes(nc.InformacionSRI.XmlFirmado);
                    string recepXml = await _sriApiClientService.EnviarRecepcionAsync(xmlBytes);
                    var respRecep = _sriResponseParserService.ParsearRespuestaRecepcion(recepXml);
                    
                    if(respRecep.Estado == "DEVUELTA") 
                    {
                         bool esErrorDuplicado = respRecep.Errores.Any(e => e.Identificador == "43");

                         if (!esErrorDuplicado) 
                         {
                             var ncToUpdate = await _context.NotasDeCredito.Include(n => n.InformacionSRI).FirstOrDefaultAsync(n => n.Id == ncId);
                             if(ncToUpdate != null)
                             {
                                 ncToUpdate.Estado = EstadoNotaDeCredito.RechazadaSRI;
                                 if (ncToUpdate.InformacionSRI != null) 
                                 {
                                    ncToUpdate.InformacionSRI.RespuestaSRI = JsonSerializer.Serialize(respRecep.Errores);
                                 }
                                 await _context.SaveChangesAsync();
                             }
                             return;
                         }
                         else 
                         {
                             // Ya recibida, avanzar a EnviadaSRI
                             if(nc.Estado != EstadoNotaDeCredito.EnviadaSRI)
                             {
                                var ncUpdate = await _context.NotasDeCredito.FindAsync(ncId);
                                if(ncUpdate != null) {
                                    ncUpdate.Estado = EstadoNotaDeCredito.EnviadaSRI;
                                    await _context.SaveChangesAsync();
                                }
                             }
                         }
                    }
                    else
                    {
                         // Recibida OK
                         if(nc.Estado != EstadoNotaDeCredito.EnviadaSRI)
                         {
                            var ncUpdate = await _context.NotasDeCredito.FindAsync(ncId);
                            if(ncUpdate != null) {
                                ncUpdate.Estado = EstadoNotaDeCredito.EnviadaSRI;
                                await _context.SaveChangesAsync();
                            }
                         }
                    }
                    
                    await Task.Delay(2000);
                    
                    if (!string.IsNullOrEmpty(nc.InformacionSRI.ClaveAcceso))
                    {
                        string authXml = await _sriApiClientService.ConsultarAutorizacionAsync(nc.InformacionSRI.ClaveAcceso);
                        var respAuth = _sriResponseParserService.ParsearRespuestaAutorizacion(authXml);
                        
                        if(respAuth.Estado != "PROCESANDO")
                        {
                            await FinalizeAuthorizationAsync(ncId, respAuth);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en CheckSriStatusAsync para NC {NcId}", ncId);
            }
        }

        private async Task FinalizeAuthorizationAsync(Guid ncId, RespuestaAutorizacion respuesta)
        {
            if (!_processingCreditNotes.TryAdd(ncId, 0))
            {
                return;
            }

            try
            {
                using var scope = _serviceScopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<FacturasSRIDbContext>();

                var nc = await context.NotasDeCredito
                    .Include(n => n.InformacionSRI)
                    .Include(n => n.Cliente)
                    .Include(n => n.Detalles).ThenInclude(d => d.Producto).ThenInclude(p => p.ProductoImpuestos).ThenInclude(pi => pi.Impuesto)
                    .Include(n => n.Factura).ThenInclude(f => f.Detalles)
                    .FirstOrDefaultAsync(n => n.Id == ncId);
                
                if (nc == null || nc.Estado == EstadoNotaDeCredito.Autorizada || nc.InformacionSRI == null || nc.Cliente == null) return;

                switch (respuesta.Estado)
                {
                    case "AUTORIZADO":
                        nc.Estado = EstadoNotaDeCredito.Autorizada;
                        nc.InformacionSRI.NumeroAutorizacion = respuesta.NumeroAutorizacion;
                        nc.InformacionSRI.FechaAutorizacion = respuesta.FechaAutorizacion;
                        nc.InformacionSRI.RespuestaSRI = "AUTORIZADO";
                        
                        await RestaurarStockAsync(context, nc);
                        await ActualizarCantidadesDevueltasAsync(context, nc);
                        
                        await context.SaveChangesAsync();

                        try
                        {
                            var itemsDto = nc.Detalles.Select(d => new CreditNoteItemDetailDto { 
                                ProductCode = d.Producto?.CodigoPrincipal ?? "N/A", 
                                ProductName = d.Producto?.Nombre ?? "N/A", 
                                Cantidad = d.Cantidad, 
                                PrecioVentaUnitario = d.PrecioVentaUnitario, 
                                Subtotal = d.Subtotal 
                            }).ToList();
                            
                            var taxSummaries = nc.Detalles
                                .SelectMany(d => d.Producto?.ProductoImpuestos.Select(pi => new 
                                { d.Subtotal, TaxName = pi.Impuesto?.Nombre ?? "", TaxRate = pi.Impuesto?.Porcentaje ?? 0 }) ?? Enumerable.Empty<object>().Select(x => new { Subtotal = 0m, TaxName = "", TaxRate = 0m }))
                                .GroupBy(x => new { x.TaxName, x.TaxRate })
                                .Select(g => new TaxSummary { TaxName = g.Key.TaxName, TaxRate = g.Key.TaxRate, Amount = g.Sum(x => x.Subtotal * (x.TaxRate / 100m)) })
                                .Where(x => x.Amount > 0 || x.TaxRate == 0).ToList();

                            var ncDto = new CreditNoteDetailViewDto
                            {
                                Id = nc.Id,
                                NumeroNotaCredito = nc.NumeroNotaCredito,
                                FechaEmision = nc.FechaEmision,
                                ClienteNombre = nc.Cliente.RazonSocial,
                                ClienteIdentificacion = nc.Cliente.NumeroIdentificacion,
                                ClienteDireccion = nc.Cliente.Direccion,
                                ClienteEmail = nc.Cliente.Email,
                                NumeroFacturaModificada = nc.Factura != null ? nc.Factura.NumeroFactura : "N/A",
                                FechaEmisionFacturaModificada = nc.Factura?.FechaEmision ?? DateTime.MinValue,
                                RazonModificacion = nc.RazonModificacion,
                                SubtotalSinImpuestos = nc.SubtotalSinImpuestos,
                                TotalIVA = nc.TotalIVA,
                                Total = nc.Total,
                                ClaveAcceso = nc.InformacionSRI.ClaveAcceso,
                                NumeroAutorizacion = nc.InformacionSRI.NumeroAutorizacion,
                                Items = itemsDto,
                                TaxSummaries = taxSummaries
                            };

                            var pdfGenerator = scope.ServiceProvider.GetRequiredService<PdfGeneratorService>();
                            var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
                            byte[] pdfBytes = pdfGenerator.GenerarNotaCreditoPdf(ncDto);

                            if (!string.IsNullOrEmpty(nc.Cliente.Email))
                            {
                                await emailService.SendCreditNoteEmailAsync(nc.Cliente.Email, nc.Cliente.RazonSocial, nc.NumeroNotaCredito, nc.Id, pdfBytes, nc.InformacionSRI.XmlFirmado ?? "");
                            }
                        }
                        catch (Exception emailEx)
                        {
                            _logger.LogError(emailEx, "Error enviando correo post-autorización para NC {NcId}", ncId);
                        }
                        break;
                        
                    case "NO AUTORIZADO":
                        nc.Estado = EstadoNotaDeCredito.RechazadaSRI;
                        nc.InformacionSRI.RespuestaSRI = JsonSerializer.Serialize(respuesta.Errores);
                        await context.SaveChangesAsync();
                        break;

                    default:
                        if (respuesta.Errores != null && respuesta.Errores.Any())
                        {
                            nc.InformacionSRI.RespuestaSRI = JsonSerializer.Serialize(respuesta.Errores);
                            await context.SaveChangesAsync();
                        }
                        break;
                }
            }
            finally
            {
                _processingCreditNotes.TryRemove(ncId, out _);
            }
        }
        
        private async Task RestaurarStockAsync(FacturasSRIDbContext context, NotaDeCredito nc)
        {
            foreach (var detalle in nc.Detalles)
            {
                var producto = await context.Productos.FindAsync(detalle.ProductoId);
                if (producto != null && producto.ManejaInventario)
                {
                    if (producto.ManejaLotes)
                    {
                        var consumos = await context.FacturaDetalleConsumoLotes
                            .Include(c => c.FacturaDetalle)
                            .Where(c => c.FacturaDetalle.FacturaId == nc.FacturaId && c.FacturaDetalle.ProductoId == detalle.ProductoId)
                            .Include(c => c.Lote)
                            .OrderByDescending(c => c.Lote.FechaCaducidad)
                            .ToListAsync();

                        int remanente = detalle.Cantidad;
                        foreach (var consumo in consumos)
                        {
                            if (remanente <= 0) break;
                            if (consumo.Lote != null)
                            {
                                consumo.Lote.CantidadDisponible += remanente; 
                                remanente = 0;
                            }
                        }
                    }
                    else
                    {
                        producto.StockTotal += detalle.Cantidad;
                    }
                }
            }
        }

        private async Task ActualizarCantidadesDevueltasAsync(FacturasSRIDbContext context, NotaDeCredito nc)
        {
            foreach (var detalleNc in nc.Detalles)
            {
                var facturaDetalle = await context.FacturaDetalles.FirstOrDefaultAsync(fd => fd.FacturaId == nc.FacturaId && fd.ProductoId == detalleNc.ProductoId);
                if (facturaDetalle != null)
                {
                    facturaDetalle.CantidadDevuelta += detalleNc.Cantidad;
                }
            }
        }
        
        private string GenerarClaveAcceso(DateTime fechaEmision, string tipoComprobante, string ruc, string establecimiento, string puntoEmision, string secuencial, string tipoAmbiente)
        {
            var fecha = fechaEmision.ToString("ddMMyyyy");
            var tipoEmision = "1";
            var codigoNumerico = "12345678";
            var clave = new StringBuilder();
            clave.Append(fecha).Append(tipoComprobante).Append(ruc).Append(tipoAmbiente).Append(establecimiento).Append(puntoEmision).Append(secuencial).Append(codigoNumerico).Append(tipoEmision);
            if (clave.Length != 48) throw new InvalidOperationException($"Error long clave: {clave.Length}");
            clave.Append(CalcularDigitoVerificador(clave.ToString()));
            return clave.ToString();
        }

        private int CalcularDigitoVerificador(string clave)
        {
            var reverso = clave.Reverse().ToArray();
            var suma = 0;
            var factor = 2;
            for (int i = 0; i < reverso.Length; i++) { suma += (int)char.GetNumericValue(reverso[i]) * factor; factor++; if (factor > 7) factor = 2; }
            int modulo = suma % 11; int digito = 11 - modulo; if (digito == 11) return 0; if (digito == 10) return 1; return digito;
        }

        private DateTime GetEcuadorTime(DateTime utcTime)
        {
            try { var tz = TimeZoneInfo.FindSystemTimeZoneById("SA Pacific Standard Time"); return TimeZoneInfo.ConvertTimeFromUtc(utcTime, tz); } catch { return utcTime.AddHours(-5); }
        }

        public async Task CancelCreditNoteAsync(Guid creditNoteId)
        {
            var nc = await _context.NotasDeCredito.FindAsync(creditNoteId);
            if (nc == null)
            {
                throw new InvalidOperationException("La nota de crédito no existe.");
            }
            if (nc.Estado != EstadoNotaDeCredito.Borrador)
            {
                throw new InvalidOperationException("Solo se pueden cancelar notas de crédito en estado Borrador.");
            }
            nc.Estado = EstadoNotaDeCredito.Cancelada;
            await _context.SaveChangesAsync();
        }

        public async Task ReactivateCancelledCreditNoteAsync(Guid creditNoteId)
        {
            var nc = await _context.NotasDeCredito.FindAsync(creditNoteId);
            if (nc == null)
            {
                throw new InvalidOperationException("La nota de crédito no existe.");
            }
            if (nc.Estado != EstadoNotaDeCredito.Cancelada)
            {
                throw new InvalidOperationException("Solo se pueden reactivar notas de crédito en estado Cancelada.");
            }
            nc.Estado = EstadoNotaDeCredito.Borrador;
            await _context.SaveChangesAsync();
        }

        public async Task<CreditNoteDetailViewDto?> IssueDraftCreditNoteAsync(Guid creditNoteId)
        {
            var nc = await _context.NotasDeCredito
                .Include(n => n.Cliente)
                .Include(n => n.Factura)
                .Include(n => n.InformacionSRI)
                // === CORRECCIÓN 2: CARGA PROFUNDA PARA IMPUESTOS EN BORRADORES ===
                .Include(n => n.Detalles)
                    .ThenInclude(d => d.Producto)
                        .ThenInclude(p => p.ProductoImpuestos)
                            .ThenInclude(pi => pi.Impuesto)
                .FirstOrDefaultAsync(n => n.Id == creditNoteId);

            if (nc == null) throw new InvalidOperationException("La nota de crédito no existe.");
            if (nc.Estado != EstadoNotaDeCredito.Borrador) throw new InvalidOperationException("Solo se pueden emitir notas de crédito en estado Borrador.");
            
            var certPath = _configuration["CompanyInfo:CertificatePath"] ?? throw new InvalidOperationException("Falta configuración de certificado.");
            var certPass = _configuration["CompanyInfo:CertificatePassword"] ?? throw new InvalidOperationException("Falta password de certificado.");

            if (nc.InformacionSRI == null || string.IsNullOrEmpty(nc.InformacionSRI.ClaveAcceso)) throw new InvalidOperationException("La NC no tiene clave de acceso generada.");
            if (nc.Cliente == null) throw new InvalidOperationException("Cliente no encontrado en NC.");
            if (nc.Factura == null) throw new InvalidOperationException("Factura no encontrada en NC.");

            var (xmlGenerado, xmlFirmadoBytes) = _xmlGeneratorService.GenerarYFirmarNotaCredito(nc.InformacionSRI.ClaveAcceso, nc, nc.Cliente, nc.Factura, certPath, certPass);

            nc.InformacionSRI.XmlGenerado = xmlGenerado;
            nc.InformacionSRI.XmlFirmado = Encoding.UTF8.GetString(xmlFirmadoBytes);
            
            // Se queda como PENDIENTE para que el background intente enviar
            // y si falla, CheckStatus pueda reintentar.
            nc.Estado = EstadoNotaDeCredito.Pendiente;

            await _context.SaveChangesAsync();

            _ = Task.Run(() => EnviarNcAlSriEnFondoAsync(nc.Id, xmlFirmadoBytes));
            
            return await GetCreditNoteDetailByIdAsync(nc.Id);
        }

        public async Task<CreditNoteDto?> UpdateCreditNoteAsync(UpdateCreditNoteDto dto)
        {
            var nc = await _context.NotasDeCredito
                .Include(n => n.Detalles)
                .Include(n => n.Factura).ThenInclude(f => f.Detalles)
                .FirstOrDefaultAsync(n => n.Id == dto.Id);

            if (nc == null) throw new InvalidOperationException("La nota de crédito no existe.");
            if (nc.Estado != EstadoNotaDeCredito.Borrador) throw new InvalidOperationException("Solo se pueden modificar borradores.");

            nc.RazonModificacion = dto.RazonModificacion;

            var oldDetails = nc.Detalles.ToList();
            var newDetails = new List<NotaDeCreditoDetalle>();

            decimal subtotalAccum = 0;
            decimal ivaAccum = 0;

            foreach (var itemDto in dto.Items)
            {
                if (itemDto.CantidadDevolucion <= 0) continue;

                var detalleFactura = nc.Factura.Detalles.FirstOrDefault(d => d.ProductoId == itemDto.ProductoId);
                if (detalleFactura == null) throw new Exception($"Producto ID {itemDto.ProductoId} inválido.");
                
                if (itemDto.CantidadDevolucion > (detalleFactura.Cantidad - detalleFactura.CantidadDevuelta)) throw new Exception($"La cantidad a devolver para el producto excede la cantidad disponible en la factura original.");

                decimal precioUnit = detalleFactura.PrecioVentaUnitario;
                decimal subtotalItem = itemDto.CantidadDevolucion * precioUnit;
                decimal valorIvaItem = 0;
                
                var producto = await _context.Productos.Include(p => p.ProductoImpuestos).ThenInclude(pi => pi.Impuesto).FirstAsync(p => p.Id == itemDto.ProductoId);
                var impuestoIva = producto.ProductoImpuestos.FirstOrDefault(pi => pi.Impuesto != null && pi.Impuesto.Porcentaje > 0);
                if (impuestoIva != null && impuestoIva.Impuesto != null) valorIvaItem = subtotalItem * (impuestoIva.Impuesto.Porcentaje / 100);

                var detalleNc = new NotaDeCreditoDetalle
                {
                    NotaDeCreditoId = nc.Id,
                    ProductoId = itemDto.ProductoId,
                    // === CORRECCIÓN 3: ASIGNAR EL PRODUCTO MANUALMENTE AL ACTUALIZAR ===
                    Producto = producto,
                    Cantidad = itemDto.CantidadDevolucion,
                    PrecioVentaUnitario = precioUnit,
                    Subtotal = subtotalItem,
                    ValorIVA = valorIvaItem
                };
                newDetails.Add(detalleNc);

                subtotalAccum += subtotalItem;
                ivaAccum += valorIvaItem;
            }

            _context.NotaDeCreditoDetalles.RemoveRange(oldDetails);
            nc.Detalles = newDetails;

            nc.SubtotalSinImpuestos = subtotalAccum;
            nc.TotalIVA = ivaAccum;
            nc.Total = subtotalAccum + ivaAccum;

            await _context.SaveChangesAsync();

            if (dto.EmitirTrasGuardar)
            {
                await IssueDraftCreditNoteAsync(nc.Id);
            }

            var resultDto = await GetCreditNoteDetailByIdAsync(nc.Id);
            return resultDto != null ? new CreditNoteDto { Id = resultDto.Id, NumeroNotaCredito = resultDto.NumeroNotaCredito } : null;
        }
    }
}