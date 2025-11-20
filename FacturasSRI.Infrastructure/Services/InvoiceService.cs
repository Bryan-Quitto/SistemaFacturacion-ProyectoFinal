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

        public InvoiceService(
            FacturasSRIDbContext context, 
            ILogger<InvoiceService> logger, 
            IConfiguration configuration,
            FirmaDigitalService firmaDigitalService,
            XmlGeneratorService xmlGeneratorService,
            SriApiClientService sriApiClientService,
            SriResponseParserService sriResponseParserService
            )
        {
            _context = context;
            _logger = logger;
            _configuration = configuration;
            _firmaDigitalService = firmaDigitalService;
            _xmlGeneratorService = xmlGeneratorService;
            _sriApiClientService = sriApiClientService;
            _sriResponseParserService = sriResponseParserService;
        }
        
        private async Task<Cliente> GetOrCreateConsumidorFinalClientAsync()
        {
            var consumidorFinalId = "9999999999";

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
            // 1. VALIDACIÓN DE CONFIGURACIÓN
            var rucEmisor = _configuration["CompanyInfo:Ruc"];
            var establishmentCode = _configuration["CompanyInfo:EstablishmentCode"];
            var emissionPointCode = _configuration["CompanyInfo:EmissionPointCode"];
            var environmentType = _configuration["CompanyInfo:EnvironmentType"];
            var certificatePath = _configuration["CompanyInfo:CertificatePath"];
            var certificatePassword = _configuration["CompanyInfo:CertificatePassword"];

            if (string.IsNullOrEmpty(rucEmisor)) throw new InvalidOperationException("Falta configurar 'CompanyInfo:Ruc' en appsettings.");
            if (string.IsNullOrEmpty(establishmentCode)) throw new InvalidOperationException("Falta configurar 'CompanyInfo:EstablishmentCode' en appsettings.");
            if (string.IsNullOrEmpty(emissionPointCode)) throw new InvalidOperationException("Falta configurar 'CompanyInfo:EmissionPointCode' en appsettings.");
            if (string.IsNullOrEmpty(environmentType)) throw new InvalidOperationException("Falta configurar 'CompanyInfo:EnvironmentType' en appsettings.");
            if (string.IsNullOrEmpty(certificatePath)) throw new InvalidOperationException("Falta configurar 'CompanyInfo:CertificatePath' en appsettings.");
            if (string.IsNullOrEmpty(certificatePassword)) throw new InvalidOperationException("Falta configurar 'CompanyInfo:CertificatePassword' en appsettings.");

            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    Cliente cliente;
                    
                    if (invoiceDto.EsConsumidorFinal)
                    {
                        cliente = await GetOrCreateConsumidorFinalClientAsync();
                    }
                    else if (invoiceDto.ClienteId.HasValue)
                    {
                        cliente = await _context.Clientes.FindAsync(invoiceDto.ClienteId.Value);
                        if (cliente == null)
                        {
                            throw new ArgumentException("Cliente no encontrado.");
                        }
                    }
                    else
                    {
                        cliente = new Cliente
                        {
                            Id = Guid.NewGuid(),
                            TipoIdentificacion = invoiceDto.TipoIdentificacionComprador ?? throw new ArgumentException("Tipo de identificación del comprador es requerido."),
                            NumeroIdentificacion = invoiceDto.IdentificacionComprador ?? throw new ArgumentException("Número de identificación del comprador es requerido."),
                            RazonSocial = invoiceDto.RazonSocialComprador ?? throw new ArgumentException("Razón social del comprador es requerida."),
                            Direccion = invoiceDto.DireccionComprador,
                            Email = invoiceDto.EmailComprador,
                            Telefono = null,
                            FechaCreacion = DateTime.UtcNow,
                            EstaActivo = true
                        };
                        _context.Clientes.Add(cliente);
                    }

                    var secuencial = await _context.Secuenciales
                        .FirstOrDefaultAsync(s => s.Establecimiento == establishmentCode && s.PuntoEmision == emissionPointCode);

                    if (secuencial == null)
                    {
                        secuencial = new Secuencial { 
                            Id = Guid.NewGuid(),
                            Establecimiento = establishmentCode, 
                            PuntoEmision = emissionPointCode, 
                            UltimoSecuencialFactura = 0 
                        };
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
                        Estado = EstadoFactura.Generada,
                        UsuarioIdCreador = invoiceDto.UsuarioIdCreador, 
                        FechaCreacion = DateTime.UtcNow
                    };

                    decimal subtotalSinImpuestos = 0;
                    decimal totalIva = 0;

                    foreach (var item in invoiceDto.Items)
                    {
                        var producto = await _context.Productos
                            .Include(p => p.ProductoImpuestos)
                            .ThenInclude(pi => pi.Impuesto)
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
                    
                    var cuentaPorCobrar = new CuentaPorCobrar
                    {
                        Id = Guid.NewGuid(),
                        FacturaId = invoice.Id,
                        ClienteId = cliente.Id,
                        FechaEmision = invoice.FechaEmision,
                        FechaVencimiento = invoice.FechaEmision.AddDays(30),
                        MontoTotal = invoice.Total,
                        SaldoPendiente = invoice.Total,
                        UsuarioIdCreador = invoiceDto.UsuarioIdCreador,
                        FechaCreacion = DateTime.UtcNow
                    };
                    
                    _context.Facturas.Add(invoice);
                    _context.CuentasPorCobrar.Add(cuentaPorCobrar);
                    
                    // Generamos la clave con las variables validadas
                    var claveAcceso = GenerarClaveAcceso(invoice.FechaEmision, "01", rucEmisor, establishmentCode, emissionPointCode, numeroSecuencial, environmentType);
                    
                    var facturaSri = new FacturaSRI
                    {
                        FacturaId = invoice.Id,
                        ClaveAcceso = claveAcceso
                    };
                    
                    _context.FacturasSRI.Add(facturaSri);

                    await _context.SaveChangesAsync();
                    
                    // Tupla corregida
                    var (xmlGenerado, xmlFirmadoBytes) = _xmlGeneratorService.GenerarYFirmarFactura(claveAcceso, invoice, cliente, certificatePath, certificatePassword);
                    
                    facturaSri.XmlGenerado = xmlGenerado; 
                    facturaSri.XmlFirmado = Encoding.UTF8.GetString(xmlFirmadoBytes);

                    string respuestaRecepcionXml = await _sriApiClientService.EnviarRecepcionAsync(xmlFirmadoBytes);
                    RespuestaRecepcion respuestaRecepcion = _sriResponseParserService.ParsearRespuestaRecepcion(respuestaRecepcionXml);

                    if (respuestaRecepcion.Estado == "DEVUELTA")
                    {
                        invoice.Estado = EstadoFactura.RechazadaSRI;
                        facturaSri.RespuestaSRI = JsonSerializer.Serialize(respuestaRecepcion.Errores);
                        await _context.SaveChangesAsync();
                        await transaction.CommitAsync();
                        
                        var errorMsg = string.Join(", ", respuestaRecepcion.Errores.Select(e => $"{e.Identificador}: {e.Mensaje}"));
                        _logger.LogWarning("Factura rechazada en Recepción: {Error}", errorMsg);
                        throw new InvalidOperationException($"El SRI rechazó la factura (Recepción): {errorMsg}");
                    }

                    invoice.Estado = EstadoFactura.EnviadaSRI;
                    await _context.SaveChangesAsync();
                    
                    await Task.Delay(1000);

                    string respuestaAutorizacionXml = await _sriApiClientService.ConsultarAutorizacionAsync(claveAcceso);
                    RespuestaAutorizacion respuestaAutorizacion = _sriResponseParserService.ParsearRespuestaAutorizacion(respuestaAutorizacionXml);

                    switch (respuestaAutorizacion.Estado)
                    {
                        case "AUTORIZADO":
                            invoice.Estado = EstadoFactura.Autorizada;
                            facturaSri.NumeroAutorizacion = respuestaAutorizacion.NumeroAutorizacion;
                            facturaSri.FechaAutorizacion = respuestaAutorizacion.FechaAutorizacion;
                            facturaSri.RespuestaSRI = "AUTORIZADO";
                            break;
                        
                        case "NO AUTORIZADO":
                            invoice.Estado = EstadoFactura.RechazadaSRI;
                            facturaSri.RespuestaSRI = JsonSerializer.Serialize(respuestaAutorizacion.Errores);
                            break;

                        case "PROCESANDO":
                            facturaSri.RespuestaSRI = "El SRI todavía está procesando la autorización.";
                            _logger.LogInformation("Factura {NumeroFactura} sigue en procesamiento.", invoice.NumeroFactura);
                            break;

                        case "ERROR":
                            invoice.Estado = EstadoFactura.RechazadaSRI;
                            facturaSri.RespuestaSRI = JsonSerializer.Serialize(respuestaAutorizacion.Errores);
                            break;

                        default:
                            invoice.Estado = EstadoFactura.RechazadaSRI;
                            facturaSri.RespuestaSRI = "Respuesta desconocida del servicio de autorización.";
                            _logger.LogWarning("Respuesta desconocida del SRI: {Estado}", respuestaAutorizacion.Estado);
                            break;
                    }
                    
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    var resultDto = await GetInvoiceByIdAsync(invoice.Id);
                    return resultDto!;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error al crear la factura.");
                    await transaction.RollbackAsync();
                    throw;
                }
            }    
        }
        
        public async Task<InvoiceDetailViewDto?> CheckSriStatusAsync(Guid invoiceId)
        {
            var invoice = await _context.Facturas
                .Include(i => i.InformacionSRI)
                .FirstOrDefaultAsync(i => i.Id == invoiceId);

            if (invoice == null || invoice.InformacionSRI == null || string.IsNullOrEmpty(invoice.InformacionSRI.ClaveAcceso))
            {
                _logger.LogWarning("No se encontró factura o información del SRI para consultar el estado (ID: {InvoiceId}).", invoiceId);
                return null;
            }

            if (invoice.Estado == EstadoFactura.Autorizada)
            {
                _logger.LogInformation("La factura {InvoiceId} ya está autorizada. No se consulta al SRI.", invoiceId);
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
                        break;
                    
                    case "NO AUTORIZADO":
                        invoice.Estado = EstadoFactura.RechazadaSRI;
                        invoice.InformacionSRI.RespuestaSRI = JsonSerializer.Serialize(respuestaAutorizacion.Errores);
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
                        _logger.LogWarning("Respuesta desconocida del SRI: {Estado}", respuestaAutorizacion.Estado);
                        break;
                }

                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al consultar el estado de la factura {InvoiceId} en el SRI.", invoiceId);
                invoice.InformacionSRI.RespuestaSRI = $"Error de conexión: {ex.Message}";
                await _context.SaveChangesAsync();
            }
            
            return await GetInvoiceDetailByIdAsync(invoiceId);
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
                            CreadoPor = usuario != null ? usuario.PrimerNombre + " " + usuario.PrimerApellido : "Usuario no encontrado"
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
                ClaveAcceso = invoice.InformacionSRI?.ClaveAcceso,
                NumeroAutorizacion = invoice.InformacionSRI?.NumeroAutorizacion,
                RespuestaSRI = invoice.InformacionSRI?.RespuestaSRI
            };
        }
    }
}