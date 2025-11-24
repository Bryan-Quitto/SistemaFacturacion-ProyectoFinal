using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace FacturasSRI.Core.Services
{
    public class SriApiClientService
    {
        private const string URL_RECEPCION_PRUEBAS = "https://celcer.sri.gob.ec/comprobantes-electronicos-ws/RecepcionComprobantesOffline";
        private const string URL_AUTORIZACION_PRUEBAS = "https://celcer.sri.gob.ec/comprobantes-electronicos-ws/AutorizacionComprobantesOffline";

        private readonly HttpClient _httpClient;
        private readonly ILogger<SriApiClientService> _logger;

        // Inyectamos HttpClient directamente. Ya vendrá configurado desde Program.cs
        public SriApiClientService(HttpClient httpClient, ILogger<SriApiClientService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task<string> EnviarRecepcionAsync(byte[] xmlFirmadoBytes)
        {
            // Siempre usamos la URL de PRUEBAS
            string url = URL_RECEPCION_PRUEBAS; 
            
            string base64Xml = Convert.ToBase64String(xmlFirmadoBytes);
            string sobreSoap = $@"<soapenv:Envelope xmlns:soapenv=""http://schemas.xmlsoap.org/soap/envelope/"" xmlns:ec=""http://ec.gob.sri.ws.recepcion"">
                                    <soapenv:Header/>
                                    <soapenv:Body>
                                        <ec:validarComprobante>
                                            <xml>{base64Xml}</xml>
                                        </ec:validarComprobante>
                                    </soapenv:Body>
                                  </soapenv:Envelope>";

            var httpContent = new StringContent(sobreSoap, Encoding.UTF8, "text/xml");
            
            try 
            {
                _logger.LogInformation("Enviando solicitud de recepción al SRI (Pruebas)...");
                
                HttpResponseMessage respuesta = await _httpClient.PostAsync(url, httpContent);
                string respuestaSoap = await respuesta.Content.ReadAsStringAsync();

                return respuestaSoap;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ERROR CONEXIÓN SRI (RECEPCIÓN)");
                throw;
            }
        }

        public async Task<string> ConsultarAutorizacionAsync(string claveAcceso)
        {
            // Siempre usamos la URL de PRUEBAS
            string url = URL_AUTORIZACION_PRUEBAS;

            string sobreSoap = $@"<soapenv:Envelope xmlns:soapenv=""http://schemas.xmlsoap.org/soap/envelope/"" xmlns:ec=""http://ec.gob.sri.ws.autorizacion"">
                                    <soapenv:Header/>
                                    <soapenv:Body>
                                        <ec:autorizacionComprobante>
                                            <claveAccesoComprobante>{claveAcceso}</claveAccesoComprobante>
                                        </ec:autorizacionComprobante>
                                    </soapenv:Body>
                                  </soapenv:Envelope>";
            
            var httpContent = new StringContent(sobreSoap, Encoding.UTF8, "text/xml");

            try
            {
                _logger.LogInformation("Consultando autorización al SRI (Pruebas) Clave: {Clave}", claveAcceso);
                
                HttpResponseMessage respuesta = await _httpClient.PostAsync(url, httpContent);
                string respuestaSoap = await respuesta.Content.ReadAsStringAsync();

                return respuestaSoap;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ERROR CONEXIÓN SRI (AUTORIZACIÓN)");
                throw;
            }
        }
    }
}