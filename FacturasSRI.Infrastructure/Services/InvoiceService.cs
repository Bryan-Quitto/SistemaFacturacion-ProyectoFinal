using FacturasSRI.Application.Dtos;
using FacturasSRI.Application.Interfaces;
using FacturasSRI.Domain.Entities;
using FacturasSRI.Domain.Enums;
using FacturasSRI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FacturasSRI.Core.Services;
using FacturasSRI.Core.Models;
using System.Text;
using System.Text.Json;
using System.Threading;
using FacturasSRI.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;

namespace FacturasSRI.Infrastructure.Services
{
    public class InvoiceService : IInvoiceService
    {
        private readonly FacturasSRIDbContext _context;
        private readonly ILogger<InvoiceService> _logger;
        private readonly IConfiguration _configuration;
        private readonly FirmaDigitalService _firmaDigitalService;
        private readonly XmlGeneratorService _xmlGeneratorService;
        private readonly SriApiClientService _sriApiClientService;
        private readonly SriResponseParserService _sriResponseParserService;
        private static readonly SemaphoreSlim _invoiceCreationSemaphore = new SemaphoreSlim(1, 1);
        private readonly IEmailService _emailService;
        private readonly PdfGeneratorService _pdfGenerator;
        private readonly IServiceScopeFactory _serviceScopeFactory;

        public InvoiceService(
            FacturasSRIDbContext context,
            ILogger<InvoiceService> logger,
            IConfiguration configuration,
            FirmaDigitalService firmaDigitalService,
            XmlGeneratorService xmlGeneratorService,
            SriApiClientService sriApiClientService,
            SriResponseParserService sriResponseParserService,
            IEmailService emailService,  
            PdfGeneratorService pdfGenerator,
            IServiceScopeFactory serviceScopeFactory
            )
        {
            _context = context;
            _logger = logger;
            _configuration = configuration;
            _firmaDigitalService = firmaDigitalService;
            _xmlGeneratorService = xmlGeneratorService;
            _sriApiClientService = sriApiClientService;
            _sriResponseParserService = sriResponseParserService;
            _emailService = emailService; 
            _pdfGenerator = pdfGenerator; 
            _serviceScopeFactory = serviceScopeFactory;
        }

        private async Task<Cliente> GetOrCreateConsumidorFinalClientAsync()
        {
            var consumidorFinalId = "9999999999999";
            var consumidorFinalClient = await _context.Clientes.FirstOrDefaultAsync(c => c.NumeroIdentificacion == consumidorFinalId);
            if (consumidorFinalClient == null)
            {
                consumidorFinalClient = new Cliente { Id = Guid.NewGuid(), TipoIdentificacion = default, NumeroIdentificacion = consumidorFinalId, RazonSocial = "CONSUMIDOR FINAL", Direccion = "N/A", Email = "consumidorfinal@example.com", Telefono = "N/A", FechaCreacion = DateTime.UtcNow, EstaActivo = true };
                _context.Clientes.Add(consumidorFinalClient);
                await _context.SaveChangesAsync();
            }
            return consumidorFinalClient;
        }

        public async Task<InvoiceDto> CreateInvoiceAsync(CreateInvoiceDto invoiceDto)
        {
            if (invoiceDto.EsConsumidorFinal && invoiceDto.FormaDePago != FormaDePago.Contado)
                throw new InvalidOperationException("Las facturas para Consumidor Final solo pueden ser de Contado.");

            await _invoiceCreationSemaphore.WaitAsync();
            try
            {
                _logger.LogInformation("Iniciando creación de factura...");
                var (factura, cliente) = await CrearFacturaPendienteAsync(invoiceDto);

                if (invoiceDto.EsBorrador)
                {
                    _logger.LogInformation("Factura creada como borrador. No se enviará al SRI. Número: {Numero}", factura.NumeroFactura);
                }
                else
                {
                    var (xmlGenerado, xmlFirmadoBytes, claveAcceso) = await GenerarYFirmarXmlAsync(factura, cliente);
                    var facturaSri = await _context.FacturasSRI.FirstAsync(f => f.FacturaId == factura.Id);
                    facturaSri.XmlGenerado = xmlGenerado;
                    facturaSri.XmlFirmado = Encoding.UTF8.GetString(xmlFirmadoBytes);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation("Factura guardada. Iniciando envío background para {Numero}", factura.NumeroFactura);
                    _ = Task.Run(() => EnviarAlSriEnFondoAsync(factura.Id, xmlFirmadoBytes, claveAcceso));
                }
                return (await GetInvoiceByIdAsync(factura.Id))!;
            }
            catch (Exception ex) { _logger.LogError(ex, "Error CRÍTICO en CreateInvoiceAsync."); throw; }
            finally { _invoiceCreationSemaphore.Release(); }
        }

