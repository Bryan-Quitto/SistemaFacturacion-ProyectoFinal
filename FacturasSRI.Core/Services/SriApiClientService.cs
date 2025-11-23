using System.Text;
using System.Threading.Tasks;
using System.Net.Http;
using System;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;

namespace FacturasSRI.Core.Services
{
    public class SriApiClientService
    {
        private const string URL_RECEPCION_PRUEBAS = "https://celcer.sri.gob.ec/comprobantes-electronicos-ws/RecepcionComprobantesOffline";
        private const string URL_AUTORIZACION_PRUEBAS = "https://celcer.sri.gob.ec/comprobantes-electronicos-ws/AutorizacionComprobantesOffline";

        private readonly HttpClient _httpClient;
        private readonly ILogger<SriApiClientService> _logger;

        public SriApiClientService(ILogger<SriApiClientService> logger)
        {
            _logger = logger;

            // CONFIGURACIÓN ESPECIAL PARA EL SRI (TLS 1.2 + Ignorar errores SSL)
            var handler = new HttpClientHandler();
            
            // 1. El SRI de pruebas a veces tiene certificados vencidos, esto evita que .NET lance error
            handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true;

            // 2. Forzamos TLS 1.2 porque el SRI no soporta las versiones nuevas de .NET por defecto
            handler.SslProtocols = System.Security.Authentication.SslProtocols.Tls12;

            _httpClient = new HttpClient(handler);
            _httpClient.Timeout = TimeSpan.FromSeconds(60); 
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("FacturasSRI-Client/1.0");
        }

        public async Task<string> EnviarRecepcionAsync(byte[] xmlFirmadoBytes)
        {
            string base64Xml = Convert.ToBase64String(xmlFirmadoBytes);

            // XML Envuelto en SOAP 1.1
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
                _logger.LogInformation("Enviando solicitud de recepción al SRI...");
                
                HttpResponseMessage respuesta = await _httpClient.PostAsync(URL_RECEPCION_PRUEBAS, httpContent);

                // Aseguramos que si el server responde 500 (común en SOAP faults), leamos el contenido igual
                string respuestaSoap = await respuesta.Content.ReadAsStringAsync();

                _logger.LogInformation("==========================================");
                _logger.LogInformation("RESPUESTA SRI (RECEPCIÓN):");
                _logger.LogInformation("{Respuesta}", respuestaSoap);
                _logger.LogInformation("==========================================");

                return respuestaSoap;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ERROR CRÍTICO DE CONEXIÓN CON EL SRI (RECEPCIÓN)");
                throw; // Re-lanzamos para que el servicio lo maneje
            }
        }

        public async Task<string> ConsultarAutorizacionAsync(string claveAcceso)
        {
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
                _logger.LogInformation("Consultando autorización al SRI para la clave: {Clave}", claveAcceso);
                
                HttpResponseMessage respuesta = await _httpClient.PostAsync(URL_AUTORIZACION_PRUEBAS, httpContent);
                string respuestaSoap = await respuesta.Content.ReadAsStringAsync();

                _logger.LogInformation("==========================================");
                _logger.LogInformation("RESPUESTA SRI (AUTORIZACIÓN):");
                _logger.LogInformation("{Respuesta}", respuestaSoap);
                _logger.LogInformation("==========================================");

                return respuestaSoap;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ERROR CRÍTICO DE CONEXIÓN CON EL SRI (AUTORIZACIÓN)");
                throw;
            }
        }
    }
}