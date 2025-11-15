using System;
using System.Security.Cryptography.X509Certificates;
using System.Xml;
using FirmaXadesNetCore;
using FirmaXadesNetCore.Signature.Parameters;

namespace FacturasSRI.Core.Services
{
    public class FirmaDigitalService
    {
        public string FirmarXml(string xmlSinFirmar, string rutaCertificado, string passwordCertificado)
        {
            var certificado = new X509Certificate2(rutaCertificado, passwordCertificado, X509KeyStorageFlags.Exportable);

            var xadesService = new XadesService();
            var parametros = new SignatureParameters
            {
                SignaturePolicyInfo = new SignaturePolicyInfo
                {
                    PolicyIdentifier = "http://www.w3.org/2000/09/xmldsig#"
                },
                SignaturePackaging = SignaturePackaging.ENVELOPED
            };

            var documentoXml = new XmlDocument();
            documentoXml.PreserveWhitespace = true;
            documentoXml.LoadXml(xmlSinFirmar);

            try
            {
                var signedXml = xadesService.Sign(documentoXml, certificado, parametros);
                return signedXml.Document.OuterXml;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al firmar el XML: {ex.Message}", ex);
            }
        }
    }
}