        private async Task EnviarAlSriEnFondoAsync(Guid facturaId, byte[] xmlFirmadoBytes, string claveAcceso)
        {
            using (var scope = _serviceScopeFactory.CreateScope())
            {
                var scopedContext = scope.ServiceProvider.GetRequiredService<FacturasSRIDbContext>();
                var scopedSriClient = scope.ServiceProvider.GetRequiredService<SriApiClientService>();
                var scopedParser = scope.ServiceProvider.GetRequiredService<SriResponseParserService>();
                var scopedLogger = scope.ServiceProvider.GetRequiredService<ILogger<InvoiceService>>();
                var scopedEmail = scope.ServiceProvider.GetRequiredService<IEmailService>();
                var scopedPdf = scope.ServiceProvider.GetRequiredService<PdfGeneratorService>();
                
                try
                {
                    scopedLogger.LogInformation($"[BG] Enviando Recepción SRI para {facturaId}...");
                    string respuestaRecepcionXml = await scopedSriClient.EnviarRecepcionAsync(xmlFirmadoBytes);
                    
                    var invoice = await scopedContext.Facturas
                        .Include(f => f.Cliente)
                        .Include(f => f.InformacionSRI)
                        .Include(f => f.Detalles).ThenInclude(d => d.Producto).ThenInclude(p => p.ProductoImpuestos).ThenInclude(pi => pi.Impuesto)
                        .FirstOrDefaultAsync(f => f.Id == facturaId);

                    if (invoice == null) return;
                    var facturaSri = invoice.InformacionSRI; 

                    var respuestaRecepcion = scopedParser.ParsearRespuestaRecepcion(respuestaRecepcionXml);

                    // --- INICIO DE LA CORRECCIÓN ---
                    // Verifica si el estado es DEVUELTA y si la razón es "CLAVE ACCESO REGISTRADA"
                    bool esClaveRepetida = respuestaRecepcion.Estado == "DEVUELTA" &&
                                           respuestaRecepcion.Errores.Any(e => e.Identificador == "43");

                    if (respuestaRecepcion.Estado == "DEVUELTA" && !esClaveRepetida)
                    {
                        // Si es DEVUELTA por cualquier otra razón, se considera rechazada.
                        invoice.Estado = EstadoFactura.RechazadaSRI;
                        facturaSri.RespuestaSRI = JsonSerializer.Serialize(respuestaRecepcion.Errores);
                        scopedLogger.LogWarning("[BG] Factura DEVUELTA por SRI (Razón: {Razon})", facturaSri.RespuestaSRI);
                    }
                    else
                    {
                        // Si fue RECIBIDA o si fue DEVUELTA por clave repetida, procedemos a autorizar.
                        if (esClaveRepetida)
                        {
                            scopedLogger.LogInformation("[BG] Clave de acceso ya registrada para {FacturaId}. Procediendo a consultar autorización.", facturaId);
                        }

                        try 
                        {
                            await Task.Delay(2500); 
                            string respuestaAutorizacionXml = await scopedSriClient.ConsultarAutorizacionAsync(claveAcceso);
                            var respuestaAutorizacion = scopedParser.ParsearRespuestaAutorizacion(respuestaAutorizacionXml);

                            if (respuestaAutorizacion.Estado == "AUTORIZADO")
                            {
                                invoice.Estado = EstadoFactura.Autorizada;
                                facturaSri.NumeroAutorizacion = respuestaAutorizacion.NumeroAutorizacion;
                                facturaSri.FechaAutorizacion = respuestaAutorizacion.FechaAutorizacion;
                                facturaSri.RespuestaSRI = "AUTORIZADO";

                                await CrearCxCScoped(scopedContext, invoice);
                                await scopedContext.SaveChangesAsync();

                                try
                                {
                                    var items = invoice.Detalles.Select(d => new InvoiceItemDetailDto
                                    {
                                        ProductoId = d.ProductoId, ProductName = d.Producto.Nombre, Cantidad = d.Cantidad, PrecioVentaUnitario = d.PrecioVentaUnitario, Subtotal = d.Subtotal,
                                        Taxes = d.Producto.ProductoImpuestos.Select(pi => new TaxDto { Nombre = pi.Impuesto.Nombre, Porcentaje = pi.Impuesto.Porcentaje }).ToList()
                                    }).ToList();
                                    var taxSummaries = items.SelectMany(i => i.Taxes.Select(t => new { i.Cantidad, i.PrecioVentaUnitario, t.Nombre, t.Porcentaje }))
                                        .GroupBy(t => new { t.Nombre, t.Porcentaje })
                                        .Select(g => new TaxSummary { TaxName = g.Key.Nombre, TaxRate = g.Key.Porcentaje, Amount = g.Sum(x => x.Cantidad * x.PrecioVentaUnitario * (x.Porcentaje / 100)) }).ToList();

                                    var detailDto = new InvoiceDetailViewDto
                                    {
                                        Id = invoice.Id, NumeroFactura = invoice.NumeroFactura, FechaEmision = invoice.FechaEmision, ClienteNombre = invoice.Cliente.RazonSocial, ClienteIdentificacion = invoice.Cliente.NumeroIdentificacion, ClienteDireccion = invoice.Cliente.Direccion, ClienteEmail = invoice.Cliente.Email, SubtotalSinImpuestos = invoice.SubtotalSinImpuestos, TotalIVA = invoice.TotalIVA, Total = invoice.Total, FormaDePago = invoice.FormaDePago, SaldoPendiente = (invoice.FormaDePago == FormaDePago.Credito ? invoice.Total - invoice.MontoAbonoInicial : 0), ClaveAcceso = facturaSri.ClaveAcceso, NumeroAutorizacion = facturaSri.NumeroAutorizacion,
                                        Items = items, TaxSummaries = taxSummaries
                                    };

                                    byte[] pdfBytes = scopedPdf.GenerarFacturaPdf(detailDto);
                                    string xmlSigned = facturaSri.XmlFirmado;

                                    await scopedEmail.SendInvoiceEmailAsync(invoice.Cliente.Email, invoice.Cliente.RazonSocial, invoice.NumeroFactura, invoice.Id, pdfBytes, xmlSigned);
                                    scopedLogger.LogInformation("[BG] Correo enviado.");
                                }
                                catch (Exception exEmail) { scopedLogger.LogError(exEmail, "[BG] Error correo."); }
                            }
                            else if (respuestaAutorizacion.Estado == "NO AUTORIZADO")
                            {
                                invoice.Estado = EstadoFactura.RechazadaSRI;
                                facturaSri.RespuestaSRI = JsonSerializer.Serialize(respuestaAutorizacion.Errores);
                            }
                            else 
                            {
                                invoice.Estado = EstadoFactura.EnviadaSRI; 
                            }
                        }
                        catch 
                        {
                            invoice.Estado = EstadoFactura.EnviadaSRI;
                        }
                    }
                    // --- FIN DE LA CORRECCIÓN ---

                    await scopedContext.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    scopedLogger.LogWarning("[BG] Error/Timeout enviando al SRI: {Msg}.", ex.Message);
                }
            }
        }

