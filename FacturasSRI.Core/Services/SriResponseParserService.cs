using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using FacturasSRI.Core.Models;

namespace FacturasSRI.Core.Services
{
    public class SriResponseParserService
    {
        private static XNamespace ns2 = "http://ec.gob.sri.ws.recepcion";
        private static XNamespace ns2_auth = "http://ec.gob.sri.ws.autorizacion";

        public RespuestaRecepcion ParsearRespuestaRecepcion(string soapResponse)
        {
            var respuesta = new RespuestaRecepcion();
            var xmlDoc = XDocument.Parse(soapResponse);

            var respuestaNode = xmlDoc.Descendants(ns2 + "validarComprobanteResponse").FirstOrDefault();
            if (respuestaNode == null)
            {
                throw new Exception("No se encontró 'validarComprobanteResponse' en la respuesta SOAP.");
            }

            respuesta.Estado = respuestaNode.Descendants("estado").FirstOrDefault()?.Value ?? "ERROR";

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

        public RespuestaAutorizacion ParsearRespuestaAutorizacion(string soapResponse)
        {
            var respuesta = new RespuestaAutorizacion();
            var xmlDoc = XDocument.Parse(soapResponse);

            var autorizacionNode = xmlDoc.Descendants(ns2_auth + "autorizacionComprobanteResponse")
                                         .Descendants("autorizacion")
                                         .FirstOrDefault();
            
            if (autorizacionNode == null)
            {
                var faultString = xmlDoc.Descendants("faultstring").FirstOrDefault()?.Value;
                if (!string.IsNullOrEmpty(faultString))
                {
                    respuesta.Estado = "ERROR";
                    respuesta.Errores.Add(new SriError { Identificador = "SOAP", Mensaje = faultString });
                    return respuesta;
                }
                
                respuesta.Estado = "PROCESANDO";
                return respuesta;
            }

            respuesta.Estado = autorizacionNode.Descendants("estado").FirstOrDefault()?.Value ?? "ERROR";

            if (respuesta.Estado == "AUTORIZADO")
            {
                respuesta.NumeroAutorizacion = autorizacionNode.Descendants("numeroAutorizacion").FirstOrDefault()?.Value ?? "";
                
                if (DateTime.TryParse(autorizacionNode.Descendants("fechaAutorizacion").FirstOrDefault()?.Value, out DateTime fecha))
                {
                    respuesta.FechaAutorizacion = fecha;
                }
            }
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