using System;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using System.Xml;
using FacturasSRI.Domain.Enums; 
using System.Globalization;
using System.Collections.Generic;
using System.Text;

using FacturaDominio = FacturasSRI.Domain.Entities.Factura;
using ClienteDominio = FacturasSRI.Domain.Entities.Cliente;

using FacturaXml = FacturasSRI.Core.XmlModels.Factura.Factura;
using InfoTributariaXml = FacturasSRI.Core.XmlModels.Factura.InfoTributaria;
using InfoFacturaXml = FacturasSRI.Core.XmlModels.Factura.FacturaInfoFactura;
using DetalleXml = FacturasSRI.Core.XmlModels.Factura.FacturaDetallesDetalle;
using TotalImpuestoXml = FacturasSRI.Core.XmlModels.Factura.FacturaInfoFacturaTotalConImpuestosTotalImpuesto;
using ImpuestoDetalleXml = FacturasSRI.Core.XmlModels.Factura.Impuesto;
using FacturasSRI.Core.XmlModels.Factura;

namespace FacturasSRI.Core.Services
{
    public class XmlGeneratorService
    {
        private readonly FirmaDigitalService _firmaService;

        private const string RUC_EMISOR = "1850641927001";
        private const string RAZON_SOCIAL_EMISOR = "AÑILEMA HOFFMANN JIMMY ALEXANDER";
        private const string NOMBRE_COMERCIAL_EMISOR = "AETHER TECH";
        private const string DIRECCION_MATRIZ_EMISOR = "AV. BENJAMIN FRANKLIN SNN Y EDWARD JENNER";
        private const string COD_ESTABLECIMIENTO = "001";
        private const string COD_PUNTO_EMISION = "001";
        private const ObligadoContabilidad OBLIGADO_CONTABILIDAD = ObligadoContabilidad.No;
        private const string TIPO_AMBIENTE = "1"; 
        
        private readonly CultureInfo _cultureInfo = CultureInfo.InvariantCulture;

        public XmlGeneratorService(FirmaDigitalService firmaService)
        {
            _firmaService = firmaService;
        }

        public (string XmlGenerado, byte[] XmlFirmadoBytes) GenerarYFirmarFactura(
            string claveAcceso,
            FacturaDominio facturaDominio, 
            ClienteDominio clienteDominio,
            string rutaCertificado,
            string passwordCertificado
            )
        {
            FacturaXml facturaXml = GenerarXmlFactura(claveAcceso, facturaDominio, clienteDominio);
            
            string xmlSinFirmar = SerializarObjeto(facturaXml);

            byte[] xmlFirmadoBytes = _firmaService.FirmarXml(xmlSinFirmar, rutaCertificado, passwordCertificado);

            return (xmlSinFirmar, xmlFirmadoBytes);
        }

