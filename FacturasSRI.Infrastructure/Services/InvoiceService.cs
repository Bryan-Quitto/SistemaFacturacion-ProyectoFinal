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

        public InvoiceService(
            FacturasSRIDbContext context,
            ILogger<InvoiceService> logger,
            IConfiguration configuration,
            FirmaDigitalService firmaDigitalService,
            XmlGeneratorService xmlGeneratorService,
            SriApiClientService sriApiClientService,
            SriResponseParserService sriResponseParserService,
            IEmailService emailService,  
            PdfGeneratorService pdfGenerator
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
        }

        private async Task<Cliente> GetOrCreateConsumidorFinalClientAsync()
        {
            var consumidorFinalId = "9999999999999";

            var consumidorFinalClient = await _context.Clientes
                .FirstOrDefaultAsync(c => c.NumeroIdentificacion == consumidorFinalId);

            if (consumidorFinalClient == null)
            {
                consumidorFinalClient = new Cliente
                {
                    Id = Guid.NewGuid(),
                    TipoIdentificacion = default,
                    NumeroIdentificacion = consumidorFinalId,
                    RazonSocial = "CONSUMIDOR FINAL",
                    Direccion = "N/A",
                    Email = "consumidorfinal@example.com",
                    Telefono = "N/A",
                    FechaCreacion = DateTime.UtcNow,
                    EstaActivo = true
                };
                _context.Clientes.Add(consumidorFinalClient);
                await _context.SaveChangesAsync();
            }

            return consumidorFinalClient;
        }

        public async Task<InvoiceDto> CreateInvoiceAsync(CreateInvoiceDto invoiceDto)
        {
            await _invoiceCreationSemaphore.WaitAsync();
            try
            {
                // Step 1: Create the invoice in a "Pending" state in a local transaction.
                var (factura, cliente) = await CrearFacturaPendienteAsync(invoiceDto);

                // Step 2: Generate XML and sign it. This happens outside any DB transaction.
                var (xmlGenerado, xmlFirmadoBytes, claveAcceso) = await GenerarYFirmarXmlAsync(factura, cliente);

                // Update the FacturaSRI entity with the generated XMLs
                var facturaSri = await _context.FacturasSRI.FirstAsync(f => f.FacturaId == factura.Id);
                facturaSri.XmlGenerado = xmlGenerado;
                facturaSri.XmlFirmado = Encoding.UTF8.GetString(xmlFirmadoBytes);
                await _context.SaveChangesAsync();

                // Step 3: Send to SRI and process the response. This is the external call.
                string respuestaRecepcionXml = await _sriApiClientService.EnviarRecepcionAsync(xmlFirmadoBytes);

                // Step 4: Process and save the SRI response in a new, separate transaction.
                await ProcesarRespuestaSriAsync(factura.Id, claveAcceso, respuestaRecepcionXml);

                // Step 5: Return the final state of the invoice.
                var resultDto = await GetInvoiceByIdAsync(factura.Id);
                return resultDto!;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en el proceso de creación de factura.");
                // Note: We are not doing a transaction rollback here because the operations are now separated.
                // A more robust implementation would include compensation logic to undo previous steps if a later one fails.
                // For example, if sending to SRI fails, we might want to mark the invoice as "Failed" instead of "Pending".
                throw;
            }
            finally
            {
                _invoiceCreationSemaphore.Release();
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

                                            Estado = EstadoFactura.Pendiente, // New state

                                            UsuarioIdCreador = invoiceDto.UsuarioIdCreador,

                                            FechaCreacion = DateTime.UtcNow

                                        };

                                        

                                        _logger.LogWarning("Fecha de Emisión generada (UTC): {Fecha}", invoice.FechaEmision.ToString("o"));

                    

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

                        if (producto.ManejaInventario)
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
                    
                    // Assign payment terms to the invoice, to be used after authorization
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
                    if (invoice == null)
                    {
                        _logger.LogWarning("No se encontró la factura con ID {FacturaId} para procesar la respuesta del SRI.", facturaId);
                        return;
                    }
                    var facturaSri = await _context.FacturasSRI.FirstAsync(f => f.FacturaId == facturaId);

                    RespuestaRecepcion respuestaRecepcion = _sriResponseParserService.ParsearRespuestaRecepcion(respuestaRecepcionXml);

                    if (respuestaRecepcion.Estado == "DEVUELTA")
                    {
                        invoice.Estado = EstadoFactura.RechazadaSRI;
                        facturaSri.RespuestaSRI = JsonSerializer.Serialize(respuestaRecepcion.Errores);

                        // TODO: Implement compensation logic here. For example, roll back stock changes.
                        // For now, we just log the error.
                        var errorMsg = string.Join(", ", respuestaRecepcion.Errores.Select(e => $"{e.Identificador}: {e.Mensaje}"));
                        _logger.LogWarning("Factura rechazada en Recepción: {Error}", errorMsg);
                    }
                    else
                    {
                        invoice.Estado = EstadoFactura.EnviadaSRI; // Or "Procesando"
                    }

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    // We could trigger a background job to check for authorization later.
                    // For now, we'll try to check it right away for simplicity, but outside the reception transaction.
                    if (invoice.Estado == EstadoFactura.EnviadaSRI)
                    {
                        await Task.Delay(2000); // Wait as per SRI recommendation
                        await CheckSriStatusAsync(facturaId);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error procesando la respuesta del SRI.");
                    await transaction.RollbackAsync();

                    // It's important to mark the invoice as failed if processing the response fails
                    var invoice = await _context.Facturas.FindAsync(facturaId);
                    if (invoice != null)
                    {
                        invoice.Estado = EstadoFactura.RechazadaSRI; // Or a new "ProcessingError" state
                        var facturaSri = await _context.FacturasSRI.FirstAsync(f => f.FacturaId == facturaId);
                        facturaSri.RespuestaSRI = $"Error local procesando respuesta del SRI: {ex.Message}";
                        await _context.SaveChangesAsync();
                    }

                    throw; // Rethrow the exception so the caller knows something went wrong.
                }
            }
        }

        public async Task<InvoiceDetailViewDto?> CheckSriStatusAsync(Guid invoiceId)
        {
            var invoice = await _context.Facturas
                .Include(i => i.InformacionSRI)
                .Include(i => i.Cliente)
                .FirstOrDefaultAsync(i => i.Id == invoiceId);

            if (invoice == null || invoice.InformacionSRI == null || string.IsNullOrEmpty(invoice.InformacionSRI.ClaveAcceso))
            {
                _logger.LogWarning("No se encontró factura o información del SRI para consultar el estado (ID: {InvoiceId}).", invoiceId);
                return null;
            }

             if (invoice.Estado == EstadoFactura.Autorizada)
            {
                return await GetInvoiceDetailByIdAsync(invoiceId);
            }

            try
            {
                string respuestaAutorizacionXml = await _sriApiClientService.ConsultarAutorizacionAsync(invoice.InformacionSRI.ClaveAcceso);
                RespuestaAutorizacion respuestaAutorizacion = _sriResponseParserService.ParsearRespuestaAutorizacion(respuestaAutorizacionXml);

                switch (respuestaAutorizacion.Estado)
                {
                    case "AUTORIZADO":
                        invoice.Estado = EstadoFactura.Autorizada;
                        invoice.InformacionSRI.NumeroAutorizacion = respuestaAutorizacion.NumeroAutorizacion;
                        invoice.InformacionSRI.FechaAutorizacion = respuestaAutorizacion.FechaAutorizacion;
                        invoice.InformacionSRI.RespuestaSRI = "AUTORIZADO";

                        await CrearCuentaPorCobrarPostAutorizacionAsync(invoice);

                        await _context.SaveChangesAsync();

                        // Intentamos enviar el correo (Fire & Forget safe)
                        try 
                        {
                            // Obtenemos el DTO completo para poder generar el PDF
                            var invoiceDetailDto = await GetInvoiceDetailByIdAsync(invoiceId);
                            
                            if (invoiceDetailDto != null && !string.IsNullOrEmpty(invoice.Cliente.Email))
                            {
                                // 1. Generar PDF en memoria
                                byte[] pdfBytes = _pdfGenerator.GenerarFacturaPdf(invoiceDetailDto);

                                // 2. Obtener XML firmado (string)
                                string xmlFirmado = invoice.InformacionSRI.XmlFirmado ?? "";

                                // 3. Enviar Correo
                                await _emailService.SendInvoiceEmailAsync(
                                    invoice.Cliente.Email, 
                                    invoice.Cliente.RazonSocial, 
                                    invoice.NumeroFactura,
                                    invoice.Id,
                                    pdfBytes, 
                                    xmlFirmado
                                );
                                
                                _logger.LogInformation("Correo de factura enviado exitosamente a {Email}", invoice.Cliente.Email);
                            }
                        }
                        catch (Exception emailEx)
                        {
                            // Si falla el correo, NO revertimos la factura. Solo logueamos.
                            _logger.LogError(emailEx, "La factura se autorizó pero falló el envío de correo.");
                        }

                        break;

                    case "NO AUTORIZADO":
                        invoice.Estado = EstadoFactura.RechazadaSRI;
                        invoice.InformacionSRI.RespuestaSRI = JsonSerializer.Serialize(respuestaAutorizacion.Errores);
                        await _context.SaveChangesAsync();
                        break;

                    case "PROCESANDO":
                        invoice.InformacionSRI.RespuestaSRI = "El SRI todavía está procesando la autorización.";
                        _logger.LogInformation("Factura {NumeroFactura} sigue en procesamiento.", invoice.NumeroFactura);
                        break;

                    case "ERROR":
                        invoice.Estado = EstadoFactura.RechazadaSRI;
                        invoice.InformacionSRI.RespuestaSRI = JsonSerializer.Serialize(respuestaAutorizacion.Errores);
                        break;

                    default:
                        invoice.Estado = EstadoFactura.RechazadaSRI;
                        invoice.InformacionSRI.RespuestaSRI = $"Respuesta desconocida: {respuestaAutorizacion.Estado}";
                        await _context.SaveChangesAsync();
                        _logger.LogWarning("Respuesta desconocida del SRI: {Estado}", respuestaAutorizacion.Estado);
                        break;
                }

                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al consultar el estado de la factura {InvoiceId} en el SRI.", invoiceId);
                if (invoice?.InformacionSRI != null)
                {
                    invoice.InformacionSRI.RespuestaSRI = $"Error de conexión: {ex.Message}";
                    await _context.SaveChangesAsync();
                }
            }

            return await GetInvoiceDetailByIdAsync(invoiceId);
        }

        private async Task CrearCuentaPorCobrarPostAutorizacionAsync(Factura invoice)
        {
            var cuentaExistente = await _context.CuentasPorCobrar.FirstOrDefaultAsync(c => c.FacturaId == invoice.Id);
            if (cuentaExistente != null)
            {
                return; // Ya existe, no hacer nada.
            }

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
            else // Credito
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

            // --- DIAGNÓSTICO DE CLAVE DE ACCESO ---
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
                throw new InvalidOperationException($"Error al generar clave de acceso. Longitud: {clave.Length} (esperado: 48). " +
                    $"Detalle: Fecha({fecha.Length}), TipoComp({tipoComprobante.Length}), RUC({ruc?.Length}), Ambiente({tipoAmbiente?.Length}), " +
                    $"Estab({establecimiento?.Length}), PtoEmi({puntoEmision?.Length}), Secuencial({secuencial?.Length}), CodNum(8), TipoEmi(1)");
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
                if (factor > 7)
                {
                    factor = 2;
                }
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
                .Include(i => i.Detalles)
                .ThenInclude(d => d.Producto)
                .FirstOrDefaultAsync(i => i.Id == id);

            if (invoice == null) return null;

            return new InvoiceDto
            {
                Id = invoice.Id,
                FechaEmision = invoice.FechaEmision,
                NumeroFactura = invoice.NumeroFactura,
                ClienteId = invoice.ClienteId,
                SubtotalSinImpuestos = invoice.SubtotalSinImpuestos,
                TotalDescuento = invoice.TotalDescuento,
                TotalIVA = invoice.TotalIVA,
                Total = invoice.Total,
                Estado = invoice.Estado,
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

                        .Include(i => i.Detalles)

                            .ThenInclude(d => d.Producto)

                                .ThenInclude(p => p.ProductoImpuestos)

                                    .ThenInclude(pi => pi.Impuesto)

                        .Where(i => i.Id == id)

                        .FirstOrDefaultAsync();

        

                    if (invoice == null)

                    {

                        return null;

                    }

                    

                    var cuentaPorCobrar = await _context.CuentasPorCobrar.FirstOrDefaultAsync(c => c.FacturaId == invoice.Id);

        

                    var items = invoice.Detalles.Select(d => new InvoiceItemDetailDto

                    {

                        ProductoId = d.ProductoId,

                        ProductName = d.Producto.Nombre,

                        Cantidad = d.Cantidad,

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
                SaldoPendiente = cuentaPorCobrar?.SaldoPendiente ?? 0,
                ClaveAcceso = invoice.InformacionSRI?.ClaveAcceso,
                NumeroAutorizacion = invoice.InformacionSRI?.NumeroAutorizacion,
                RespuestaSRI = invoice.InformacionSRI?.RespuestaSRI
            };
        }
    private DateTime GetEcuadorTime(DateTime utcTime)
{
    try
    {
        // Intento para Windows
        var tz = TimeZoneInfo.FindSystemTimeZoneById("SA Pacific Standard Time");
        return TimeZoneInfo.ConvertTimeFromUtc(utcTime, tz);
    }
    catch
    {
        // Intento para Linux/Docker (Supabase suele correr aquí)
        try 
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById("America/Guayaquil");
            return TimeZoneInfo.ConvertTimeFromUtc(utcTime, tz);
        }
        catch
        {
            // Fallback manual si todo falla
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

        if (invoice == null || invoiceEntity == null) throw new Exception("Factura no encontrada");
        if (string.IsNullOrEmpty(invoice.ClienteEmail)) throw new Exception("El cliente no tiene email registrado");

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
    });
    return Task.CompletedTask;
}
    }
}