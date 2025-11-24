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
        
        public async Task ResendCreditNoteEmailAsync(Guid creditNoteId)
        {
            await Task.Run(async () =>
            {
                var creditNote = await GetCreditNoteDetailByIdAsync(creditNoteId);
                var creditNoteEntity = await _context.NotasDeCredito.Include(i => i.InformacionSRI).FirstOrDefaultAsync(i => i.Id == creditNoteId);

                if (creditNote == null || creditNoteEntity == null) return;
                if (string.IsNullOrEmpty(creditNote.ClienteEmail)) return;

                try
                {
                    var pdfBytes = _pdfGenerator.GenerarNotaCreditoPdf(creditNote);
                    var xmlFirmado = creditNoteEntity.InformacionSRI?.XmlFirmado ?? "";

                    await _emailService.SendCreditNoteEmailAsync(
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
                    ClienteNombre = nc.Cliente.RazonSocial,
                    NumeroFacturaModificada = nc.Factura.NumeroFactura,
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
                ProductCode = d.Producto.CodigoPrincipal,
                ProductName = d.Producto.Nombre,
                Cantidad = d.Cantidad,
                PrecioVentaUnitario = d.PrecioVentaUnitario,
                Subtotal = d.Subtotal
            }).ToList();

            var taxSummaries = nc.Detalles
                .SelectMany(d => d.Producto.ProductoImpuestos.Select(pi => new
                {
                    d.Subtotal,
                    TaxName = pi.Impuesto.Nombre,
                    TaxRate = pi.Impuesto.Porcentaje
                }))
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
                ClienteNombre = nc.Cliente.RazonSocial,
                ClienteIdentificacion = nc.Cliente.NumeroIdentificacion,
                ClienteDireccion = nc.Cliente.Direccion,
                ClienteEmail = nc.Cliente.Email,
                FacturaId = nc.FacturaId,
                NumeroFacturaModificada = nc.Factura.NumeroFactura,
                FechaEmisionFacturaModificada = nc.Factura.FechaEmision,
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
        // CORRECCIÓN AQUI: Agregado .AsNoTracking() para leer el estado REAL de la base de datos
        var factura = await _context.Facturas
            .AsNoTracking() 
            .Include(f => f.Cliente)
            .Include(f => f.Detalles).ThenInclude(d => d.Producto).ThenInclude(p => p.ProductoImpuestos).ThenInclude(pi => pi.Impuesto)
            .FirstOrDefaultAsync(f => f.Id == dto.FacturaId);

        if (factura == null) throw new InvalidOperationException("La factura original no existe.");
        
        // Ahora esta validación pasará correctamente porque leemos el dato fresco
        if (factura.Estado != EstadoFactura.Autorizada) throw new InvalidOperationException("Solo se pueden emitir Notas de Crédito a facturas AUTORIZADAS.");

        var establishmentCode = _configuration["CompanyInfo:EstablishmentCode"];
        var emissionPointCode = _configuration["CompanyInfo:EmissionPointCode"];
        var rucEmisor = _configuration["CompanyInfo:Ruc"];
        var environmentType = _configuration["CompanyInfo:EnvironmentType"];
        var certPath = _configuration["CompanyInfo:CertificatePath"];
        var certPass = _configuration["CompanyInfo:CertificatePassword"];

        var secuencialEntity = await _context.Secuenciales.FirstOrDefaultAsync(s => s.Establecimiento == establishmentCode && s.PuntoEmision == emissionPointCode);
        if (secuencialEntity == null)
        {
            secuencialEntity = new Secuencial { Id = Guid.NewGuid(), Establecimiento = establishmentCode, PuntoEmision = emissionPointCode, UltimoSecuencialFactura = 0, UltimoSecuencialNotaCredito = 0 };
            _context.Secuenciales.Add(secuencialEntity);
        }
        secuencialEntity.UltimoSecuencialNotaCredito++;
        string numeroSecuencialStr = secuencialEntity.UltimoSecuencialNotaCredito.ToString("D9");

        var nc = new NotaDeCredito
        {
            Id = Guid.NewGuid(),
            FacturaId = factura.Id,
            ClienteId = factura.ClienteId.Value,
            FechaEmision = DateTime.UtcNow,
            NumeroNotaCredito = numeroSecuencialStr,
            // Iniciamos como Pendiente si no es borrador, igual que en Facturas
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
            if (detalleFactura == null) throw new Exception($"Producto ID {itemDto.ProductoId} inválido.");
            if (itemDto.CantidadDevolucion > (detalleFactura.Cantidad - detalleFactura.CantidadDevuelta)) throw new Exception($"La cantidad a devolver para '{detalleFactura.Producto.Nombre}' excede la cantidad disponible ({detalleFactura.Cantidad - detalleFactura.CantidadDevuelta}).");

            decimal precioUnit = detalleFactura.PrecioVentaUnitario;
            decimal subtotalItem = itemDto.CantidadDevolucion * precioUnit;
            decimal valorIvaItem = 0;
            var impuestoIva = detalleFactura.Producto.ProductoImpuestos.FirstOrDefault(pi => pi.Impuesto.Porcentaje > 0);
            if (impuestoIva != null) valorIvaItem = subtotalItem * (impuestoIva.Impuesto.Porcentaje / 100);

            var detalleNc = new NotaDeCreditoDetalle
            {
                Id = Guid.NewGuid(),
                NotaDeCreditoId = nc.Id,
                ProductoId = itemDto.ProductoId,
                Producto = detalleFactura.Producto, // EF Core lo vinculará aunque sea AsNoTracking si el ID es correcto
                Cantidad = itemDto.CantidadDevolucion,
                PrecioVentaUnitario = precioUnit,
                DescuentoAplicado = 0,
                Subtotal = subtotalItem,
                ValorIVA = valorIvaItem
            };
            
            // IMPORTANTE: Como factura es AsNoTracking, debemos decirle al contexto que el Producto YA existe y no intente crearlo de nuevo
            // Sin embargo, al asignar solo el ID suele bastar, pero aquí asignamos la entidad completa 'Producto'.
            // Para evitar duplicados, mejor asignamos null a la propiedad de navegación y dejamos solo el ID si fuera posible, 
            // o confiamos en que _context.NotasDeCredito.Add(nc) maneje el grafo.
            // Lo más seguro aquí con AsNoTracking es poner Producto = null y dejar ProductoId.
            detalleNc.Producto = null; 
            
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
            var (xmlGenerado, xmlFirmadoBytes) = _xmlGeneratorService.GenerarYFirmarNotaCredito(claveAcceso, nc, factura.Cliente, factura, certPath, certPass);

            ncSri.XmlGenerado = xmlGenerado;
            ncSri.XmlFirmado = Encoding.UTF8.GetString(xmlFirmadoBytes);
            
            // Actualizamos a EnviadaSRI justo antes de guardar y lanzar el hilo
            nc.Estado = EstadoNotaDeCredito.EnviadaSRI;

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
                    
                if (nc == null) return;

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
                
                RespuestaAutorizacion? respuestaAutorizacion = null;
                for (int i = 0; i < 4; i++)
                {
                    await Task.Delay(new[] { 2500, 5000, 10000, 15000 }[i]);
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
            }
        }

        public async Task CheckSriStatusAsync(Guid ncId)
        {
            var nc = await _context.NotasDeCredito.Include(n => n.InformacionSRI).FirstOrDefaultAsync(n => n.Id == ncId);
            if (nc == null || nc.InformacionSRI == null || nc.Estado == EstadoNotaDeCredito.Autorizada || nc.Estado == EstadoNotaDeCredito.Cancelada)
                return;
            
            try
            {
                string authXml = await _sriApiClientService.ConsultarAutorizacionAsync(nc.InformacionSRI.ClaveAcceso);
                var respAuth = _sriResponseParserService.ParsearRespuestaAutorizacion(authXml);

                if (respAuth.Estado != "PROCESANDO")
                {
                    await FinalizeAuthorizationAsync(ncId, respAuth);
                }
                else
                {
                    nc.InformacionSRI.RespuestaSRI = "El SRI todavía está procesando la autorización.";
                    await _context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en CheckSriStatusAsync para NC {NcId}", ncId);
                nc.InformacionSRI.RespuestaSRI = $"Error consultando al SRI: {ex.Message}";
                await _context.SaveChangesAsync();
            }
        }

        private async Task FinalizeAuthorizationAsync(Guid ncId, RespuestaAutorizacion respuesta)
        {
            if (!_processingCreditNotes.TryAdd(ncId, 0))
            {
                _logger.LogInformation("La NC {NcId} ya está siendo procesada. Se omite este intento.", ncId);
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
                
                if (nc == null || nc.Estado == EstadoNotaDeCredito.Autorizada) return;

                switch (respuesta.Estado)
                {
                    case "AUTORIZADO":
                        _logger.LogInformation("Finalizando autorización para NC {NcId}", ncId);
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
                                ProductCode = d.Producto.CodigoPrincipal, 
                                ProductName = d.Producto.Nombre, 
                                Cantidad = d.Cantidad, 
                                PrecioVentaUnitario = d.PrecioVentaUnitario, 
                                Subtotal = d.Subtotal 
                            }).ToList();
                            
                            var taxSummaries = nc.Detalles
                                .SelectMany(d => d.Producto.ProductoImpuestos.Select(pi => new 
                                { d.Subtotal, TaxName = pi.Impuesto.Nombre, TaxRate = pi.Impuesto.Porcentaje }))
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
                                NumeroFacturaModificada = nc.Factura.NumeroFactura,
                                FechaEmisionFacturaModificada = nc.Factura.FechaEmision,
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

                            await emailService.SendCreditNoteEmailAsync(nc.Cliente.Email, nc.Cliente.RazonSocial, nc.NumeroNotaCredito, nc.Id, pdfBytes, nc.InformacionSRI.XmlFirmado);
                            _logger.LogInformation("Correo de NC autorizada {NcId} enviado.", ncId);
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
                            consumo.Lote.CantidadDisponible += remanente; 
                            remanente = 0;
                        }
                    }
                    else
                    {
                        producto.StockTotal += detalle.Cantidad;
                    }
                }
            }
            _logger.LogInformation("Stock restaurado para Nota de Crédito {Numero}", nc.NumeroNotaCredito);
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
                .Include(n => n.Detalles).ThenInclude(d => d.Producto)
                .FirstOrDefaultAsync(n => n.Id == creditNoteId);

            if (nc == null) throw new InvalidOperationException("La nota de crédito no existe.");
            if (nc.Estado != EstadoNotaDeCredito.Borrador) throw new InvalidOperationException("Solo se pueden emitir notas de crédito en estado Borrador.");
            
            _logger.LogInformation("Iniciando emisión de Nota de Crédito borrador ID: {Id}", creditNoteId);

            var certPath = _configuration["CompanyInfo:CertificatePath"];
            var certPass = _configuration["CompanyInfo:CertificatePassword"];

            var (xmlGenerado, xmlFirmadoBytes) = _xmlGeneratorService.GenerarYFirmarNotaCredito(nc.InformacionSRI.ClaveAcceso, nc, nc.Cliente, nc.Factura, certPath, certPass);

            nc.InformacionSRI.XmlGenerado = xmlGenerado;
            nc.InformacionSRI.XmlFirmado = Encoding.UTF8.GetString(xmlFirmadoBytes);
            nc.Estado = EstadoNotaDeCredito.EnviadaSRI;

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
                var impuestoIva = producto.ProductoImpuestos.FirstOrDefault(pi => pi.Impuesto.Porcentaje > 0);
                if (impuestoIva != null) valorIvaItem = subtotalItem * (impuestoIva.Impuesto.Porcentaje / 100);

                var detalleNc = new NotaDeCreditoDetalle
                {
                    NotaDeCreditoId = nc.Id,
                    ProductoId = itemDto.ProductoId,
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
            _logger.LogInformation("Borrador de Nota de Crédito {Id} actualizado.", nc.Id);

            if (dto.EmitirTrasGuardar)
            {
                _logger.LogInformation("Flag 'EmitirTrasGuardar' detectado para NC. Emisión inmediata...");
                await IssueDraftCreditNoteAsync(nc.Id);
            }

            var resultDto = await GetCreditNoteDetailByIdAsync(nc.Id);
            return new CreditNoteDto { Id = resultDto.Id, NumeroNotaCredito = resultDto.NumeroNotaCredito };
        }
    }
}