using System.Text;
using System.Threading.Tasks;
using System.Net.Http;
using System;
using Microsoft.Extensions.Logging;

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
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(30); 
            _logger = logger;
        }

        public async Task<string> EnviarRecepcionAsync(byte[] xmlFirmadoBytes)
{
    string base64Xml = Convert.ToBase64String(xmlFirmadoBytes);

    string sobreSoap = $"<soapenv:Envelope xmlns:soapenv=\"http://schemas.xmlsoap.org/soap/envelope/\" xmlns:ec=\"http://ec.gob.sri.ws.recepcion\"><soapenv:Header/><soapenv:Body><ec:validarComprobante><xml>{base64Xml}</xml></ec:validarComprobante></soapenv:Body></soapenv:Envelope>";

    var httpContent = new StringContent(sobreSoap, Encoding.UTF8, "text/xml");
    
    try 
    {
        using (var requestMessage = new HttpRequestMessage(HttpMethod.Post, URL_RECEPCION_PRUEBAS))
        {
            requestMessage.Content = httpContent;
            
            _logger.LogInformation("Enviando solicitud de recepción al SRI...");

            HttpResponseMessage respuesta = await _httpClient.SendAsync(requestMessage);

            string respuestaSoap = await respuesta.Content.ReadAsStringAsync();

            _logger.LogInformation("==========================================");
            _logger.LogInformation("RESPUESTA SRI (RECEPCIÓN):");
            _logger.LogInformation("{Respuesta}", respuestaSoap);
            _logger.LogInformation("==========================================");

            return respuestaSoap;
        }
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "ERROR CRÍTICO DE CONEXIÓN CON EL SRI (RECEPCIÓN)");
        throw;
    }
}

        public async Task<string> ConsultarAutorizacionAsync(string claveAcceso)
        {
            // Igual aquí, SOAP en una sola línea
            string sobreSoap = $"<soapenv:Envelope xmlns:soapenv=\"http://schemas.xmlsoap.org/soap/envelope/\" xmlns:ec=\"http://ec.gob.sri.ws.autorizacion\"><soapenv:Header/><soapenv:Body><ec:autorizacionComprobante><claveAccesoComprobante>{claveAcceso}</claveAccesoComprobante></ec:autorizacionComprobante></soapenv:Body></soapenv:Envelope>";
            
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