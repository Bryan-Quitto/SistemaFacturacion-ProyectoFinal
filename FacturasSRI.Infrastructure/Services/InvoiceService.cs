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
    public class InvoiceService : IInvoiceService
    {
        private readonly IDbContextFactory<FacturasSRIDbContext> _contextFactory;
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
        
        private static readonly ConcurrentDictionary<Guid, byte> _processingInvoices = new ConcurrentDictionary<Guid, byte>();

        public InvoiceService(
            IDbContextFactory<FacturasSRIDbContext> contextFactory,
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
            _contextFactory = contextFactory;
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

        private async Task<Cliente> GetOrCreateConsumidorFinalClientAsync(FacturasSRIDbContext context)
        {
            var consumidorFinalId = "9999999999999";
            var consumidorFinalClient = await context.Clientes.FirstOrDefaultAsync(c => c.NumeroIdentificacion == consumidorFinalId);
            if (consumidorFinalClient == null)
            {
                consumidorFinalClient = new Cliente { Id = Guid.NewGuid(), TipoIdentificacion = default, NumeroIdentificacion = consumidorFinalId, RazonSocial = "CONSUMIDOR FINAL", Direccion = "N/A", Email = "consumidorfinal@example.com", Telefono = "N/A", FechaCreacion = DateTime.UtcNow, EstaActivo = true };
                context.Clientes.Add(consumidorFinalClient);
                await context.SaveChangesAsync();
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
                using var context = await _contextFactory.CreateDbContextAsync();
                var (factura, cliente) = await CrearFacturaPendienteAsync(context, invoiceDto);

                if (invoiceDto.EsBorrador)
                {
                    _logger.LogInformation("Factura creada como borrador. No se enviará al SRI. Número: {Numero}", factura.NumeroFactura);
                }
                else
                {
                    var (xmlGenerado, xmlFirmadoBytes, claveAcceso) = await GenerarYFirmarXmlAsync(context, factura, cliente);
                    var facturaSri = await context.FacturasSRI.FirstAsync(f => f.FacturaId == factura.Id);
                    facturaSri.XmlGenerado = xmlGenerado;
                    facturaSri.XmlFirmado = Encoding.UTF8.GetString(xmlFirmadoBytes);
                    await context.SaveChangesAsync();

                    _ = Task.Run(() => EnviarAlSriEnFondoAsync(factura.Id, xmlFirmadoBytes, claveAcceso));
                }
                
                return (await GetInvoiceByIdAsync(factura.Id))!;
            }
            catch (Exception ex) { _logger.LogError(ex, "Error CRÍTICO en CreateInvoiceAsync."); throw; }
            finally { _invoiceCreationSemaphore.Release(); }
        }

        private async Task EnviarAlSriEnFondoAsync(Guid facturaId, byte[] xmlFirmadoBytes, string claveAcceso)
        {
            using var scope = _serviceScopeFactory.CreateScope();
            await using var scopedContext = await scope.ServiceProvider.GetRequiredService<IDbContextFactory<FacturasSRIDbContext>>().CreateDbContextAsync();
            var scopedSriClient = scope.ServiceProvider.GetRequiredService<SriApiClientService>();
            var scopedParser = scope.ServiceProvider.GetRequiredService<SriResponseParserService>();
            var scopedLogger = scope.ServiceProvider.GetRequiredService<ILogger<InvoiceService>>();

            try
            {
                string respuestaRecepcionXml = await scopedSriClient.EnviarRecepcionAsync(xmlFirmadoBytes);
                var respuestaRecepcion = scopedParser.ParsearRespuestaRecepcion(respuestaRecepcionXml);

                bool esClaveRepetida = respuestaRecepcion.Estado == "DEVUELTA" && respuestaRecepcion.Errores.Any(e => e.Identificador == "43");

                if (respuestaRecepcion.Estado == "DEVUELTA" && !esClaveRepetida)
                {
                    var invoice = await scopedContext.Facturas.Include(f => f.InformacionSRI).FirstOrDefaultAsync(f => f.Id == facturaId);
                    if (invoice != null && invoice.InformacionSRI != null)
                    {
                        invoice.Estado = EstadoFactura.RechazadaSRI;
                        invoice.InformacionSRI.RespuestaSRI = JsonSerializer.Serialize(respuestaRecepcion.Errores);
                        await scopedContext.SaveChangesAsync();
                    }
                    return;
                } 
                else 
                {
                     var invoice = await scopedContext.Facturas.FindAsync(facturaId);
                     if(invoice != null && invoice.Estado == EstadoFactura.Pendiente) {
                         invoice.Estado = EstadoFactura.EnviadaSRI;
                         await scopedContext.SaveChangesAsync();
                     }
                }
                
                RespuestaAutorizacion? respuestaAutorizacion = null;
                for (int i = 0; i < 4; i++)
                {
                    await Task.Delay(new[] { 2500, 5000, 10000, 15000 }[i]);
                    string respuestaAutorizacionXml = await scopedSriClient.ConsultarAutorizacionAsync(claveAcceso);
                    respuestaAutorizacion = scopedParser.ParsearRespuestaAutorizacion(respuestaAutorizacionXml);
                    
                    if (respuestaAutorizacion.Estado != "PROCESANDO") break;
                }
                
                if (respuestaAutorizacion != null)
                {
                    await FinalizeAuthorizationAsync(facturaId, respuestaAutorizacion);
                }
            }
            catch (Exception ex)
            {
                scopedLogger.LogError(ex, "[BG] Error crítico en EnviarAlSriEnFondoAsync para factura {FacturaId}", facturaId);
            }
        }

        public async Task<InvoiceDetailViewDto?> CheckSriStatusAsync(Guid invoiceId)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            var invoiceState = await context.Facturas
                .AsNoTracking()
                .Include(f => f.InformacionSRI)
                .FirstOrDefaultAsync(i => i.Id == invoiceId);

            if (invoiceState == null || invoiceState.InformacionSRI == null) return null;

            if (invoiceState.Estado == EstadoFactura.Autorizada || 
                invoiceState.Estado == EstadoFactura.RechazadaSRI || 
                invoiceState.Estado == EstadoFactura.Cancelada)
            {
                return await GetInvoiceDetailByIdAsync(invoiceId);
            }

            try
            {
                bool forzarRecepcion = invoiceState.Estado == EstadoFactura.Pendiente;

                if (!forzarRecepcion)
                {
                    string authXml = await _sriApiClientService.ConsultarAutorizacionAsync(invoiceState.InformacionSRI.ClaveAcceso!);
                    var respAuth = _sriResponseParserService.ParsearRespuestaAutorizacion(authXml);

                    if (respAuth.Estado != "PROCESANDO")
                    {
                        await FinalizeAuthorizationAsync(invoiceId, respAuth);
                        return await GetInvoiceDetailByIdAsync(invoiceId);
                    }
                    else
                    {
                        forzarRecepcion = true; 
                    }
                }

                if (forzarRecepcion)
                {
                    if (string.IsNullOrEmpty(invoiceState.InformacionSRI.XmlFirmado)) 
                    {
                         _logger.LogWarning("XML Firmado es nulo para factura {Id}", invoiceId);
                         return await GetInvoiceDetailByIdAsync(invoiceId);
                    }

                    byte[] xmlBytes = Encoding.UTF8.GetBytes(invoiceState.InformacionSRI.XmlFirmado);
                    string recepXml = await _sriApiClientService.EnviarRecepcionAsync(xmlBytes);
                    var respRecep = _sriResponseParserService.ParsearRespuestaRecepcion(recepXml);
                    
                    if(respRecep.Estado == "DEVUELTA") {
                         bool esErrorDuplicado = respRecep.Errores.Any(e => e.Identificador == "43");

                        if (!esErrorDuplicado) // Solo si NO es duplicado, marcamos como rechazada
                        {
                            var invoiceToUpdate = await context.Facturas.Include(f => f.InformacionSRI).FirstAsync(f => f.Id == invoiceId);
                            invoiceToUpdate.Estado = EstadoFactura.RechazadaSRI;
                            if (invoiceToUpdate.InformacionSRI != null) 
                            {
                                invoiceToUpdate.InformacionSRI.RespuestaSRI = JsonSerializer.Serialize(respRecep.Errores);
                            }
                            await context.SaveChangesAsync();
                            return await GetInvoiceDetailByIdAsync(invoiceId);
                        }
                        else 
                        {
                            if(invoiceState.Estado != EstadoFactura.EnviadaSRI)
                            {
                                var invoiceToUpdate = await context.Facturas.FirstAsync(f => f.Id == invoiceId);
                                invoiceToUpdate.Estado = EstadoFactura.EnviadaSRI;
                                await context.SaveChangesAsync();
                            }
                        }
                    }
                    
                    if(invoiceState.Estado != EstadoFactura.EnviadaSRI)
                    {
                        var invoiceToUpdate = await context.Facturas.FirstAsync(f => f.Id == invoiceId);
                        invoiceToUpdate.Estado = EstadoFactura.EnviadaSRI;
                        await context.SaveChangesAsync();
                    }
                    
                    await Task.Delay(2000);
                    
                    if (!string.IsNullOrEmpty(invoiceState.InformacionSRI.ClaveAcceso))
                    {
                        string authXml = await _sriApiClientService.ConsultarAutorizacionAsync(invoiceState.InformacionSRI.ClaveAcceso);
                        var respAuth = _sriResponseParserService.ParsearRespuestaAutorizacion(authXml);
                        
                        if(respAuth.Estado != "PROCESANDO")
                        {
                            await FinalizeAuthorizationAsync(invoiceId, respAuth);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en CheckSriStatusAsync para factura {InvoiceId}", invoiceId);
            }
            try 
            {
                return await GetInvoiceDetailByIdAsync(invoiceId);
            }
            catch (ObjectDisposedException)
            {
                _logger.LogWarning("Intento de leer factura {Id} con contexto eliminado (usuario navegó).", invoiceId);
                return null; 
            }
        }

        private async Task FinalizeAuthorizationAsync(Guid invoiceId, RespuestaAutorizacion respuesta)
        {
            if (!_processingInvoices.TryAdd(invoiceId, 0)) return;

            try
            {
                using var scope = _serviceScopeFactory.CreateScope();
                await using var context = await scope.ServiceProvider.GetRequiredService<IDbContextFactory<FacturasSRIDbContext>>().CreateDbContextAsync();
                
                var invoice = await context.Facturas
                    .Include(i => i.InformacionSRI)
                    .Include(i => i.Cliente)
                    .Include(i => i.Detalles).ThenInclude(d => d.Producto).ThenInclude(p => p.ProductoImpuestos).ThenInclude(pi => pi.Impuesto)
                    .FirstOrDefaultAsync(i => i.Id == invoiceId);

                if (invoice == null || invoice.Estado == EstadoFactura.Autorizada || invoice.InformacionSRI == null || invoice.Cliente == null) return;

                switch (respuesta.Estado)
                {
                    case "AUTORIZADO":
                        invoice.Estado = EstadoFactura.Autorizada;
                        invoice.InformacionSRI.NumeroAutorizacion = respuesta.NumeroAutorizacion;
                        invoice.InformacionSRI.FechaAutorizacion = respuesta.FechaAutorizacion;
                        invoice.InformacionSRI.RespuestaSRI = "AUTORIZADO";

                        await CrearCxCScoped(context, invoice);
                        await context.SaveChangesAsync();
                        
                        try
                        {
                            var items = invoice.Detalles.Select(d => new InvoiceItemDetailDto
                            {
                                ProductoId = d.ProductoId, 
                                ProductCode = d.Producto?.CodigoPrincipal ?? "",
                                ProductName = d.Producto?.Nombre ?? "Producto Desconocido", 
                                Cantidad = d.Cantidad, 
                                PrecioVentaUnitario = d.PrecioVentaUnitario, 
                                Subtotal = d.Subtotal,
                                Taxes = d.Producto?.ProductoImpuestos.Select(pi => new TaxDto { Nombre = pi.Impuesto?.Nombre ?? "", Porcentaje = pi.Impuesto?.Porcentaje ?? 0 }).ToList() ?? new List<TaxDto>()
                            }).ToList();
                            var taxSummaries = items.SelectMany(i => i.Taxes.Select(t => new { i.Cantidad, i.PrecioVentaUnitario, t.Nombre, t.Porcentaje }))
                                .GroupBy(t => new { t.Nombre, t.Porcentaje })
                                .Select(g => new TaxSummary { TaxName = g.Key.Nombre, TaxRate = g.Key.Porcentaje, Amount = g.Sum(x => x.Cantidad * x.PrecioVentaUnitario * (x.Porcentaje / 100)) }).ToList();

                            DateTime? fechaVencimiento = null;
                            if (invoice.FormaDePago == FormaDePago.Credito)
                            {
                                fechaVencimiento = invoice.FechaEmision.AddDays(invoice.DiasCredito ?? 0);
                            }

                            var detailDto = new InvoiceDetailViewDto
                            {
                                Id = invoice.Id, NumeroFactura = invoice.NumeroFactura, FechaEmision = invoice.FechaEmision, ClienteNombre = invoice.Cliente.RazonSocial, ClienteIdentificacion = invoice.Cliente.NumeroIdentificacion, ClienteDireccion = invoice.Cliente.Direccion, ClienteEmail = invoice.Cliente.Email, SubtotalSinImpuestos = invoice.SubtotalSinImpuestos, TotalIVA = invoice.TotalIVA, Total = invoice.Total, FormaDePago = invoice.FormaDePago, SaldoPendiente = (invoice.FormaDePago == FormaDePago.Credito ? invoice.Total - invoice.MontoAbonoInicial : 0), ClaveAcceso = invoice.InformacionSRI.ClaveAcceso, NumeroAutorizacion = invoice.InformacionSRI.NumeroAutorizacion,
                                Items = items, TaxSummaries = taxSummaries,
                                DiasCredito = invoice.DiasCredito,
                                FechaVencimiento = fechaVencimiento
                            };

                            var pdfGenerator = scope.ServiceProvider.GetRequiredService<PdfGeneratorService>();
                            var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
                            
                            byte[] pdfBytes = pdfGenerator.GenerarFacturaPdf(detailDto);
                            string xmlFirmado = invoice.InformacionSRI.XmlFirmado ?? "";

                            if (!string.IsNullOrEmpty(invoice.Cliente.Email))
                            {
                                await emailService.SendInvoiceEmailAsync(invoice.Cliente.Email, invoice.Cliente.RazonSocial, invoice.NumeroFactura, invoice.Id, pdfBytes, xmlFirmado);
                            }
                        }
                        catch (Exception emailEx)
                        {
                            _logger.LogError(emailEx, "Error enviando correo post-autorización para {InvoiceId}", invoiceId);
                        }
                        break;

                    case "NO AUTORIZADO":
                        invoice.Estado = EstadoFactura.RechazadaSRI;
                        invoice.InformacionSRI.RespuestaSRI = JsonSerializer.Serialize(respuesta.Errores);
                        await context.SaveChangesAsync();
                        break;
                    
                    default:
                         if(respuesta.Errores != null && respuesta.Errores.Any())
                        {
                            invoice.InformacionSRI.RespuestaSRI = JsonSerializer.Serialize(respuesta.Errores);
                            await context.SaveChangesAsync();
                        }
                        break;
                }
            }
            finally
            {
                _processingInvoices.TryRemove(invoiceId, out _);
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
                
                var fechaVencimiento = invoice.FechaEmision.AddDays(invoice.DiasCredito ?? 0);

                var cxc = new CuentaPorCobrar { FacturaId = invoice.Id, ClienteId = invoice.ClienteId, FechaEmision = invoice.FechaEmision, FechaVencimiento = fechaVencimiento, MontoTotal = invoice.Total, SaldoPendiente = saldo, Pagada = saldo <= 0, UsuarioIdCreador = invoice.UsuarioIdCreador, FechaCreacion = DateTime.UtcNow };
                context.CuentasPorCobrar.Add(cxc);
            }
        }

        private async Task<(Factura, Cliente)> CrearFacturaPendienteAsync(FacturasSRIDbContext context, CreateInvoiceDto invoiceDto)
        {
            using (var transaction = await context.Database.BeginTransactionAsync())
            {
                try
                {
                    var establishmentCode = _configuration["CompanyInfo:EstablishmentCode"] ?? throw new InvalidOperationException("Falta configurar 'CompanyInfo:EstablishmentCode'.");
                    var emissionPointCode = _configuration["CompanyInfo:EmissionPointCode"] ?? throw new InvalidOperationException("Falta configurar 'CompanyInfo:EmissionPointCode'.");

                    Cliente? cliente;
                    if (invoiceDto.EsConsumidorFinal)
                    {
                        cliente = await GetOrCreateConsumidorFinalClientAsync(context);
                    }
                    else if (invoiceDto.ClienteId.HasValue)
                    {
                        cliente = await context.Clientes.FindAsync(invoiceDto.ClienteId.Value);
                        if (cliente == null) throw new ArgumentException("Cliente no encontrado.");
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
                        context.Clientes.Add(cliente);
                    }

                    var secuencial = await context.Secuenciales.FirstOrDefaultAsync(s => s.Establecimiento == establishmentCode && s.PuntoEmision == emissionPointCode);
                    if (secuencial == null)
                    {
                        secuencial = new Secuencial { Id = Guid.NewGuid(), Establecimiento = establishmentCode, PuntoEmision = emissionPointCode, UltimoSecuencialFactura = 0 };
                        context.Secuenciales.Add(secuencial);
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
                        var producto = await context.Productos
                            .Include(p => p.ProductoImpuestos).ThenInclude(pi => pi.Impuesto)
                            .SingleAsync(p => p.Id == item.ProductoId);

                        decimal valorIvaItem = 0;
                        var impuestoIva = producto.ProductoImpuestos.FirstOrDefault(pi => pi.Impuesto != null && pi.Impuesto.Porcentaje > 0);
                        var subtotalItem = item.Cantidad * producto.PrecioVentaUnitario;

                        if (impuestoIva != null && impuestoIva.Impuesto != null)
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
                                await DescontarStockDeLotes(context, detalle);
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

                    context.Facturas.Add(invoice);

                    var rucEmisor = _configuration["CompanyInfo:Ruc"] ?? throw new InvalidOperationException("Falta configurar 'CompanyInfo:Ruc'.");
                    var environmentType = _configuration["CompanyInfo:EnvironmentType"] ?? throw new InvalidOperationException("Falta configurar 'CompanyInfo:EnvironmentType'.");
                    var fechaEcuador = GetEcuadorTime(invoice.FechaEmision);
                    var claveAcceso = GenerarClaveAcceso(fechaEcuador, "01", rucEmisor, establishmentCode, emissionPointCode, numeroSecuencial, environmentType);

                    var facturaSri = new FacturaSRI
                    {
                        FacturaId = invoice.Id,
                        ClaveAcceso = claveAcceso
                    };
                    context.FacturasSRI.Add(facturaSri);

                    await context.SaveChangesAsync();
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

        private async Task<(string XmlGenerado, byte[] XmlFirmadoBytes, string ClaveAcceso)> GenerarYFirmarXmlAsync(FacturasSRIDbContext context, Factura factura, Cliente cliente)
        {
            var certificatePath = _configuration["CompanyInfo:CertificatePath"] ?? throw new InvalidOperationException("Falta configurar 'CompanyInfo:CertificatePath'.");
            var certificatePassword = _configuration["CompanyInfo:CertificatePassword"] ?? throw new InvalidOperationException("Falta configurar 'CompanyInfo:CertificatePassword'.");

            var facturaSri = await context.FacturasSRI.FirstAsync(f => f.FacturaId == factura.Id);

            if (string.IsNullOrEmpty(facturaSri.ClaveAcceso))
            {
                throw new InvalidOperationException("La clave de acceso no se ha generado para esta factura.");
            }

            var (xmlGenerado, xmlFirmadoBytes) = _xmlGeneratorService.GenerarYFirmarFactura(facturaSri.ClaveAcceso, factura, cliente, certificatePath, certificatePassword);

            return (xmlGenerado, xmlFirmadoBytes, facturaSri.ClaveAcceso);
        }

        private Task DescontarStockGeneral(Producto producto, int cantidadADescontar)
        {
            if (producto.StockTotal < cantidadADescontar)
            {
                throw new InvalidOperationException($"No hay stock suficiente para '{producto.Nombre}'. Stock disponible: {producto.StockTotal}, se requieren: {cantidadADescontar}.");
            }
            producto.StockTotal -= cantidadADescontar;
            return Task.CompletedTask;
        }

        private async Task DescontarStockDeLotes(FacturasSRIDbContext context, FacturaDetalle detalle)
        {
            var cantidadADescontar = detalle.Cantidad;
            var lotesDisponibles = await context.Lotes
                .Where(l => l.ProductoId == detalle.ProductoId && l.CantidadDisponible > 0)
                .OrderBy(l => l.FechaCompra)
                .ToListAsync();

            var stockTotal = lotesDisponibles.Sum(l => l.CantidadDisponible);
            if (stockTotal < cantidadADescontar)
            {
                var producto = await context.Productos.FindAsync(detalle.ProductoId);
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
                context.FacturaDetalleConsumoLotes.Add(consumoDeLote);
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
             await using var context = await _contextFactory.CreateDbContextAsync();
             var invoice = await context.Facturas
                .AsNoTracking()
                .Include(i => i.Cliente) 
                .Include(i => i.Detalles).ThenInclude(d => d.Producto)
                .FirstOrDefaultAsync(i => i.Id == id);

            if (invoice == null) return null;

            var cuentaPorCobrar = await context.CuentasPorCobrar.AsNoTracking().FirstOrDefaultAsync(c => c.FacturaId == id);

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
                DiasCredito = invoice.DiasCredito,
                MontoAbonoInicial = invoice.MontoAbonoInicial,
                SaldoPendiente = cuentaPorCobrar?.SaldoPendiente ?? 0,
                FechaVencimiento = cuentaPorCobrar?.FechaVencimiento,
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

        public async Task<PaginatedList<InvoiceDto>> GetInvoicesAsync(int pageNumber, int pageSize, string? searchTerm, EstadoFactura? status, FormaDePago? formaDePago, string? paymentStatus)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            var query = from invoice in context.Facturas.AsNoTracking()
                        join cpc in context.CuentasPorCobrar on invoice.Id equals cpc.FacturaId into cpcJoin
                        from cpc in cpcJoin.DefaultIfEmpty()
                        join usuario in context.Usuarios on invoice.UsuarioIdCreador equals usuario.Id into usuarioJoin
                        from usuario in usuarioJoin.DefaultIfEmpty()
                        join cliente in context.Clientes on invoice.ClienteId equals cliente.Id into clienteJoin
                        from cliente in clienteJoin.DefaultIfEmpty()
                        select new { invoice, cpc, usuario, cliente };

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                query = query.Where(x => x.invoice.NumeroFactura.Contains(searchTerm) || (x.cliente != null && x.cliente.RazonSocial.Contains(searchTerm)));
            }

            if (status.HasValue)
            {
                query = query.Where(x => x.invoice.Estado == status.Value);
            }

            if (formaDePago.HasValue)
            {
                query = query.Where(x => x.invoice.FormaDePago == formaDePago.Value);
            }

            if (!string.IsNullOrEmpty(paymentStatus) && paymentStatus != "All")
            {
                if (paymentStatus == "Pending")
                {
                    query = query.Where(x => x.cpc != null && x.cpc.SaldoPendiente > 0);
                }
                else if (paymentStatus == "Paid")
                {
                    query = query.Where(x => x.cpc == null || x.cpc.SaldoPendiente <= 0);
                }
            }

            var finalQuery = query
                .OrderByDescending(x => x.invoice.FechaCreacion)
                .Select(x => new InvoiceDto
                {
                    Id = x.invoice.Id,
                    FechaEmision = x.invoice.FechaEmision,
                    NumeroFactura = x.invoice.NumeroFactura,
                    Estado = x.invoice.Estado,
                    ClienteId = x.invoice.ClienteId,
                    ClienteNombre = x.cliente != null ? x.cliente.RazonSocial : "Consumidor Final",
                    SubtotalSinImpuestos = x.invoice.SubtotalSinImpuestos,
                    TotalDescuento = x.invoice.TotalDescuento,
                    TotalIVA = x.invoice.TotalIVA,
                    Total = x.invoice.Total,
                    CreadoPor = x.usuario != null ? x.usuario.PrimerNombre + " " + x.usuario.PrimerApellido : "Usuario no encontrado",
                    FormaDePago = x.invoice.FormaDePago,
                    DiasCredito = x.invoice.DiasCredito,
                    MontoAbonoInicial = x.invoice.MontoAbonoInicial,
                    SaldoPendiente = x.cpc != null ? x.cpc.SaldoPendiente : 0,
                    FechaVencimiento = x.cpc != null ? x.cpc.FechaVencimiento : (DateTime?)null
                });
            
            return await PaginatedList<InvoiceDto>.CreateAsync(finalQuery, pageNumber, pageSize);
        }

        public async Task<InvoiceDetailViewDto?> GetInvoiceDetailByIdAsync(Guid id)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            var invoice = await context.Facturas
                .AsNoTracking()
                .Include(i => i.Cliente)
                .Include(i => i.InformacionSRI)
                .Include(i => i.Detalles).ThenInclude(d => d.Producto).ThenInclude(p => p.ProductoImpuestos).ThenInclude(pi => pi.Impuesto)
                .Where(i => i.Id == id)
                .FirstOrDefaultAsync();

            if (invoice == null) return null;

            var cuentaPorCobrar = await context.CuentasPorCobrar.AsNoTracking().FirstOrDefaultAsync(c => c.FacturaId == invoice.Id);

            var items = invoice.Detalles.Select(d => new InvoiceItemDetailDto
            {
                ProductoId = d.ProductoId,
                ProductCode = d.Producto?.CodigoPrincipal ?? "",
                ProductName = d.Producto?.Nombre ?? "N/A",
                Cantidad = d.Cantidad,
                CantidadDevuelta = d.CantidadDevuelta,
                PrecioVentaUnitario = d.PrecioVentaUnitario,
                Subtotal = d.Subtotal,
                Taxes = d.Producto?.ProductoImpuestos.Select(pi => new TaxDto
                {
                    Id = pi.Impuesto?.Id ?? Guid.Empty,
                    Nombre = pi.Impuesto?.Nombre ?? "N/A",
                    CodigoSRI = pi.Impuesto?.CodigoSRI ?? "",
                    Porcentaje = pi.Impuesto?.Porcentaje ?? 0,
                    EstaActivo = pi.Impuesto?.EstaActivo ?? false
                }).ToList() ?? new List<TaxDto>()
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
                ClienteId = invoice.ClienteId ?? Guid.Empty,
                ClienteNombre = invoice.Cliente?.RazonSocial ?? "N/A",
                ClienteIdentificacion = invoice.Cliente?.NumeroIdentificacion ?? "N/A",
                ClienteDireccion = invoice.Cliente?.Direccion ?? "",
                ClienteEmail = invoice.Cliente?.Email ?? "",
                SubtotalSinImpuestos = invoice.SubtotalSinImpuestos,
                TotalIVA = invoice.TotalIVA,
                Total = invoice.Total,
                Items = items,
                TaxSummaries = taxSummaries,
                Estado = invoice.Estado,
                FormaDePago = invoice.FormaDePago,
                DiasCredito = invoice.DiasCredito,
                MontoAbonoInicial = invoice.MontoAbonoInicial,
                SaldoPendiente = cuentaPorCobrar?.SaldoPendiente ?? 0,
                FechaVencimiento = cuentaPorCobrar?.FechaVencimiento,
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
                using var scope = _serviceScopeFactory.CreateScope();
                var scopedService = scope.ServiceProvider.GetRequiredService<IInvoiceService>();
                var scopedEmail = scope.ServiceProvider.GetRequiredService<IEmailService>();
                var scopedPdf = scope.ServiceProvider.GetRequiredService<PdfGeneratorService>();
                await using var scopedContext = await scope.ServiceProvider.GetRequiredService<IDbContextFactory<FacturasSRIDbContext>>().CreateDbContextAsync();

                var invoice = await scopedService.GetInvoiceDetailByIdAsync(invoiceId);
                var invoiceEntity = await scopedContext.Facturas.Include(i => i.InformacionSRI).AsNoTracking().FirstOrDefaultAsync(i => i.Id == invoiceId);

                if (invoice == null || invoiceEntity == null) return;
                if (string.IsNullOrEmpty(invoice.ClienteEmail)) return;

                try {
                    var pdfBytes = scopedPdf.GenerarFacturaPdf(invoice);
                    var xmlFirmado = invoiceEntity.InformacionSRI?.XmlFirmado ?? "";

                    await scopedEmail.SendInvoiceEmailAsync(
                        invoice.ClienteEmail,
                        invoice.ClienteNombre,
                        invoice.NumeroFactura,
                        invoice.Id,
                        pdfBytes,
                        xmlFirmado
                    );
                } catch (Exception) {
                    
                }
            });
            return Task.CompletedTask;
        }

        public async Task CancelInvoiceAsync(Guid invoiceId)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            var invoice = await context.Facturas.FindAsync(invoiceId);
            if (invoice == null)
            {
                throw new InvalidOperationException("La factura no existe.");
            }

            if (invoice.Estado != EstadoFactura.Borrador)
            {
                throw new InvalidOperationException("Solo se pueden cancelar facturas en estado Borrador.");
            }

            invoice.Estado = EstadoFactura.Cancelada;
            await context.SaveChangesAsync();
        }

        public async Task<InvoiceDetailViewDto?> IssueDraftInvoiceAsync(Guid invoiceId)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            var invoice = await context.Facturas
                .Include(i => i.Detalles)
                    .ThenInclude(d => d.Producto)
                        .ThenInclude(p => p.ProductoImpuestos)
                            .ThenInclude(pi => pi.Impuesto)
                .Include(i => i.Cliente)
                .FirstOrDefaultAsync(i => i.Id == invoiceId);

            if (invoice == null)
                throw new InvalidOperationException("La factura no existe.");

            if (invoice.Estado != EstadoFactura.Borrador)
                throw new InvalidOperationException("Solo se pueden emitir facturas que están en estado Borrador.");

            _logger.LogInformation("Iniciando emisión de factura borrador ID: {Id}", invoiceId);

            foreach (var detalle in invoice.Detalles)
            {
                if (detalle.Producto.ManejaInventario)
                {
                    if (detalle.Producto.ManejaLotes)
                    {
                        await DescontarStockDeLotes(context, detalle);
                    }
                    else
                    {
                        await DescontarStockGeneral(detalle.Producto, detalle.Cantidad);
                    }
                }
            }

            var (xmlGenerado, xmlFirmadoBytes, claveAcceso) = await GenerarYFirmarXmlAsync(context, invoice, invoice.Cliente!);

            var facturaSri = await context.FacturasSRI.FirstAsync(f => f.FacturaId == invoice.Id);
            facturaSri.XmlGenerado = xmlGenerado;
            facturaSri.XmlFirmado = Encoding.UTF8.GetString(xmlFirmadoBytes);

            invoice.Estado = EstadoFactura.Pendiente;
            await context.SaveChangesAsync();

            _logger.LogInformation("Factura borrador actualizada a Pendiente. Iniciando envío background para {Numero}", invoice.NumeroFactura);
            _ = Task.Run(() => EnviarAlSriEnFondoAsync(invoice.Id, xmlFirmadoBytes, claveAcceso));

            return await GetInvoiceDetailByIdAsync(invoiceId);
        }

        public async Task ReactivateCancelledInvoiceAsync(Guid invoiceId)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            var invoice = await context.Facturas.FindAsync(invoiceId);
            if (invoice == null)
            {
                throw new InvalidOperationException("La factura no existe.");
            }

            if (invoice.Estado != EstadoFactura.Cancelada)
            {
                throw new InvalidOperationException("Solo se pueden reactivar facturas que están en estado Cancelada.");
            }

            invoice.Estado = EstadoFactura.Borrador;
            await context.SaveChangesAsync();
        }

        public async Task<InvoiceDto?> UpdateInvoiceAsync(UpdateInvoiceDto invoiceDto)
        {
            if (invoiceDto.EsConsumidorFinal && invoiceDto.FormaDePago != FormaDePago.Contado)
            {
                throw new InvalidOperationException("Las facturas para Consumidor Final solo pueden ser de Contado.");
            }

            await using var context = await _contextFactory.CreateDbContextAsync();
            var invoice = await context.Facturas
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
                invoice.ClienteId = (await GetOrCreateConsumidorFinalClientAsync(context)).Id;
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

            context.FacturaDetalles.RemoveRange(invoice.Detalles);
            invoice.Detalles.Clear();

            decimal subtotalSinImpuestos = 0;
            decimal totalIva = 0;

            foreach (var item in invoiceDto.Items)
            {
                var producto = await context.Productos
                    .Include(p => p.ProductoImpuestos).ThenInclude(pi => pi.Impuesto)
                    .SingleAsync(p => p.Id == item.ProductoId);

                decimal valorIvaItem = 0;
                var impuestoIva = producto.ProductoImpuestos.FirstOrDefault(pi => pi.Impuesto != null && pi.Impuesto.Porcentaje > 0);
                var subtotalItem = item.Cantidad * producto.PrecioVentaUnitario;

                if (impuestoIva != null && impuestoIva.Impuesto != null)
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

            await context.SaveChangesAsync();
            
            _logger.LogInformation("Borrador de factura {Id} actualizado.", invoice.Id);

            if (invoiceDto.EmitirTrasGuardar)
            {
                _logger.LogInformation("Flag 'EmitirTrasGuardar' detectado. Emisión inmediata...");
                await IssueDraftInvoiceAsync(invoice.Id);
            }

            return await GetInvoiceByIdAsync(invoice.Id);
        }
        
        public Task SendPaymentReminderEmailAsync(Guid invoiceId)
        {
            Task.Run(async () =>
            {
                var invoice = await GetInvoiceDetailByIdAsync(invoiceId);
                if (invoice == null) return;
                if (string.IsNullOrEmpty(invoice.ClienteEmail)) return;
                if (invoice.FechaVencimiento == null) return;

                try
                {
                    await _emailService.SendPaymentReminderEmailAsync(
                        invoice.ClienteEmail,
                        invoice.ClienteNombre,
                        invoice.NumeroFactura,
                        invoice.Total,
                        invoice.SaldoPendiente,
                        invoice.FechaVencimiento.Value
                    );
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error enviando email de recordatorio de pago en background");
                }
            });
            return Task.CompletedTask;
        }

        public async Task<PaginatedList<InvoiceDto>> GetInvoicesByClientIdAsync(Guid clienteId, int pageNumber, int pageSize, EstadoFactura? status, string? searchTerm)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            var query = context.Facturas.AsNoTracking().Where(i => i.ClienteId == clienteId);

            if (status.HasValue)
            {
                query = query.Where(i => i.Estado == status.Value);
            }

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                query = query.Where(i => i.NumeroFactura.Contains(searchTerm));
            }
            
            var finalQuery = query
                .OrderByDescending(i => i.FechaCreacion)
                .Select(i => new InvoiceDto
                {
                    Id = i.Id,
                    FechaEmision = i.FechaEmision,
                    NumeroFactura = i.NumeroFactura,
                    Estado = i.Estado,
                    ClienteId = i.ClienteId,
                    ClienteNombre = i.Cliente != null ? i.Cliente.RazonSocial : "",
                    Total = i.Total,
                    SaldoPendiente = context.CuentasPorCobrar.Where(c => c.FacturaId == i.Id).Select(c => c.SaldoPendiente).FirstOrDefault()
                });

            return await PaginatedList<InvoiceDto>.CreateAsync(finalQuery, pageNumber, pageSize);
        }
    }
}