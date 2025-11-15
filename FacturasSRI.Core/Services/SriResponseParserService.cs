// En: FacturasSRI.Core/Services/SriResponseParserService.cs

using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using FacturasSRI.Core.Models; // <-- ¡El archivo que creamos antes!

namespace FacturasSRI.Core.Services
{
    public class SriResponseParserService
    {
        // Los namespaces son la parte más compleja de SOAP.
        // El SRI usa 'ns2' para sus respuestas.
        private static XNamespace ns2 = "http://ec.gob.sri.ws.recepcion";
        private static XNamespace ns2_auth = "http://ec.gob.sri.ws.autorizacion";

        /// <summary>
        /// Parsea la respuesta XML del servicio de Recepción.
        /// </summary>
        public RespuestaRecepcion ParsearRespuestaRecepcion(string soapResponse)
        {
            var respuesta = new RespuestaRecepcion();
            var xmlDoc = XDocument.Parse(soapResponse);

            // 1. Navegamos dentro del sobre SOAP hasta la respuesta
            var respuestaNode = xmlDoc.Descendants(ns2 + "validarComprobanteResponse").FirstOrDefault();
            if (respuestaNode == null)
            {
                throw new Exception("No se encontró 'validarComprobanteResponse' en la respuesta SOAP.");
            }

            // 2. Extraemos el estado principal
            respuesta.Estado = respuestaNode.Descendants("estado").FirstOrDefault()?.Value ?? "ERROR";

            // 3. Si fue DEVUELTA, extraemos los errores
            if (respuesta.Estado == "DEVUELTA")
            {
                respuesta.Errores = xmlDoc.Descendants("mensaje")
                    .Select(m => new SriError
                    {
                        Identificador = m.Descendants("identificador").FirstOrDefault()?.Value ?? "",
                        Mensaje = m.Descendants("mensaje").FirstOrDefault()?.Value ?? "",
                        InformacionAdicional = m.Descendants("informacionAdicional").FirstOrDefault()?.Value ?? "",
                        Tipo = m.Descendants("tipo").FirstOrDefault()?.Value ?? ""
                    })
                    .ToList();
            }

            return respuesta;
        }

        /// <summary>
        /// Parsea la respuesta XML del servicio de Autorización.
        /// </summary>
        public RespuestaAutorizacion ParsearRespuestaAutorizacion(string soapResponse)
        {
            var respuesta = new RespuestaAutorizacion();
            var xmlDoc = XDocument.Parse(soapResponse);

            // 1. Navegamos dentro del sobre SOAP
            var autorizacionNode = xmlDoc.Descendants(ns2_auth + "autorizacionComprobanteResponse")
                                         .Descendants("autorizacion")
                                         .FirstOrDefault();
            
            if (autorizacionNode == null)
            {
                // A veces la respuesta es un "fault" (error de SOAP)
                var faultString = xmlDoc.Descendants("faultstring").FirstOrDefault()?.Value;
                if (!string.IsNullOrEmpty(faultString))
                {
                    respuesta.Estado = "ERROR";
                    respuesta.Errores.Add(new SriError { Identificador = "SOAP", Mensaje = faultString });
                    return respuesta;
                }
                throw new Exception("No se encontró 'autorizacion' en la respuesta SOAP.");
            }

            // 2. Extraemos el estado
            respuesta.Estado = autorizacionNode.Descendants("estado").FirstOrDefault()?.Value ?? "ERROR";

            // 3. Si fue AUTORIZADO, extraemos los datos
            if (respuesta.Estado == "AUTORIZADO")
            {
                respuesta.NumeroAutorizacion = autorizacionNode.Descendants("numeroAutorizacion").FirstOrDefault()?.Value ?? "";
                
                if (DateTime.TryParse(autorizacionNode.Descendants("fechaAutorizacion").FirstOrDefault()?.Value, out DateTime fecha))
                {
                    respuesta.FechaAutorizacion = fecha;
                }
            }
            // 4. Si fue NO AUTORIZADO, extraemos los errores
            else if (respuesta.Estado == "NO AUTORIZADO")
            {
                respuesta.Errores = autorizacionNode.Descendants("mensaje")
                    .Select(m => new SriError
                    {
                        Identificador = m.Descendants("identificador").FirstOrDefault()?.Value ?? "",
                        Mensaje = m.Descendants("mensaje").FirstOrDefault()?.Value ?? "",
                        InformacionAdicional = m.Descendants("informacionAdicional").FirstOrDefault()?.Value ?? "",
                        Tipo = m.Descendants("tipo").FirstOrDefault()?.Value ?? ""
                    })
                    .ToList();
            }

            return respuesta;
        }
    }
}