        private async Task CrearCxCScoped(FacturasSRIDbContext context, Factura invoice)
        {
            var cuentaExistente = await context.CuentasPorCobrar.FirstOrDefaultAsync(c => c.FacturaId == invoice.Id);
            if (cuentaExistente != null) return;

            if (invoice.FormaDePago == FormaDePago.Contado)
            {
                var cuentaPorCobrar = new CuentaPorCobrar { FacturaId = invoice.Id, ClienteId = invoice.ClienteId, FechaEmision = invoice.FechaEmision, FechaVencimiento = invoice.FechaEmision, MontoTotal = invoice.Total, SaldoPendiente = 0, Pagada = true, UsuarioIdCreador = invoice.UsuarioIdCreador, FechaCreacion = DateTime.UtcNow };
                context.CuentasPorCobrar.Add(cuentaPorCobrar);
                var cobro = new Cobro { FacturaId = invoice.Id, FechaCobro = DateTime.UtcNow, Monto = invoice.Total, MetodoDePago = "Contado", UsuarioIdCreador = invoice.UsuarioIdCreador, FechaCreacion = DateTime.UtcNow };
                context.Cobros.Add(cobro);
            }
            else 
            {
                decimal saldo = invoice.Total;
                if (invoice.MontoAbonoInicial > 0)
                {
                    var abono = new Cobro { FacturaId = invoice.Id, FechaCobro = DateTime.UtcNow, Monto = invoice.MontoAbonoInicial, MetodoDePago = "Abono Inicial", UsuarioIdCreador = invoice.UsuarioIdCreador, FechaCreacion = DateTime.UtcNow };
                    context.Cobros.Add(abono);
                    saldo -= invoice.MontoAbonoInicial;
                }
                var cxc = new CuentaPorCobrar { FacturaId = invoice.Id, ClienteId = invoice.ClienteId, FechaEmision = invoice.FechaEmision, FechaVencimiento = invoice.FechaEmision.AddDays(invoice.DiasCredito ?? 30), MontoTotal = invoice.Total, SaldoPendiente = saldo, Pagada = saldo <= 0, UsuarioIdCreador = invoice.UsuarioIdCreador, FechaCreacion = DateTime.UtcNow };
                context.CuentasPorCobrar.Add(cxc);
            }
        }

        public async Task<InvoiceDetailViewDto?> CheckSriStatusAsync(Guid invoiceId)
        {
            _logger.LogInformation("--- CheckSriStatusAsync ---");
            var invoice = await _context.Facturas.Include(i => i.InformacionSRI).Include(i => i.Cliente).FirstOrDefaultAsync(i => i.Id == invoiceId);
            if (invoice == null || invoice.InformacionSRI == null) return null;

            if (invoice.Estado == EstadoFactura.Autorizada || invoice.Estado == EstadoFactura.RechazadaSRI) 
                return await GetInvoiceDetailByIdAsync(invoiceId);

            try
            {
                if (invoice.Estado == EstadoFactura.Pendiente)
                {
                    byte[] xmlBytes = Encoding.UTF8.GetBytes(invoice.InformacionSRI.XmlFirmado);
                    string recepXml = await _sriApiClientService.EnviarRecepcionAsync(xmlBytes);
                    var respRecep = _sriResponseParserService.ParsearRespuestaRecepcion(recepXml);
                    
                    bool esClaveRepetida = respRecep.Estado == "DEVUELTA" && 
                                           respRecep.Errores.Any(e => e.Identificador == "43");

                    if(respRecep.Estado == "DEVUELTA" && !esClaveRepetida) {
                        invoice.Estado = EstadoFactura.RechazadaSRI;
                        invoice.InformacionSRI.RespuestaSRI = JsonSerializer.Serialize(respRecep.Errores);
                        await _context.SaveChangesAsync();
                        return await GetInvoiceDetailByIdAsync(invoiceId);
                    }
                    
                    // Si fue RECIBIDA o DEVUELTA con clave repetida, marcamos como Enviada y continuamos a autorizar.
                    invoice.Estado = EstadoFactura.EnviadaSRI;
                    await _context.SaveChangesAsync();
                    await Task.Delay(2000);
                }

                string authXml = await _sriApiClientService.ConsultarAutorizacionAsync(invoice.InformacionSRI.ClaveAcceso);
                var respAuth = _sriResponseParserService.ParsearRespuestaAutorizacion(authXml);
                await ProcesarEstadoAutorizacionAsync(invoice, respAuth, invoiceId);
            }
            catch (Exception ex)
            {
                invoice.InformacionSRI.RespuestaSRI = $"Error SRI: {ex.Message}";
                await _context.SaveChangesAsync();
            }

            return await GetInvoiceDetailByIdAsync(invoiceId);
        }
        
