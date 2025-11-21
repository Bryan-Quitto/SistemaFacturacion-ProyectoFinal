using System;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Xml;
using FirmaXadesNetCore;
using FirmaXadesNetCore.Signature.Parameters;

namespace FacturasSRI.Core.Services
{
    public class FirmaDigitalService
    {
        public byte[] FirmarXml(string xmlSinFirmar, string rutaCertificado, string passwordCertificado)
        {
            var certificado = new X509Certificate2(rutaCertificado, passwordCertificado, X509KeyStorageFlags.Exportable);

            var xadesService = new XadesService();

            var parametros = new SignatureParameters
            {
                Signer = new FirmaXadesNetCore.Crypto.Signer(certificado),
                
                SignaturePackaging = SignaturePackaging.ENVELOPED,
                
                DataFormat = new DataFormat 
                { 
                    MimeType = "text/xml" 
                }
            };

            var documentoXml = new XmlDocument();
            documentoXml.PreserveWhitespace = true;
            documentoXml.LoadXml(xmlSinFirmar);

            using (var stream = new MemoryStream())
            {
                documentoXml.Save(stream);
                stream.Position = 0;

                try
                {
                    var signedXml = xadesService.Sign(stream, parametros);
                    
                    using (var ms = new MemoryStream())
                    {
                        signedXml.Document.Save(ms);
                        return ms.ToArray();
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception($"Error al firmar el XML: {ex.Message}", ex);
                }
            }
        }
    }
}