using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace FacturasSRI.Core.Services
{
    public class SriApiClientService
    {
        // URLs EXCLUSIVAS DE PRUEBAS
        private const string URL_RECEPCION_PRUEBAS = "https://celcer.sri.gob.ec/comprobantes-electronicos-ws/RecepcionComprobantesOffline";
        private const string URL_AUTORIZACION_PRUEBAS = "https://celcer.sri.gob.ec/comprobantes-electronicos-ws/AutorizacionComprobantesOffline";

        // Declaración de variables de clase (Aquí estaba el error de _httpClient)
        private readonly HttpClient _httpClient;
        private readonly ILogger<SriApiClientService> _logger;

        public SriApiClientService(ILogger<SriApiClientService> logger)
        {
            _logger = logger;

            // Configuración para ignorar errores de SSL (común en el servidor de pruebas de SRI 'celcer')
            // y forzar TLS 1.2
            var handler = new HttpClientHandler();
            handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true;
            handler.SslProtocols = System.Security.Authentication.SslProtocols.Tls12;

            _httpClient = new HttpClient(handler);
            _httpClient.Timeout = TimeSpan.FromSeconds(60); 
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("FacturasSRI-Client/1.0");
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