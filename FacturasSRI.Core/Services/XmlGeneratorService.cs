using System;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using System.Xml;
using FacturasSRI.Domain.Enums; 
using System.Globalization;
using System.Collections.Generic;

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

        private const string RUC_EMISOR = "1799999999001";
        private const string RAZON_SOCIAL_EMISOR = "Aether Tecnologías";
        private const string NOMBRE_COMERCIAL_EMISOR = "Aether Tech";
        private const string DIRECCION_MATRIZ_EMISOR = "Av. de los Shyris N37-271 y Holanda, Edificio Shyris Center, Quito, Ecuador";
        private const string COD_ESTABLECIMIENTO = "001";
        private const string COD_PUNTO_EMISION = "001";
        private const ObligadoContabilidad OBLIGADO_CONTABILIDAD = ObligadoContabilidad.Si;
        private const string TIPO_AMBIENTE = "1"; 
        
        private readonly CultureInfo _cultureInfo = CultureInfo.InvariantCulture;

        public XmlGeneratorService(FirmaDigitalService firmaService)
        {
            _firmaService = firmaService;
        }

        public string GenerarYFirmarFactura(
            string claveAcceso,
            FacturaDominio facturaDominio, 
            ClienteDominio clienteDominio
            )
        {
            FacturaXml facturaXml = GenerarXmlFactura(claveAcceso, facturaDominio, clienteDominio);
            string xmlSinFirmar = SerializarObjeto(facturaXml);

            string rutaCertificado = @"C:\Users\THINKPAD\Desktop\certificado_prueba_sri.p12";
            string passwordCertificado = "9ninelivesL"; 

            string xmlFirmado = _firmaService.FirmarXml(xmlSinFirmar, rutaCertificado, passwordCertificado);

            return xmlFirmado;
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
                Version = "1.0.0", 
            };
            
            facturaXml.InfoTributaria = new InfoTributariaXml 
            {
                Ambiente = TIPO_AMBIENTE,
                TipoEmision = "1", 
                RazonSocial = RAZON_SOCIAL_EMISOR,
                NombreComercial = NOMBRE_COMERCIAL_EMISOR,
                Ruc = RUC_EMISOR,
                ClaveAcceso = claveAcceso,
                CodDoc = "01", 
                Estab = COD_ESTABLECIMIENTO,
                PtoEmi = COD_PUNTO_EMISION,
                Secuencial = secuencialFormateado,
                DirMatriz = DIRECCION_MATRIZ_EMISOR
            };

            facturaXml.InfoFactura = new InfoFacturaXml 
            {
                FechaEmision = facturaDominio.FechaEmision.ToString("dd/MM/yyyy"),
                DirEstablecimiento = DIRECCION_MATRIZ_EMISOR, 
                ObligadoContabilidad = OBLIGADO_CONTABILIDAD,
                
                TipoIdentificacionComprador = MapearTipoIdentificacion(clienteDominio.TipoIdentificacion),
                RazonSocialComprador = clienteDominio.RazonSocial,
                IdentificacionComprador = clienteDominio.NumeroIdentificacion,
                
                TotalSinImpuestos = facturaDominio.SubtotalSinImpuestos,
                TotalDescuento = facturaDominio.TotalDescuento,
                Propina = 0.00m,
                ImporteTotal = facturaDominio.Total
            };
            
            var gruposImpuestos = facturaDominio.Detalles
                .SelectMany(d => d.Producto.ProductoImpuestos.Select(pi => new { Detalle = d, Impuesto = pi.Impuesto }))
                .GroupBy(x => x.Impuesto)
                .Select(g => new TotalImpuestoXml 
                {
                    Codigo = "2", 
                    CodigoPorcentaje = g.Key.CodigoSRI, 
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
                    Descripcion = detalle.Producto.Nombre,
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
                        Valor = detalle.ValorIVA
                    });

                foreach (var impuesto in impuestosDetalle)
                {
                    detalleXml.Impuestos.Add(impuesto);
                }
                
                facturaXml.Detalles.Add(detalleXml);
            }

            return facturaXml;
        }

        private string SerializarObjeto(object objeto)
        {
            using (var stringWriter = new StringWriter())
            {
                var settings = new XmlWriterSettings
                {
                    Indent = true, 
                    Encoding = new System.Text.UTF8Encoding(false) 
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

        private string MapearTipoIdentificacion(TipoIdentificacion tipo)
        {
            switch (tipo)
            {
                case TipoIdentificacion.Cedula:
                    return "05"; 
                case TipoIdentificacion.RUC:
                    return "04"; 
                case TipoIdentificacion.Pasaporte:
                    return "06"; 
                case TipoIdentificacion.ConsumidorFinal:
                    return "07"; 
                default:
                    throw new ArgumentOutOfRangeException(nameof(tipo), $"Tipo de identificación no soportado por el SRI: {tipo}.");
            }
        }
    }
}