        private async Task ProcesarEstadoAutorizacionAsync(Factura invoice, RespuestaAutorizacion respuestaAutorizacion, Guid invoiceId)
        {
             switch (respuestaAutorizacion.Estado)
            {
                case "AUTORIZADO":
                    invoice.Estado = EstadoFactura.Autorizada;
                    invoice.InformacionSRI.NumeroAutorizacion = respuestaAutorizacion.NumeroAutorizacion;
                    invoice.InformacionSRI.FechaAutorizacion = respuestaAutorizacion.FechaAutorizacion;
                    invoice.InformacionSRI.RespuestaSRI = "AUTORIZADO";

                    await CrearCuentaPorCobrarPostAutorizacionAsync(invoice);
                    await _context.SaveChangesAsync();

                    try 
                    {
                        var invoiceDetailDto = await GetInvoiceDetailByIdAsync(invoiceId);
                        if (invoiceDetailDto != null && !string.IsNullOrEmpty(invoice.Cliente.Email))
                        {
                            byte[] pdfBytes = _pdfGenerator.GenerarFacturaPdf(invoiceDetailDto);
                            string xmlFirmado = invoice.InformacionSRI.XmlFirmado ?? "";

                            await _emailService.SendInvoiceEmailAsync(
                                invoice.Cliente.Email, 
                                invoice.Cliente.RazonSocial, 
                                invoice.NumeroFactura,
                                invoice.Id,
                                pdfBytes, 
                                xmlFirmado
                            );
                        }
                    }
                    catch (Exception emailEx)
                    {
                        _logger.LogError(emailEx, "Error envío correo post-autorización");
                    }
                    break;

                case "NO AUTORIZADO":
                    invoice.Estado = EstadoFactura.RechazadaSRI;
                    invoice.InformacionSRI.RespuestaSRI = JsonSerializer.Serialize(respuestaAutorizacion.Errores);
                    await _context.SaveChangesAsync();
                    break;

                case "PROCESANDO":
                    invoice.InformacionSRI.RespuestaSRI = "El SRI todavía está procesando la autorización.";
                    await _context.SaveChangesAsync();
                    break;
                
                default:
                    if(respuestaAutorizacion.Errores != null && respuestaAutorizacion.Errores.Any())
                    {
                        invoice.InformacionSRI.RespuestaSRI = JsonSerializer.Serialize(respuestaAutorizacion.Errores);
                        await _context.SaveChangesAsync();
                    }
                    break;
            }
        }

        private async Task<(Factura, Cliente)> CrearFacturaPendienteAsync(CreateInvoiceDto invoiceDto)
        {
            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    var establishmentCode = _configuration["CompanyInfo:EstablishmentCode"] ?? throw new InvalidOperationException("Falta configurar 'CompanyInfo:EstablishmentCode'.");
                    var emissionPointCode = _configuration["CompanyInfo:EmissionPointCode"] ?? throw new InvalidOperationException("Falta configurar 'CompanyInfo:EmissionPointCode'.");

                    Cliente cliente;
                    if (invoiceDto.EsConsumidorFinal)
                    {
                        cliente = await GetOrCreateConsumidorFinalClientAsync();
                    }
                    else if (invoiceDto.ClienteId.HasValue)
                    {
                        cliente = await _context.Clientes.FindAsync(invoiceDto.ClienteId.Value) ?? throw new ArgumentException("Cliente no encontrado.");
                    }
                    else
                    {
                        cliente = new Cliente
                        {
                            Id = Guid.NewGuid(),
                            TipoIdentificacion = invoiceDto.TipoIdentificacionComprador ?? throw new ArgumentException("Tipo de identificación del comprador es requerido."),
                            NumeroIdentificacion = invoiceDto.IdentificacionComprador ?? throw new ArgumentException("Número de identificación del comprador es requerido."),
                            RazonSocial = invoiceDto.RazonSocialComprador ?? throw new ArgumentException("Razón social del comprador es requerida."),
                            Direccion = invoiceDto.DireccionComprador ?? "",
                            Email = invoiceDto.EmailComprador ?? "",
                            FechaCreacion = DateTime.UtcNow,
                            EstaActivo = true
                        };
                        _context.Clientes.Add(cliente);
                    }

                    var secuencial = await _context.Secuenciales.FirstOrDefaultAsync(s => s.Establecimiento == establishmentCode && s.PuntoEmision == emissionPointCode);
                    if (secuencial == null)
                    {
                        secuencial = new Secuencial { Id = Guid.NewGuid(), Establecimiento = establishmentCode, PuntoEmision = emissionPointCode, UltimoSecuencialFactura = 0 };
                        _context.Secuenciales.Add(secuencial);
                    }

                    secuencial.UltimoSecuencialFactura++;
                    var numeroSecuencial = secuencial.UltimoSecuencialFactura.ToString("D9");

                    var invoice = new Factura
                    {
                        Id = Guid.NewGuid(),
                        ClienteId = cliente.Id,
                        FechaEmision = DateTime.UtcNow,
                        NumeroFactura = numeroSecuencial,
                        Estado = invoiceDto.EsBorrador ? EstadoFactura.Borrador : EstadoFactura.Pendiente, 
                        UsuarioIdCreador = invoiceDto.UsuarioIdCreador,
                        FechaCreacion = DateTime.UtcNow
                    };
                    
                    decimal subtotalSinImpuestos = 0;
                    decimal totalIva = 0;

                    foreach (var item in invoiceDto.Items)
                    {
                        var producto = await _context.Productos
                            .Include(p => p.ProductoImpuestos).ThenInclude(pi => pi.Impuesto)
                            .SingleAsync(p => p.Id == item.ProductoId);

                        decimal valorIvaItem = 0;
                        var impuestoIva = producto.ProductoImpuestos.FirstOrDefault(pi => pi.Impuesto.Porcentaje > 0);
                        var subtotalItem = item.Cantidad * producto.PrecioVentaUnitario;

                        if (impuestoIva != null)
                        {
                            valorIvaItem = subtotalItem * (impuestoIva.Impuesto.Porcentaje / 100);
                        }

                        var detalle = new FacturaDetalle
                        {
                            Id = Guid.NewGuid(),
                            FacturaId = invoice.Id,
                            ProductoId = item.ProductoId,
                            Producto = producto,
                            Cantidad = item.Cantidad,
                            PrecioVentaUnitario = producto.PrecioVentaUnitario,
                            Subtotal = subtotalItem,
                            ValorIVA = valorIvaItem,
                        };

                        if (producto.ManejaInventario && !invoiceDto.EsBorrador)
                        {
                            if (producto.ManejaLotes)
                            {
                                await DescontarStockDeLotes(detalle);
                            }
                            else
                            {
                                await DescontarStockGeneral(producto, detalle.Cantidad);
                            }
                        }

                        invoice.Detalles.Add(detalle);
                        subtotalSinImpuestos += detalle.Subtotal;
                        totalIva += valorIvaItem;
                    }

                    invoice.SubtotalSinImpuestos = subtotalSinImpuestos;
                    invoice.TotalIVA = totalIva;
                    invoice.Total = subtotalSinImpuestos + totalIva;
                    
                    invoice.FormaDePago = invoiceDto.FormaDePago;
                    invoice.DiasCredito = invoiceDto.DiasCredito;
                    invoice.MontoAbonoInicial = invoiceDto.MontoAbonoInicial;

                    _context.Facturas.Add(invoice);

                    var rucEmisor = _configuration["CompanyInfo:Ruc"] ?? throw new InvalidOperationException("Falta configurar 'CompanyInfo:Ruc'.");
                    var environmentType = _configuration["CompanyInfo:EnvironmentType"] ?? throw new InvalidOperationException("Falta configurar 'CompanyInfo:EnvironmentType'.");
                    var fechaEcuador = GetEcuadorTime(invoice.FechaEmision);
                    var claveAcceso = GenerarClaveAcceso(fechaEcuador, "01", rucEmisor, establishmentCode, emissionPointCode, numeroSecuencial, environmentType);

                    var facturaSri = new FacturaSRI
                    {
                        FacturaId = invoice.Id,
                        ClaveAcceso = claveAcceso
                    };
                    _context.FacturasSRI.Add(facturaSri);

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return (invoice, cliente);
                }
                catch (Exception)
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
        }