        private FacturaXml GenerarXmlFactura( 
            string claveAcceso,
            FacturaDominio facturaDominio, 
            ClienteDominio clienteDominio
            )
        {
            string secuencialFormateado = facturaDominio.NumeroFactura.PadLeft(9, '0');

            var facturaXml = new FacturaXml 
            {
                Id = FacturaId.Comprobante,
                IdSpecified = true,
                Version = "1.1.0", 
            };
            
            facturaXml.InfoTributaria = new InfoTributariaXml 
            {
                Ambiente = TIPO_AMBIENTE,
                TipoEmision = "1", 
                RazonSocial = NormalizeString(RAZON_SOCIAL_EMISOR),
                NombreComercial = NormalizeString(NOMBRE_COMERCIAL_EMISOR),
                Ruc = RUC_EMISOR,
                ClaveAcceso = claveAcceso,
                CodDoc = "01", 
                Estab = COD_ESTABLECIMIENTO,
                PtoEmi = COD_PUNTO_EMISION,
                Secuencial = secuencialFormateado,
                DirMatriz = NormalizeString(DIRECCION_MATRIZ_EMISOR)
            };

            facturaXml.InfoFactura = new InfoFacturaXml 
            {
                FechaEmision = facturaDominio.FechaEmision.ToString("dd/MM/yyyy"),
                DirEstablecimiento = NormalizeString(DIRECCION_MATRIZ_EMISOR), 
                ObligadoContabilidad = OBLIGADO_CONTABILIDAD,
                ObligadoContabilidadSpecified = true,
                
                TipoIdentificacionComprador = MapearTipoIdentificacion(clienteDominio.TipoIdentificacion, clienteDominio.NumeroIdentificacion),
                RazonSocialComprador = NormalizeString(clienteDominio.RazonSocial),
                IdentificacionComprador = clienteDominio.NumeroIdentificacion,
                
                TotalSinImpuestos = facturaDominio.SubtotalSinImpuestos,
                TotalDescuento = facturaDominio.TotalDescuento,
                Propina = 0.00m,
                PropinaSpecified = true,
                ImporteTotal = facturaDominio.Total
            };
            
            facturaXml.InfoFactura.Pagos.Add(new PagosPago {
                FormaPago = "01",
                Total = facturaDominio.Total,
                Plazo = 0m,
                PlazoSpecified = true
            });
            
            var gruposImpuestos = facturaDominio.Detalles
                .SelectMany(d => d.Producto.ProductoImpuestos.Select(pi => new { Detalle = d, Impuesto = pi.Impuesto }))
                .GroupBy(x => new { Codigo = "2", CodigoPorcentaje = x.Impuesto.CodigoSRI })
                .Select(g => new TotalImpuestoXml 
                {
                    Codigo = g.Key.Codigo, 
                    CodigoPorcentaje = g.Key.CodigoPorcentaje, 
                    BaseImponible = g.Sum(x => x.Detalle.Subtotal),
                    Valor = g.Sum(x => x.Detalle.ValorIVA)
                });

            foreach (var grupo in gruposImpuestos)
            {
                facturaXml.InfoFactura.TotalConImpuestos.Add(grupo);
            }

            foreach (var detalle in facturaDominio.Detalles)
            {
                var detalleXml = new DetalleXml
                {
                    CodigoPrincipal = detalle.Producto.CodigoPrincipal,
                    Descripcion = NormalizeString(detalle.Producto.Nombre),
                    Cantidad = detalle.Cantidad,
                    PrecioUnitario = detalle.PrecioVentaUnitario,
                    Descuento = detalle.Descuento,
                    PrecioTotalSinImpuesto = detalle.Subtotal
                };

                var impuestosDetalle = detalle.Producto.ProductoImpuestos
                    .Select(pi => new ImpuestoDetalleXml 
                    {
                        Codigo = "2", 
                        CodigoPorcentaje = pi.Impuesto.CodigoSRI, 
                        Tarifa = pi.Impuesto.Porcentaje,
                        BaseImponible = detalle.Subtotal,
                        Valor = detalle.Subtotal * (pi.Impuesto.Porcentaje / 100)
                    });

                foreach (var impuesto in impuestosDetalle)
                {
                    detalleXml.Impuestos.Add(impuesto);
                }
                
                facturaXml.Detalles.Add(detalleXml);
            }

            return facturaXml;
        }
        
        private string NormalizeString(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            var normalizedString = input.Normalize(NormalizationForm.FormD);
            var stringBuilder = new StringBuilder();

            foreach (var c in normalizedString)
            {
                var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
                if (unicodeCategory != UnicodeCategory.NonSpacingMark)
                {
                    stringBuilder.Append(c);
                }
            }

            return stringBuilder.ToString().Normalize(NormalizationForm.FormC);
        }


        private string SerializarObjeto(object objeto)
        {
            var currentCulture = System.Threading.Thread.CurrentThread.CurrentCulture;
            try
            {
                System.Threading.Thread.CurrentThread.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;

                using (var stringWriter = new Utf8StringWriter())
                {
                    var settings = new XmlWriterSettings
                    {
                        Indent = true,
                        Encoding = new UTF8Encoding(false)
                    };

                    using (var xmlWriter = XmlWriter.Create(stringWriter, settings))
                    {
                        var serializer = new XmlSerializer(objeto.GetType());

                        var namespaces = new XmlSerializerNamespaces();
                        namespaces.Add(string.Empty, string.Empty);

                        serializer.Serialize(xmlWriter, objeto, namespaces);
                    }
                    return stringWriter.ToString();
                }
            }
            finally
            {
                System.Threading.Thread.CurrentThread.CurrentCulture = currentCulture;
            }
        }

        private string MapearTipoIdentificacion(TipoIdentificacion tipo, string numeroIdentificacion)
        {
            if (numeroIdentificacion == "9999999999")
            {
                return "07"; 
            }

            switch (tipo)
            {
                case TipoIdentificacion.Cedula:
                    return "05"; 
                case TipoIdentificacion.RUC:
                    return "04"; 
                case TipoIdentificacion.Pasaporte:
                    return "06"; 
                default:
                    return "07";
            }
        }
    }

    public class Utf8StringWriter : StringWriter
    {
        public override Encoding Encoding => Encoding.UTF8;
    }
}