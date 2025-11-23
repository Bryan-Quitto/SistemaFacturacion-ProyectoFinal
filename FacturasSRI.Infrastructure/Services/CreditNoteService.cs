using FacturasSRI.Application.Dtos;
using FacturasSRI.Core.Models;
using FacturasSRI.Core.Services;
using FacturasSRI.Domain.Entities;
using FacturasSRI.Domain.Enums;
using FacturasSRI.Infrastructure.Persistence;
using FacturasSRI.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

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

        // Semáforo para evitar concurrencia en la generación de números secuenciales
        private static readonly SemaphoreSlim _ncSemaphore = new SemaphoreSlim(1, 1);

        public CreditNoteService(
            FacturasSRIDbContext context,
            ILogger<CreditNoteService> logger,
            IConfiguration configuration,
            XmlGeneratorService xmlGeneratorService,
            IServiceScopeFactory serviceScopeFactory,
            SriApiClientService sriApiClientService,
            SriResponseParserService sriResponseParserService)
        {
            _context = context;
            _logger = logger;
            _configuration = configuration;
            _xmlGeneratorService = xmlGeneratorService;
            _serviceScopeFactory = serviceScopeFactory;
            _sriApiClientService = sriApiClientService;
            _sriResponseParserService = sriResponseParserService;
        }

        // 1. PARA EL LISTADO
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

        // 2. PARA VER DETALLES
        public async Task<CreditNoteDetailViewDto?> GetCreditNoteDetailByIdAsync(Guid id)
        {
            // Intentamos actualizar estado si no es definitivo
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
                ProductName = d.Producto.Nombre,
                Cantidad = d.Cantidad,
                PrecioVentaUnitario = d.PrecioVentaUnitario,
                Subtotal = d.Subtotal
            }).ToList();

            // Cálculo de impuestos para visualización
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

        // 3. CREACIÓN (STOCK ELIMINADO DE AQUÍ)
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
                if (factura.Estado != EstadoFactura.Autorizada) throw new InvalidOperationException("Solo se pueden emitir Notas de Crédito a facturas AUTORIZADAS.");

                // Configuración
                var establishmentCode = _configuration["CompanyInfo:EstablishmentCode"];
                var emissionPointCode = _configuration["CompanyInfo:EmissionPointCode"];
                var rucEmisor = _configuration["CompanyInfo:Ruc"];
                var environmentType = _configuration["CompanyInfo:EnvironmentType"];
                var certPath = _configuration["CompanyInfo:CertificatePath"];
                var certPass = _configuration["CompanyInfo:CertificatePassword"];

                // Secuencial
                var secuencialEntity = await _context.Secuenciales.FirstOrDefaultAsync(s => s.Establecimiento == establishmentCode && s.PuntoEmision == emissionPointCode);
                if (secuencialEntity == null)
                {
                    secuencialEntity = new Secuencial { Id = Guid.NewGuid(), Establecimiento = establishmentCode, PuntoEmision = emissionPointCode, UltimoSecuencialFactura = 0, UltimoSecuencialNotaCredito = 0 };
                    _context.Secuenciales.Add(secuencialEntity);
                }
                secuencialEntity.UltimoSecuencialNotaCredito++;
                string numeroSecuencialStr = secuencialEntity.UltimoSecuencialNotaCredito.ToString("D9");

                // Crear Entidad
                var nc = new NotaDeCredito
                {
                    Id = Guid.NewGuid(),
                    FacturaId = factura.Id,
                    ClienteId = factura.ClienteId.Value,
                    FechaEmision = DateTime.UtcNow,
                    NumeroNotaCredito = numeroSecuencialStr,
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

                    // ¡¡IMPORTANTE!!: AQUÍ YA NO DEVOLVEMOS STOCK. 
                    // El stock solo se devuelve si el SRI Autoriza.
                }

                nc.SubtotalSinImpuestos = subtotalAccum;
                nc.TotalIVA = ivaAccum;
                nc.Total = subtotalAccum + ivaAccum;

                _context.NotasDeCredito.Add(nc);

                // SRI Data must be created for both drafts and regular notes to reserve the sequential number and clave de acceso
                var fechaEcuador = GetEcuadorTime(nc.FechaEmision);
                string claveAcceso = GenerarClaveAcceso(fechaEcuador, "04", rucEmisor, establishmentCode, emissionPointCode, numeroSecuencialStr, environmentType);

                var ncSri = new NotaDeCreditoSRI
                {
                    NotaDeCreditoId = nc.Id,
                    ClaveAcceso = claveAcceso
                };
                _context.NotasDeCreditoSRI.Add(ncSri);

                // If it's not a draft, proceed with XML generation and SRI submission
                if (!dto.EsBorrador)
                {
                    var (xmlGenerado, xmlFirmadoBytes) = _xmlGeneratorService.GenerarYFirmarNotaCredito(claveAcceso, nc, factura.Cliente, factura, certPath, certPass);

                    ncSri.XmlGenerado = xmlGenerado;
                    ncSri.XmlFirmado = Encoding.UTF8.GetString(xmlFirmadoBytes);
                    
                    nc.Estado = EstadoNotaDeCredito.EnviadaSRI;

                    // Save changes before starting background task
                    await _context.SaveChangesAsync();

                    // Launch background process
                    _ = Task.Run(() => EnviarNcAlSriEnFondoAsync(nc.Id, xmlFirmadoBytes));
                }
                else
                {
                    // Just save the draft and its associated SRI entity
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

        // 4. VERIFICACIÓN MANUAL DE ESTADO (Corrección de Stock aquí también)
        public async Task CheckSriStatusAsync(Guid ncId)
        {
            var nc = await _context.NotasDeCredito
                .Include(n => n.InformacionSRI)
                .Include(n => n.Detalles).ThenInclude(d => d.Producto) // Necesario para devolver stock
                .FirstOrDefaultAsync(n => n.Id == ncId);

            if (nc == null || nc.InformacionSRI == null) return;
            
            // Si ya está autorizada o cancelada, no hacemos nada
            if (nc.Estado == EstadoNotaDeCredito.Autorizada || nc.Estado == EstadoNotaDeCredito.Cancelada) return;

            try
            {
                if (string.IsNullOrEmpty(nc.InformacionSRI.ClaveAcceso)) return;

                string respAut = await _sriApiClientService.ConsultarAutorizacionAsync(nc.InformacionSRI.ClaveAcceso);
                var autObj = _sriResponseParserService.ParsearRespuestaAutorizacion(respAut);

                if (autObj.Estado == "AUTORIZADO")
                {
                    // === MOMENTO CRÍTICO: DEVOLVER STOCK ===
                    // Solo si antes NO estaba autorizada (para evitar duplicados)
                    if (nc.Estado != EstadoNotaDeCredito.Autorizada)
                    {
                        await RestaurarStockAsync(_context, nc);
                        await ActualizarCantidadesDevueltasAsync(_context, nc);
                    }

                    nc.Estado = EstadoNotaDeCredito.Autorizada;
                    nc.InformacionSRI.NumeroAutorizacion = autObj.NumeroAutorizacion;
                    nc.InformacionSRI.FechaAutorizacion = autObj.FechaAutorizacion;
                    nc.InformacionSRI.RespuestaSRI = "AUTORIZADO";
                    await _context.SaveChangesAsync();
                }
                else if (autObj.Estado == "NO AUTORIZADO")
                {
                    nc.Estado = EstadoNotaDeCredito.RechazadaSRI;
                    nc.InformacionSRI.RespuestaSRI = JsonSerializer.Serialize(autObj.Errores);
                    await _context.SaveChangesAsync();
                }
                // Si sigue en PROCESAMIENTO, no cambiamos estado
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Error consultando estado SRI para NC {ncId}: {ex.Message}");
            }
        }

        // 5. MÉTODO PRIVADO PARA RESTAURAR STOCK (LÓGICA CENTRALIZADA)
        private async Task RestaurarStockAsync(FacturasSRIDbContext context, NotaDeCredito nc)
        {
            foreach (var detalle in nc.Detalles)
            {
                var producto = await context.Productos.FindAsync(detalle.ProductoId);
                if (producto != null && producto.ManejaInventario)
                {
                    if (producto.ManejaLotes)
                    {
                        // Buscar los consumos de la FACTURA original para saber a qué lotes devolver
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
                            consumo.Lote.CantidadDisponible += remanente; // Simplificación: devolvemos al más reciente disponible
                            remanente = 0;
                        }
                    }
                    else
                    {
                        producto.StockTotal += detalle.Cantidad;
                    }
                }
            }
            _logger.LogInformation($"Stock restaurado para Nota de Crédito {nc.NumeroNotaCredito}");
        }

        private async Task ActualizarCantidadesDevueltasAsync(FacturasSRIDbContext context, NotaDeCredito nc)
        {
            foreach (var detalleNc in nc.Detalles)
            {
                var facturaDetalle = await context.FacturaDetalles
                    .FirstOrDefaultAsync(fd => fd.FacturaId == nc.FacturaId && fd.ProductoId == detalleNc.ProductoId);

                if (facturaDetalle != null)
                {
                    facturaDetalle.CantidadDevuelta += detalleNc.Cantidad;
                }
            }
            await context.SaveChangesAsync();
            _logger.LogInformation($"Cantidades devueltas actualizadas en la factura original para Nota de Crédito {nc.NumeroNotaCredito}");
        }

        // 6. PROCESO DE FONDO
       private async Task EnviarNcAlSriEnFondoAsync(Guid ncId, byte[] xmlFirmadoBytes)
        {
            using (var scope = _serviceScopeFactory.CreateScope())
            {
                var scopedContext = scope.ServiceProvider.GetRequiredService<FacturasSRIDbContext>();
                var scopedSriClient = scope.ServiceProvider.GetRequiredService<SriApiClientService>();
                var scopedParser = scope.ServiceProvider.GetRequiredService<SriResponseParserService>();
                var scopedLogger = scope.ServiceProvider.GetRequiredService<ILogger<CreditNoteService>>();
                var scopedPdf = scope.ServiceProvider.GetRequiredService<PdfGeneratorService>();
                var scopedEmail = scope.ServiceProvider.GetRequiredService<IEmailService>();

                try
                {
                    // 1. RECEPCIÓN
                    string respuestaRecepcionXml = await scopedSriClient.EnviarRecepcionAsync(xmlFirmadoBytes);
                    var respuestaRecepcion = scopedParser.ParsearRespuestaRecepcion(respuestaRecepcionXml);

                    // Recuperar con relaciones necesarias para stock y PDF usando el contexto SCOPED
                    var nc = await scopedContext.NotasDeCredito
                        .Include(n => n.Cliente)
                        .Include(n => n.Factura)
                        .Include(n => n.InformacionSRI) // Importante incluir esto
                        .Include(n => n.Detalles)
                            .ThenInclude(d => d.Producto)
                            .ThenInclude(p => p.ProductoImpuestos)
                            .ThenInclude(pi => pi.Impuesto)
                        .FirstOrDefaultAsync(n => n.Id == ncId);
                    
                    // Obtenemos la entidad SRI ligada a este contexto
                    var ncSri = nc.InformacionSRI; 
                    // O si no vino en el include (aunque debería), la buscamos:
                    if (ncSri == null) ncSri = await scopedContext.NotasDeCreditoSRI.FirstOrDefaultAsync(x => x.NotaDeCreditoId == ncId);

                    if (respuestaRecepcion.Estado == "DEVUELTA")
                    {
                        nc.Estado = EstadoNotaDeCredito.RechazadaSRI;
                        ncSri.RespuestaSRI = JsonSerializer.Serialize(respuestaRecepcion.Errores);
                        // NO SE DEVUELVE STOCK AQUÍ
                    }
                    else
                    {
                        // 2. AUTORIZACIÓN
                        try
                        {
                            await Task.Delay(2500); // Espera prudencial
                            string respAut = await scopedSriClient.ConsultarAutorizacionAsync(ncSri.ClaveAcceso);
                            var autObj = scopedParser.ParsearRespuestaAutorizacion(respAut);

                            if (autObj.Estado == "AUTORIZADO")
                            {
                                // === DEVOLVER STOCK (BACKGROUND) ===
                                await RestaurarStockAsync(scopedContext, nc);
                                await ActualizarCantidadesDevueltasAsync(scopedContext, nc);

                                nc.Estado = EstadoNotaDeCredito.Autorizada;
                                ncSri.NumeroAutorizacion = autObj.NumeroAutorizacion;
                                ncSri.FechaAutorizacion = autObj.FechaAutorizacion;
                                ncSri.RespuestaSRI = "AUTORIZADO";

                                // 3. ENVIAR CORREO
                                try
                                {
                                    // CORRECCIÓN: Construimos el DTO manualmente usando 'nc' y 'scopedContext'
                                    // No podemos llamar a GetCreditNoteDetailByIdAsync porque usa '_context' (disposed)
                                    
                                    var itemsDto = nc.Detalles.Select(d => new CreditNoteItemDetailDto { 
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
                                        ClaveAcceso = ncSri.ClaveAcceso,
                                        NumeroAutorizacion = ncSri.NumeroAutorizacion,
                                        Items = itemsDto,
                                        TaxSummaries = taxSummaries
                                    };
                                    
                                    byte[] pdfBytes = scopedPdf.GenerarNotaCreditoPdf(ncDto);
                                    string xmlSigned = ncSri.XmlFirmado;
                                    
                                    await scopedEmail.SendCreditNoteEmailAsync(nc.Cliente.Email, nc.Cliente.RazonSocial, nc.NumeroNotaCredito, nc.Id, pdfBytes, xmlSigned);
                                    scopedLogger.LogInformation("[BG-NC] Correo enviado correctamente.");
                                }
                                catch(Exception exEmail) { 
                                    scopedLogger.LogError(exEmail, "[BG-NC] Error enviando correo."); 
                                }
                            }
                            else if (autObj.Estado == "NO AUTORIZADO")
                            {
                                nc.Estado = EstadoNotaDeCredito.RechazadaSRI;
                                ncSri.RespuestaSRI = JsonSerializer.Serialize(autObj.Errores);
                            }
                            else 
                            {
                                ncSri.RespuestaSRI = JsonSerializer.Serialize(autObj.Errores);
                            }
                        }
                        catch (Exception exAuth) { 
                            scopedLogger.LogError(exAuth, "[BG-NC] Error Auth"); 
                        }
                    }
                    await scopedContext.SaveChangesAsync();
                }
                catch (Exception ex) { scopedLogger.LogError(ex, "[BG-NC] Error crítico."); }
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
                .Include(n => n.Detalles).ThenInclude(d => d.Producto).ThenInclude(p => p.ProductoImpuestos).ThenInclude(pi => pi.Impuesto)
                .Include(n => n.Factura).ThenInclude(f => f.Detalles)
                .FirstOrDefaultAsync(n => n.Id == dto.Id);

            if (nc == null) throw new InvalidOperationException("La nota de crédito no existe.");
            if (nc.Estado != EstadoNotaDeCredito.Borrador) throw new InvalidOperationException("Solo se pueden modificar borradores.");

            nc.RazonModificacion = dto.RazonModificacion;

            // Remove old items and recalculate totals
            _context.NotaDeCreditoDetalles.RemoveRange(nc.Detalles);
            nc.Detalles.Clear();

            decimal subtotalAccum = 0;
            decimal ivaAccum = 0;

            foreach (var itemDto in dto.Items)
            {
                if (itemDto.CantidadDevolucion <= 0) continue;

                var detalleFactura = nc.Factura.Detalles.FirstOrDefault(d => d.ProductoId == itemDto.ProductoId);
                if (detalleFactura == null) throw new Exception($"Producto ID {itemDto.ProductoId} inválido.");
                
                // We reference the original invoice detail's returned quantity here
                if (itemDto.CantidadDevolucion > (detalleFactura.Cantidad - detalleFactura.CantidadDevuelta)) throw new Exception($"La cantidad a devolver para el producto excede la cantidad disponible en la factura original.");

                decimal precioUnit = detalleFactura.PrecioVentaUnitario;
                decimal subtotalItem = itemDto.CantidadDevolucion * precioUnit;
                decimal valorIvaItem = 0;
                
                var producto = await _context.Productos.Include(p => p.ProductoImpuestos).ThenInclude(pi => pi.Impuesto).FirstAsync(p => p.Id == itemDto.ProductoId);
                var impuestoIva = producto.ProductoImpuestos.FirstOrDefault(pi => pi.Impuesto.Porcentaje > 0);
                if (impuestoIva != null) valorIvaItem = subtotalItem * (impuestoIva.Impuesto.Porcentaje / 100);

                var detalleNc = new NotaDeCreditoDetalle
                {
                    Id = Guid.NewGuid(),
                    NotaDeCreditoId = nc.Id,
                    ProductoId = itemDto.ProductoId,
                    Cantidad = itemDto.CantidadDevolucion,
                    PrecioVentaUnitario = precioUnit,
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

            await _context.SaveChangesAsync();
            _logger.LogInformation("Borrador de Nota de Crédito {Id} actualizado.", nc.Id);

            if (dto.EmitirTrasGuardar)
            {
                _logger.LogInformation("Flag 'EmitirTrasGuardar' detectado para NC. Emisión inmediata...");
                await IssueDraftCreditNoteAsync(nc.Id);
            }

            var resultDto = await GetCreditNoteDetailByIdAsync(nc.Id);
            return new CreditNoteDto { Id = resultDto.Id, NumeroNotaCredito = resultDto.NumeroNotaCredito }; // Simplified return
        }
    }
}