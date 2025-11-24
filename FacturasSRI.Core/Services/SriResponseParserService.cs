using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using FacturasSRI.Core.Models;
using Microsoft.Extensions.Logging;

namespace FacturasSRI.Core.Services
{
    public class SriResponseParserService
    {
        private readonly ILogger<SriResponseParserService> _logger;

        public SriResponseParserService(ILogger<SriResponseParserService> logger)
        {
            _logger = logger;
        }

        public RespuestaRecepcion ParsearRespuestaRecepcion(string soapResponse)
        {
            _logger.LogInformation("Parsing Respuesta Recepción...");
            var respuesta = new RespuestaRecepcion();
            var xmlDoc = XDocument.Parse(soapResponse);

            // Buscamos el nodo validarComprobanteResponse sin importar el namespace
            var respuestaNode = xmlDoc.Descendants().FirstOrDefault(d => d.Name.LocalName == "validarComprobanteResponse");
            
            if (respuestaNode == null)
            {
                var faultString = xmlDoc.Descendants("faultstring").FirstOrDefault()?.Value;
                if (!string.IsNullOrEmpty(faultString))
                {
                    throw new Exception($"Error SRI (SOAP Fault): {faultString}");
                }
                throw new Exception("No se encontró 'validarComprobanteResponse' en la respuesta.");
            }

            respuesta.Estado = respuestaNode.Descendants().FirstOrDefault(d => d.Name.LocalName == "estado")?.Value ?? "ERROR";

            if (respuesta.Estado == "DEVUELTA")
            {
                respuesta.Errores = xmlDoc.Descendants().Where(d => d.Name.LocalName == "mensaje")
                    .Select(m => new SriError
                    {
                        Identificador = m.Descendants().FirstOrDefault(d => d.Name.LocalName == "identificador")?.Value ?? "",
                        Mensaje = m.Descendants().FirstOrDefault(d => d.Name.LocalName == "mensaje")?.Value ?? "",
                        InformacionAdicional = m.Descendants().FirstOrDefault(d => d.Name.LocalName == "informacionAdicional")?.Value ?? "",
                        Tipo = m.Descendants().FirstOrDefault(d => d.Name.LocalName == "tipo")?.Value ?? ""
                    })
                    .ToList();
            }

            return respuesta;
        }

        public RespuestaAutorizacion ParsearRespuestaAutorizacion(string soapResponse)
        {
            var respuesta = new RespuestaAutorizacion();
            var xmlDoc = XDocument.Parse(soapResponse);

            var autorizacionNode = xmlDoc.Descendants().FirstOrDefault(d => d.Name.LocalName == "autorizacion");
            
            if (autorizacionNode == null)
            {
                var faultString = xmlDoc.Descendants("faultstring").FirstOrDefault()?.Value;
                if (!string.IsNullOrEmpty(faultString))
                {
                    respuesta.Estado = "ERROR";
                    respuesta.Errores.Add(new SriError { Identificador = "SOAP", Mensaje = faultString });
                    return respuesta;
                }
                
                // Si no hay nodo, es procesando
                respuesta.Estado = "PROCESANDO";
                return respuesta;
            }

            respuesta.Estado = autorizacionNode.Descendants().FirstOrDefault(d => d.Name.LocalName == "estado")?.Value ?? "ERROR";

            if (respuesta.Estado == "AUTORIZADO")
            {
                respuesta.NumeroAutorizacion = autorizacionNode.Descendants().FirstOrDefault(d => d.Name.LocalName == "numeroAutorizacion")?.Value ?? "";
                var fechaStr = autorizacionNode.Descendants().FirstOrDefault(d => d.Name.LocalName == "fechaAutorizacion")?.Value;
                if (DateTime.TryParse(fechaStr, out DateTime fecha)) respuesta.FechaAutorizacion = fecha.ToUniversalTime(); 
            }
            else if (respuesta.Estado == "NO AUTORIZADO")
            {
                respuesta.Errores = autorizacionNode.Descendants().Where(d => d.Name.LocalName == "mensaje")
                    .Select(m => new SriError
                    {
                        Identificador = m.Descendants().FirstOrDefault(d => d.Name.LocalName == "identificador")?.Value ?? "",
                        Mensaje = m.Descendants().FirstOrDefault(d => d.Name.LocalName == "mensaje")?.Value ?? "",
                        InformacionAdicional = m.Descendants().FirstOrDefault(d => d.Name.LocalName == "informacionAdicional")?.Value ?? "",
                        Tipo = m.Descendants().FirstOrDefault(d => d.Name.LocalName == "tipo")?.Value ?? ""
                    })
                    .ToList();
            }

            return respuesta;
        }
    }
}