        private async Task<(string XmlGenerado, byte[] XmlFirmadoBytes, string ClaveAcceso)> GenerarYFirmarXmlAsync(Factura factura, Cliente cliente)
        {
            var certificatePath = _configuration["CompanyInfo:CertificatePath"] ?? throw new InvalidOperationException("Falta configurar 'CompanyInfo:CertificatePath'.");
            var certificatePassword = _configuration["CompanyInfo:CertificatePassword"] ?? throw new InvalidOperationException("Falta configurar 'CompanyInfo:CertificatePassword'.");

            var facturaSri = await _context.FacturasSRI.FirstAsync(f => f.FacturaId == factura.Id);

            if (string.IsNullOrEmpty(facturaSri.ClaveAcceso))
            {
                throw new InvalidOperationException("La clave de acceso no se ha generado para esta factura.");
            }

            var (xmlGenerado, xmlFirmadoBytes) = _xmlGeneratorService.GenerarYFirmarFactura(facturaSri.ClaveAcceso, factura, cliente, certificatePath, certificatePassword);

            return (xmlGenerado, xmlFirmadoBytes, facturaSri.ClaveAcceso);
        }
        
        private async Task ProcesarRespuestaSriAsync(Guid facturaId, string claveAcceso, string respuestaRecepcionXml)
        {
            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    var invoice = await _context.Facturas.FindAsync(facturaId);
                    if (invoice == null) return;
                    
                    var facturaSri = await _context.FacturasSRI.FirstAsync(f => f.FacturaId == facturaId);

                    RespuestaRecepcion respuestaRecepcion = _sriResponseParserService.ParsearRespuestaRecepcion(respuestaRecepcionXml);

                    if (respuestaRecepcion.Estado == "DEVUELTA")
                    {
                        invoice.Estado = EstadoFactura.RechazadaSRI;
                        facturaSri.RespuestaSRI = JsonSerializer.Serialize(respuestaRecepcion.Errores);
                    }
                    else
                    {
                        invoice.Estado = EstadoFactura.EnviadaSRI; 
                    }

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    try 
                    {
                        var invoice = await _context.Facturas.FindAsync(facturaId);
                        if (invoice != null)
                        {
                            var facturaSri = await _context.FacturasSRI.FirstAsync(f => f.FacturaId == facturaId);
                            facturaSri.RespuestaSRI = $"Error procesando respuesta SRI: {ex.Message}";
                            await _context.SaveChangesAsync();
                        }
                    }
                    catch {}
                }
            }
        }

        private async Task CrearCuentaPorCobrarPostAutorizacionAsync(Factura invoice)
        {
            var cuentaExistente = await _context.CuentasPorCobrar.FirstOrDefaultAsync(c => c.FacturaId == invoice.Id);
            if (cuentaExistente != null) return;

            if (invoice.FormaDePago == FormaDePago.Contado)
            {
                var cuentaPorCobrar = new CuentaPorCobrar
                {
                    FacturaId = invoice.Id,
                    ClienteId = invoice.ClienteId,
                    FechaEmision = invoice.FechaEmision,
                    FechaVencimiento = invoice.FechaEmision,
                    MontoTotal = invoice.Total,
                    SaldoPendiente = 0,
                    Pagada = true,
                    UsuarioIdCreador = invoice.UsuarioIdCreador,
                    FechaCreacion = DateTime.UtcNow
                };
                _context.CuentasPorCobrar.Add(cuentaPorCobrar);

                var cobro = new Cobro
                {
                    FacturaId = invoice.Id,
                    FechaCobro = DateTime.UtcNow,
                    Monto = invoice.Total,
                    MetodoDePago = "Contado",
                    UsuarioIdCreador = invoice.UsuarioIdCreador,
                    FechaCreacion = DateTime.UtcNow
                };
                _context.Cobros.Add(cobro);
            }
            else 
            {
                decimal saldoPendiente = invoice.Total;
                if (invoice.MontoAbonoInicial > 0)
                {
                    var abonoInicial = new Cobro
                    {
                        FacturaId = invoice.Id,
                        FechaCobro = DateTime.UtcNow,
                        Monto = invoice.MontoAbonoInicial,
                        MetodoDePago = "Abono Inicial",
                        UsuarioIdCreador = invoice.UsuarioIdCreador,
                        FechaCreacion = DateTime.UtcNow
                    };
                    _context.Cobros.Add(abonoInicial);
                    saldoPendiente -= invoice.MontoAbonoInicial;
                }

                var cuentaPorCobrar = new CuentaPorCobrar
                {
                    FacturaId = invoice.Id,
                    ClienteId = invoice.ClienteId,
                    FechaEmision = invoice.FechaEmision,
                    FechaVencimiento = invoice.FechaEmision.AddDays(invoice.DiasCredito ?? 30),
                    MontoTotal = invoice.Total,
                    SaldoPendiente = saldoPendiente,
                    Pagada = saldoPendiente <= 0,
                    UsuarioIdCreador = invoice.UsuarioIdCreador,
                    FechaCreacion = DateTime.UtcNow
                };
                _context.CuentasPorCobrar.Add(cuentaPorCobrar);
            }
        }


        private async Task DescontarStockGeneral(Producto producto, int cantidadADescontar)
        {
            if (producto.StockTotal < cantidadADescontar)
            {
                throw new InvalidOperationException($"No hay stock suficiente para '{producto.Nombre}'. Stock disponible: {producto.StockTotal}, se requieren: {cantidadADescontar}.");
            }
            producto.StockTotal -= cantidadADescontar;
        }

        private async Task DescontarStockDeLotes(FacturaDetalle detalle)
        {
            var cantidadADescontar = detalle.Cantidad;
            var lotesDisponibles = await _context.Lotes
                .Where(l => l.ProductoId == detalle.ProductoId && l.CantidadDisponible > 0)
                .OrderBy(l => l.FechaCompra)
                .ToListAsync();

            var stockTotal = lotesDisponibles.Sum(l => l.CantidadDisponible);
            if (stockTotal < cantidadADescontar)
            {
                var producto = await _context.Productos.FindAsync(detalle.ProductoId);
                var nombreProducto = producto?.Nombre ?? $"Producto ID {detalle.ProductoId}";
                throw new InvalidOperationException($"No hay stock suficiente para '{nombreProducto}'. Stock disponible: {stockTotal}, se requieren: {cantidadADescontar}.");
            }

            foreach (var lote in lotesDisponibles)
            {
                if (cantidadADescontar <= 0) break;

                int cantidadConsumida = Math.Min(lote.CantidadDisponible, cantidadADescontar);

                lote.CantidadDisponible -= cantidadConsumida;
                cantidadADescontar -= cantidadConsumida;

                var consumoDeLote = new FacturaDetalleConsumoLote
                {
                    Id = Guid.NewGuid(),
                    FacturaDetalleId = detalle.Id,
                    LoteId = lote.Id,
                    CantidadConsumida = cantidadConsumida
                };
                _context.FacturaDetalleConsumoLotes.Add(consumoDeLote);
            }
        }

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

            if (clave.Length != 48)
            {
                throw new InvalidOperationException($"Error al generar clave. Long: {clave.Length}");
            }

            var digitoVerificador = CalcularDigitoVerificador(clave.ToString());
            clave.Append(digitoVerificador);

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

        public async Task<InvoiceDto?> GetInvoiceByIdAsync(Guid id)
        {
             var invoice = await _context.Facturas
                .Include(i => i.Cliente) // Incluir cliente para el nombre
                .Include(i => i.Detalles).ThenInclude(d => d.Producto)
                .FirstOrDefaultAsync(i => i.Id == id);

            if (invoice == null) return null;

            var cuentaPorCobrar = await _context.CuentasPorCobrar.FirstOrDefaultAsync(c => c.FacturaId == id);

            return new InvoiceDto
            {
                Id = invoice.Id,
                FechaEmision = invoice.FechaEmision,
                NumeroFactura = invoice.NumeroFactura,
                ClienteId = invoice.ClienteId,
                ClienteNombre = invoice.Cliente?.RazonSocial ?? "N/A",
                SubtotalSinImpuestos = invoice.SubtotalSinImpuestos,
                TotalDescuento = invoice.TotalDescuento,
                TotalIVA = invoice.TotalIVA,
                Total = invoice.Total,
                Estado = invoice.Estado,
                FormaDePago = invoice.FormaDePago,
                SaldoPendiente = cuentaPorCobrar?.SaldoPendiente ?? 0,
                Detalles = invoice.Detalles.Select(d => new InvoiceDetailDto
                {
                    Id = d.Id,
                    ProductoId = d.ProductoId,
                    Cantidad = d.Cantidad,
                    PrecioVentaUnitario = d.PrecioVentaUnitario,
                    Descuento = d.Descuento,
                    Subtotal = d.Subtotal
                }).ToList()
            };
        }

        public Task<List<InvoiceDto>> GetInvoicesAsync()
        {
            return (from invoice in _context.Facturas
                        join cpc in _context.CuentasPorCobrar on invoice.Id equals cpc.FacturaId into cpcJoin
                        from cpc in cpcJoin.DefaultIfEmpty()
                        join usuario in _context.Usuarios on invoice.UsuarioIdCreador equals usuario.Id into usuarioJoin
                        from usuario in usuarioJoin.DefaultIfEmpty()
                        join cliente in _context.Clientes on invoice.ClienteId equals cliente.Id into clienteJoin
                        from cliente in clienteJoin.DefaultIfEmpty()
                        orderby invoice.FechaCreacion descending
                        select new InvoiceDto
                        {
                            Id = invoice.Id,
                            FechaEmision = invoice.FechaEmision,
                            NumeroFactura = invoice.NumeroFactura,
                            Estado = invoice.Estado,
                            ClienteId = invoice.ClienteId,
                            ClienteNombre = cliente != null ? cliente.RazonSocial : "Consumidor Final",
                            SubtotalSinImpuestos = invoice.SubtotalSinImpuestos,
                            TotalDescuento = invoice.TotalDescuento,
                            TotalIVA = invoice.TotalIVA,
                            Total = invoice.Total,
                            CreadoPor = usuario != null ? usuario.PrimerNombre + " " + usuario.PrimerApellido : "Usuario no encontrado",
                            FormaDePago = invoice.FormaDePago,
                            SaldoPendiente = cpc != null ? cpc.SaldoPendiente : 0
                        }).ToListAsync();
        }

        public async Task<InvoiceDetailViewDto?> GetInvoiceDetailByIdAsync(Guid id)
        {
            var invoice = await _context.Facturas
                .Include(i => i.Cliente)
                .Include(i => i.InformacionSRI)
                .Include(i => i.Detalles).ThenInclude(d => d.Producto).ThenInclude(p => p.ProductoImpuestos).ThenInclude(pi => pi.Impuesto)
                .Where(i => i.Id == id)
                .FirstOrDefaultAsync();

            if (invoice == null) return null;

            var cuentaPorCobrar = await _context.CuentasPorCobrar.FirstOrDefaultAsync(c => c.FacturaId == invoice.Id);

            var items = invoice.Detalles.Select(d => new InvoiceItemDetailDto
            {
                ProductoId = d.ProductoId,
                ProductName = d.Producto.Nombre,
                Cantidad = d.Cantidad,
                CantidadDevuelta = d.CantidadDevuelta,
                PrecioVentaUnitario = d.PrecioVentaUnitario,
                Subtotal = d.Subtotal,
                Taxes = d.Producto.ProductoImpuestos.Select(pi => new TaxDto
                {
                    Id = pi.Impuesto.Id,
                    Nombre = pi.Impuesto.Nombre,
                    CodigoSRI = pi.Impuesto.CodigoSRI,
                    Porcentaje = pi.Impuesto.Porcentaje,
                    EstaActivo = pi.Impuesto.EstaActivo
                }).ToList()
            }).ToList();

            var taxSummaries = items
                .SelectMany(item => item.Taxes.Select(tax => new {
                    item.Cantidad,
                    item.PrecioVentaUnitario,
                    TaxName = tax.Nombre,
                    TaxRate = tax.Porcentaje
                }))
                .GroupBy(t => new { t.TaxName, t.TaxRate })
                .Select(g => new Application.Dtos.TaxSummary {
                    TaxName = g.Key.TaxName,
                    TaxRate = g.Key.TaxRate,
                    Amount = g.Sum(x => x.Cantidad * x.PrecioVentaUnitario * (x.TaxRate / 100))
                })
                .ToList();

            return new InvoiceDetailViewDto
            {
                Id = invoice.Id,
                NumeroFactura = invoice.NumeroFactura,
                FechaEmision = invoice.FechaEmision,
                ClienteId = invoice.ClienteId.Value,
                ClienteNombre = invoice.Cliente.RazonSocial,
                ClienteIdentificacion = invoice.Cliente.NumeroIdentificacion,
                ClienteDireccion = invoice.Cliente.Direccion,
                ClienteEmail = invoice.Cliente.Email,
                SubtotalSinImpuestos = invoice.SubtotalSinImpuestos,
                TotalIVA = invoice.TotalIVA,
                Total = invoice.Total,
                Items = items,
                TaxSummaries = taxSummaries,
                Estado = invoice.Estado,
                FormaDePago = invoice.FormaDePago,
                DiasCredito = invoice.DiasCredito,
                SaldoPendiente = cuentaPorCobrar?.SaldoPendiente ?? 0,
                ClaveAcceso = invoice.InformacionSRI?.ClaveAcceso,
                NumeroAutorizacion = invoice.InformacionSRI?.NumeroAutorizacion,
                RespuestaSRI = invoice.InformacionSRI?.RespuestaSRI
            };
        }
        
        private DateTime GetEcuadorTime(DateTime utcTime)
        {
            try {
                var tz = TimeZoneInfo.FindSystemTimeZoneById("SA Pacific Standard Time");
                return TimeZoneInfo.ConvertTimeFromUtc(utcTime, tz);
            } catch {
                try {
                    var tz = TimeZoneInfo.FindSystemTimeZoneById("America/Guayaquil");
                    return TimeZoneInfo.ConvertTimeFromUtc(utcTime, tz);
                } catch {
                    return utcTime.AddHours(-5);
                }
            }
        }
        
        public Task ResendInvoiceEmailAsync(Guid invoiceId)
        {
            Task.Run(async () =>
            {
                var invoice = await GetInvoiceDetailByIdAsync(invoiceId);
                var invoiceEntity = await _context.Facturas.Include(i => i.InformacionSRI).FirstOrDefaultAsync(i => i.Id == invoiceId);

                if (invoice == null || invoiceEntity == null) return;
                if (string.IsNullOrEmpty(invoice.ClienteEmail)) return;

                try {
                    var pdfBytes = _pdfGenerator.GenerarFacturaPdf(invoice);
                    var xmlFirmado = invoiceEntity.InformacionSRI?.XmlFirmado ?? "";

                    await _emailService.SendInvoiceEmailAsync(
                        invoice.ClienteEmail,
                        invoice.ClienteNombre,
                        invoice.NumeroFactura,
                        invoice.Id,
                        pdfBytes,
                        xmlFirmado
                    );
                } catch (Exception ex) {
                    _logger.LogError(ex, "Error enviando email background");
                }
            });
            return Task.CompletedTask;
        }

        public async Task CancelInvoiceAsync(Guid invoiceId)
        {
            var invoice = await _context.Facturas.FindAsync(invoiceId);
            if (invoice == null)
            {
                throw new InvalidOperationException("La factura no existe.");
            }

            if (invoice.Estado != EstadoFactura.Borrador)
            {
                throw new InvalidOperationException("Solo se pueden cancelar facturas en estado Borrador.");
            }

            invoice.Estado = EstadoFactura.Cancelada;
            await _context.SaveChangesAsync();
        }

                public async Task<InvoiceDetailViewDto?> IssueDraftInvoiceAsync(Guid invoiceId)

                {

                    var invoice = await _context.Facturas

                        .Include(i => i.Detalles)

                        .ThenInclude(d => d.Producto)

                        .Include(i => i.Cliente)

                        .FirstOrDefaultAsync(i => i.Id == invoiceId);

        

                    if (invoice == null)

                    {

                        throw new InvalidOperationException("La factura no existe.");

                    }

        

                    if (invoice.Estado != EstadoFactura.Borrador)

                    {

                        throw new InvalidOperationException("Solo se pueden emitir facturas que están en estado Borrador.");

                    }

                    

                    _logger.LogInformation("Iniciando emisión de factura borrador ID: {Id}", invoiceId);

        

                    foreach (var detalle in invoice.Detalles)

                    {

                        if (detalle.Producto.ManejaInventario)

                        {

                            if (detalle.Producto.ManejaLotes)

                            {

                                await DescontarStockDeLotes(detalle);

                            }

                            else

                            {

                                await DescontarStockGeneral(detalle.Producto, detalle.Cantidad);

                            }

                        }

                    }

        

                    var (xmlGenerado, xmlFirmadoBytes, claveAcceso) = await GenerarYFirmarXmlAsync(invoice, invoice.Cliente);

        

                    var facturaSri = await _context.FacturasSRI.FirstAsync(f => f.FacturaId == invoice.Id);

                    facturaSri.XmlGenerado = xmlGenerado;

                    facturaSri.XmlFirmado = Encoding.UTF8.GetString(xmlFirmadoBytes);

                    

                    invoice.Estado = EstadoFactura.Pendiente;

        

                    await _context.SaveChangesAsync();

        

                    _logger.LogInformation("Factura borrador actualizada a Pendiente. Iniciando envío background para {Numero}", invoice.NumeroFactura);

                    _ = Task.Run(() => EnviarAlSriEnFondoAsync(invoice.Id, xmlFirmadoBytes, claveAcceso));

        

                    return await GetInvoiceDetailByIdAsync(invoiceId);

                }

        public async Task ReactivateCancelledInvoiceAsync(Guid invoiceId)
        {
            var invoice = await _context.Facturas.FindAsync(invoiceId);
            if (invoice == null)
            {
                throw new InvalidOperationException("La factura no existe.");
            }

            if (invoice.Estado != EstadoFactura.Cancelada)
            {
                throw new InvalidOperationException("Solo se pueden reactivar facturas que están en estado Cancelada.");
            }

            invoice.Estado = EstadoFactura.Borrador;
            await _context.SaveChangesAsync();
        }

        public async Task<InvoiceDto?> UpdateInvoiceAsync(UpdateInvoiceDto invoiceDto)
        {
            if (invoiceDto.EsConsumidorFinal && invoiceDto.FormaDePago != FormaDePago.Contado)
            {
                throw new InvalidOperationException("Las facturas para Consumidor Final solo pueden ser de Contado.");
            }

            var invoice = await _context.Facturas
                .Include(f => f.Detalles)
                .FirstOrDefaultAsync(f => f.Id == invoiceDto.Id);

            if (invoice == null)
            {
                throw new InvalidOperationException("La factura a actualizar no existe.");
            }
            if (invoice.Estado != EstadoFactura.Borrador)
            {
                throw new InvalidOperationException("Solo se pueden modificar facturas en estado Borrador.");
            }

            if (invoiceDto.EsConsumidorFinal)
            {
                invoice.ClienteId = (await GetOrCreateConsumidorFinalClientAsync()).Id;
            }
            else if (invoiceDto.ClienteId.HasValue)
            {
                invoice.ClienteId = invoiceDto.ClienteId.Value;
            }
            else
            {
                throw new InvalidOperationException("Se requiere un ID de cliente para facturas que no son de consumidor final.");
            }
            
            invoice.FormaDePago = invoiceDto.FormaDePago;
            invoice.DiasCredito = invoiceDto.DiasCredito;
            invoice.MontoAbonoInicial = invoiceDto.MontoAbonoInicial;

            _context.FacturaDetalles.RemoveRange(invoice.Detalles);
            invoice.Detalles.Clear();

            decimal subtotalSinImpuestos = 0;
            decimal totalIva = 0;

            foreach (var item in invoiceDto.Items)
            {
                var producto = await _context.Productos
                    .Include(p => p.ProductoImpuestos).ThenInclude(pi => pi.Impuesto)
                    .SingleAsync(p => p.Id == item.ProductoId);

                decimal valorIvaItem = 0;
                var impuestoIva = producto.ProductoImpuestos.FirstOrDefault(pi => pi.Impuesto.Porcentaje > 0);
                var subtotalItem = item.Cantidad * producto.PrecioVentaUnitario;

                if (impuestoIva != null)
                {
                    valorIvaItem = subtotalItem * (impuestoIva.Impuesto.Porcentaje / 100);
                }

                var detalle = new FacturaDetalle
                {
                    FacturaId = invoice.Id,
                    ProductoId = item.ProductoId,
                    Producto = producto,
                    Cantidad = item.Cantidad,
                    PrecioVentaUnitario = producto.PrecioVentaUnitario,
                    Subtotal = subtotalItem,
                    ValorIVA = valorIvaItem,
                };
                invoice.Detalles.Add(detalle);
                subtotalSinImpuestos += detalle.Subtotal;
                totalIva += valorIvaItem;
            }

            invoice.SubtotalSinImpuestos = subtotalSinImpuestos;
            invoice.TotalIVA = totalIva;
            invoice.Total = subtotalSinImpuestos + totalIva;

            await _context.SaveChangesAsync();
            
            _logger.LogInformation("Borrador de factura {Id} actualizado.", invoice.Id);

            if (invoiceDto.EmitirTrasGuardar)
            {
                _logger.LogInformation("Flag 'EmitirTrasGuardar' detectado. Emisión inmediata...");
                await IssueDraftInvoiceAsync(invoice.Id);
            }

            return await GetInvoiceByIdAsync(invoice.Id);
        }
    }
}