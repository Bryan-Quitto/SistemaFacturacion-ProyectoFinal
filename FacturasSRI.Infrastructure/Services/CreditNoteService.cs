using FacturasSRI.Application.Dtos;
using FacturasSRI.Core.Models;
using FacturasSRI.Core.Services;
using FacturasSRI.Domain.Entities;
using FacturasSRI.Domain.Enums;
using FacturasSRI.Infrastructure.Persistence;
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
    public class CreditNoteService
    {
        private readonly FacturasSRIDbContext _context;
        private readonly ILogger<CreditNoteService> _logger;
        private readonly IConfiguration _configuration;
        private readonly XmlGeneratorService _xmlGeneratorService;
        private readonly IServiceScopeFactory _serviceScopeFactory;

        // Semáforo para evitar concurrencia en la generación de números secuenciales
        private static readonly SemaphoreSlim _ncSemaphore = new SemaphoreSlim(1, 1);

        public CreditNoteService(
            FacturasSRIDbContext context,
            ILogger<CreditNoteService> logger,
            IConfiguration configuration,
            XmlGeneratorService xmlGeneratorService,
            IServiceScopeFactory serviceScopeFactory)
        {
            _context = context;
            _logger = logger;
            _configuration = configuration;
            _xmlGeneratorService = xmlGeneratorService;
            _serviceScopeFactory = serviceScopeFactory;
        }

        public async Task<List<CreditNoteListItemDto>> GetCreditNotesAsync()
        {
            var creditNotes = await _context.NotasDeCredito
                .AsNoTracking()
                .Include(nc => nc.Cliente)
                .Include(nc => nc.Factura)
                .OrderByDescending(nc => nc.FechaEmision)
                .Select(nc => new CreditNoteListItemDto
                {
                    Id = nc.Id,
                    NumeroNotaCredito = nc.NumeroNotaCredito,
                    FechaEmision = nc.FechaEmision,
                    ClienteNombre = nc.Cliente.RazonSocial,
                    NumeroFacturaAfectada = nc.Factura.NumeroFactura,
                    Total = nc.Total,
                    Estado = nc.Estado
                })
                .ToListAsync();

            return creditNotes;
        }

        public async Task<NotaDeCredito> CreateCreditNoteAsync(CreateCreditNoteDto dto)
        {
            await _ncSemaphore.WaitAsync();
            try
            {
                // 1. Validar Factura Origen
                var factura = await _context.Facturas
                    .Include(f => f.Cliente)
                    .Include(f => f.Detalles)
                        .ThenInclude(d => d.Producto)
                        .ThenInclude(p => p.ProductoImpuestos)
                        .ThenInclude(pi => pi.Impuesto)
                    .FirstOrDefaultAsync(f => f.Id == dto.FacturaId);

                if (factura == null) 
                    throw new InvalidOperationException("La factura original no existe.");
                
                // Opcional: Validar que esté autorizada
                if (factura.Estado != EstadoFactura.Autorizada) 
                    throw new InvalidOperationException("Solo se pueden emitir Notas de Crédito a facturas AUTORIZADAS por el SRI.");

                // 2. Configuración Básica
                var establishmentCode = _configuration["CompanyInfo:EstablishmentCode"];
                var emissionPointCode = _configuration["CompanyInfo:EmissionPointCode"];
                var rucEmisor = _configuration["CompanyInfo:Ruc"];
                var environmentType = _configuration["CompanyInfo:EnvironmentType"];
                var certPath = _configuration["CompanyInfo:CertificatePath"];
                var certPass = _configuration["CompanyInfo:CertificatePassword"];

                // 3. Obtener y Aumentar Secuencial
                var secuencialEntity = await _context.Secuenciales
                    .FirstOrDefaultAsync(s => s.Establecimiento == establishmentCode && s.PuntoEmision == emissionPointCode);
                
                if (secuencialEntity == null)
                {
                    // Si no existe, crear uno (caso raro si ya facturaste)
                    secuencialEntity = new Secuencial 
                    { 
                        Id = Guid.NewGuid(), 
                        Establecimiento = establishmentCode, 
                        PuntoEmision = emissionPointCode, 
                        UltimoSecuencialFactura = 0,
                        UltimoSecuencialNotaCredito = 0 
                    };
                    _context.Secuenciales.Add(secuencialEntity);
                }

                secuencialEntity.UltimoSecuencialNotaCredito++;
                string numeroSecuencialStr = secuencialEntity.UltimoSecuencialNotaCredito.ToString("D9");

                // 4. Construir la Nota de Crédito
                var nc = new NotaDeCredito
                {
                    Id = Guid.NewGuid(),
                    FacturaId = factura.Id,
                    ClienteId = factura.ClienteId.Value,
                    FechaEmision = DateTime.UtcNow,
                    NumeroNotaCredito = numeroSecuencialStr, // Solo el secuencial (ej: 000000005)
                    Estado = EstadoNotaDeCredito.Pendiente, 
                    RazonModificacion = dto.RazonModificacion,
                    UsuarioIdCreador = dto.UsuarioIdCreador,
                    FechaCreacion = DateTime.UtcNow
                };

                decimal subtotalAccum = 0;
                decimal ivaAccum = 0;

                // 5. Procesar Items y Devolución de Stock
                foreach (var itemDto in dto.Items)
                {
                    if (itemDto.CantidadDevolucion <= 0) continue;

                    var detalleFactura = factura.Detalles.FirstOrDefault(d => d.ProductoId == itemDto.ProductoId);
                    if (detalleFactura == null) 
                        throw new Exception($"El producto ID {itemDto.ProductoId} no pertenece a la factura original.");

                    if (itemDto.CantidadDevolucion > detalleFactura.Cantidad)
                        throw new Exception($"Estás intentando devolver {itemDto.CantidadDevolucion} de {detalleFactura.Producto.Nombre}, pero solo se compraron {detalleFactura.Cantidad}.");

                    // Cálculos económicos
                    decimal precioUnit = detalleFactura.PrecioVentaUnitario;
                    decimal subtotalItem = itemDto.CantidadDevolucion * precioUnit;
                    
                    // Calcular IVA proporcional
                    decimal valorIvaItem = 0;
                    var impuestoIva = detalleFactura.Producto.ProductoImpuestos
                        .FirstOrDefault(pi => pi.Impuesto.Porcentaje > 0);

                    if (impuestoIva != null)
                    {
                        valorIvaItem = subtotalItem * (impuestoIva.Impuesto.Porcentaje / 100);
                    }

                    // Crear Detalle de NC
                    var detalleNc = new NotaDeCreditoDetalle
                    {
                        Id = Guid.NewGuid(),
                        NotaDeCreditoId = nc.Id,
                        ProductoId = itemDto.ProductoId,
                        Producto = detalleFactura.Producto, // Link EF
                        Cantidad = itemDto.CantidadDevolucion,
                        PrecioVentaUnitario = precioUnit,
                        DescuentoAplicado = 0, 
                        Subtotal = subtotalItem,
                        ValorIVA = valorIvaItem
                    };
                    nc.Detalles.Add(detalleNc);

                    subtotalAccum += subtotalItem;
                    ivaAccum += valorIvaItem;

                    // LOGICA DE INVENTARIO
                    if (detalleFactura.Producto.ManejaInventario)
                    {
                        if (detalleFactura.Producto.ManejaLotes)
                        {
                            await DevolverStockALotes(detalleFactura.Id, itemDto.CantidadDevolucion);
                        }
                        else
                        {
                            // Devolución simple
                            detalleFactura.Producto.StockTotal += itemDto.CantidadDevolucion;
                        }
                    }
                }

                // Totales NC
                nc.SubtotalSinImpuestos = subtotalAccum;
                nc.TotalIVA = ivaAccum;
                nc.Total = subtotalAccum + ivaAccum;

                _context.NotasDeCredito.Add(nc);

                // 6. Generar Clave Acceso (Tipo Comprobante "04")
                var fechaEcuador = GetEcuadorTime(nc.FechaEmision);
                string claveAcceso = GenerarClaveAcceso(
                    fechaEcuador, 
                    "04", // 04 = Nota Crédito
                    rucEmisor, 
                    establishmentCode, 
                    emissionPointCode, 
                    numeroSecuencialStr, 
                    environmentType
                );

                var ncSri = new NotaDeCreditoSRI
                {
                    NotaDeCreditoId = nc.Id,
                    ClaveAcceso = claveAcceso
                };
                _context.NotasDeCreditoSRI.Add(ncSri);

                // 7. Generar XML y Firmar
                // NOTA: Aquí pasamos 'factura' porque el XML necesita el número y fecha de la factura original
                var (xmlGenerado, xmlFirmadoBytes) = _xmlGeneratorService.GenerarYFirmarNotaCredito(
                    claveAcceso, 
                    nc, 
                    factura.Cliente, 
                    factura, 
                    certPath, 
                    certPass
                );

                ncSri.XmlGenerado = xmlGenerado;
                ncSri.XmlFirmado = Encoding.UTF8.GetString(xmlFirmadoBytes);
                nc.Estado = EstadoNotaDeCredito.EnviadaSRI;

                await _context.SaveChangesAsync();

                // 8. Enviar al SRI en segundo plano (Fire & Forget)
                _ = Task.Run(() => EnviarNcAlSriEnFondoAsync(nc.Id, xmlFirmadoBytes));

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

        private async Task DevolverStockALotes(Guid detalleFacturaId, int cantidadADevolver)
        {
            // Buscamos qué lotes se consumieron específicamente en esa venta
            var consumos = await _context.FacturaDetalleConsumoLotes
                .Where(c => c.FacturaDetalleId == detalleFacturaId)
                .Include(c => c.Lote)
                .OrderByDescending(c => c.Lote.FechaCaducidad) 
                .ToListAsync();

            int remanente = cantidadADevolver;

            foreach (var consumo in consumos)
            {
                if (remanente <= 0) break;
                
                // Devolvemos al lote disponible
                consumo.Lote.CantidadDisponible += remanente; 
                
                // Nota: Para simplificar, devolvemos todo el remanente al lote más nuevo que encontramos primero
                // (o podrías distribuir según 'consumo.CantidadConsumida')
                remanente = 0; 
            }

            if (remanente > 0)
            {
                 _logger.LogWarning($"Sobraron {remanente} items por devolver y no se encontraron lotes asociados. Se perderán del stock detallado.");
            }
        }

        private async Task EnviarNcAlSriEnFondoAsync(Guid ncId, byte[] xmlFirmadoBytes)
        {
            using (var scope = _serviceScopeFactory.CreateScope())
            {
                var scopedContext = scope.ServiceProvider.GetRequiredService<FacturasSRIDbContext>();
                var scopedSriClient = scope.ServiceProvider.GetRequiredService<SriApiClientService>();
                var scopedParser = scope.ServiceProvider.GetRequiredService<SriResponseParserService>();
                var scopedLogger = scope.ServiceProvider.GetRequiredService<ILogger<CreditNoteService>>();

                try
                {
                    scopedLogger.LogInformation($"[BG-NC] Enviando Nota Credito {ncId} al SRI...");
                    
                    // 1. Enviar Recepción
                    string respuestaRecepcionXml = await scopedSriClient.EnviarRecepcionAsync(xmlFirmadoBytes);
                    var respuestaRecepcion = scopedParser.ParsearRespuestaRecepcion(respuestaRecepcionXml);

                    var nc = await scopedContext.NotasDeCredito.FindAsync(ncId);
                    var ncSri = await scopedContext.NotasDeCreditoSRI.FirstOrDefaultAsync(x => x.NotaDeCreditoId == ncId);

                    if (respuestaRecepcion.Estado == "DEVUELTA")
                    {
                        nc.Estado = EstadoNotaDeCredito.RechazadaSRI;
                        ncSri.RespuestaSRI = JsonSerializer.Serialize(respuestaRecepcion.Errores);
                        scopedLogger.LogWarning("[BG-NC] NC Rechazada por SRI.");
                    }
                    else
                    {
                        // 2. Si fue recibida, intentamos Autorizar
                         try
                        {
                            await Task.Delay(2000); // Esperar al SRI
                            string respAut = await scopedSriClient.ConsultarAutorizacionAsync(ncSri.ClaveAcceso);
                            var autObj = scopedParser.ParsearRespuestaAutorizacion(respAut);

                            if(autObj.Estado == "AUTORIZADO")
                            {
                                nc.Estado = EstadoNotaDeCredito.Autorizada;
                                ncSri.NumeroAutorizacion = autObj.NumeroAutorizacion;
                                ncSri.FechaAutorizacion = autObj.FechaAutorizacion;
                                ncSri.RespuestaSRI = "AUTORIZADO";
                                scopedLogger.LogInformation("[BG-NC] NC Autorizada Exitosamente.");
                            }
                            else
                            {
                                ncSri.RespuestaSRI = JsonSerializer.Serialize(autObj.Errores);
                            }
                        }
                        catch
                        {
                            // Si falla la conexión en autorización, se queda en EnviadaSRI
                            // El usuario podrá reintentar luego si implementas un botón de "Refrescar Estado"
                        }
                    }
                    
                    await scopedContext.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    scopedLogger.LogError(ex, "[BG-NC] Error crítico en envío background.");
                }
            }
        }

        // Helper privado para Clave Acceso (Copia de InvoiceService)
        private string GenerarClaveAcceso(DateTime fechaEmision, string tipoComprobante, string ruc, string establecimiento, string puntoEmision, string secuencial, string tipoAmbiente)
        {
            var fecha = fechaEmision.ToString("ddMMyyyy");
            var tipoEmision = "1"; 
            var codigoNumerico = "12345678"; 
            var clave = new StringBuilder();
            clave.Append(fecha);
            clave.Append(tipoComprobante);
            clave.Append(ruc);
            clave.Append(tipoAmbiente);
            clave.Append(establecimiento);
            clave.Append(puntoEmision);
            clave.Append(secuencial);
            clave.Append(codigoNumerico);
            clave.Append(tipoEmision);

            if (clave.Length != 48) throw new InvalidOperationException($"Error long clave: {clave.Length}");

            clave.Append(CalcularDigitoVerificador(clave.ToString()));
            return clave.ToString();
        }

        private int CalcularDigitoVerificador(string clave)
        {
            var reverso = clave.Reverse().ToArray();
            var suma = 0;
            var factor = 2;
            for (int i = 0; i < reverso.Length; i++)
            {
                suma += (int)char.GetNumericValue(reverso[i]) * factor;
                factor++;
                if (factor > 7) factor = 2;
            }
            int modulo = suma % 11;
            int digito = 11 - modulo;
            if (digito == 11) return 0;
            if (digito == 10) return 1;
            return digito;
        }

        private DateTime GetEcuadorTime(DateTime utcTime)
        {
             try { var tz = TimeZoneInfo.FindSystemTimeZoneById("SA Pacific Standard Time"); return TimeZoneInfo.ConvertTimeFromUtc(utcTime, tz); }
             catch { return utcTime.AddHours(-5); }
        }
    }
}