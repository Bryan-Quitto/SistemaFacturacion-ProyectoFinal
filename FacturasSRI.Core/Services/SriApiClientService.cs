// En: FacturasSRI.Core/Services/SriApiClientService.cs

using System.Text;
using System.Threading.Tasks;
using System.Net.Http;
using System.Xml; // Necesario para limpiar el XML de respuesta

namespace FacturasSRI.Core.Services
{
    public class SriApiClientService
    {
        // URLs del Ambiente de Pruebas
        private const string URL_RECEPCION_PRUEBAS = "https://celcer.sri.gob.ec/comprobantes-electronicos-ws/RecepcionComprobantesOffline";
        private const string URL_AUTORIZACION_PRUEBAS = "https://celcer.sri.gob.ec/comprobantes-electronicos-ws/AutorizacionComprobantesOffline";

        // Usaremos un solo HttpClient estático para mejor rendimiento
        // En una app real, esto se inyectaría con IHttpClientFactory
        private static readonly HttpClient _httpClient = new HttpClient();

        /// <summary>
        /// Envía el XML firmado al Web Service de Recepción del SRI.
        /// </summary>
        /// <param name="xmlFirmado">El string completo del XML firmado (Paso 3).</param>
        /// <returns>El string XML de la respuesta del SRI.</returns>
        public async Task<string> EnviarRecepcionAsync(string xmlFirmado)
        {
            // 1. Construir el Sobre SOAP para la Recepción
            // El método se llama 'validarComprobante' y espera un 'xml' (en base64)
            string sobreSoap = $@"
                <soapenv:Envelope xmlns:soapenv=""http://schemas.xmlsoap.org/soap/envelope/"" xmlns:ec=""http://ec.gob.sri.ws.recepcion"">
                   <soapenv:Header/>
                   <soapenv:Body>
                      <ec:validarComprobante>
                         <xml>{Convert.ToBase64String(Encoding.UTF8.GetBytes(xmlFirmado))}</xml>
                      </ec:validarComprobante>
                   </soapenv:Body>
                </soapenv:Envelope>";

            // 2. Preparar la Petición HTTP
            var httpContent = new StringContent(sobreSoap, Encoding.UTF8, "text/xml");
            
            // 3. Enviar la Petición POST
            HttpResponseMessage respuesta = await _httpClient.PostAsync(URL_RECEPCION_PRUEBAS, httpContent);

            // 4. Leer la Respuesta
            string respuestaSoap = await respuesta.Content.ReadAsStringAsync();

            // 5. Devolver el XML de la respuesta
            return respuestaSoap;
        }

        /// <summary>
        /// Consulta el estado de un comprobante usando la Clave de Acceso.
        /// </summary>
        /// <param name="claveAcceso">La clave de acceso de 49 dígitos.</param>
        /// <returns>El string XML de la respuesta del SRI.</returns>
        public async Task<string> ConsultarAutorizacionAsync(string claveAcceso)
        {
            // 1. Construir el Sobre SOAP para la Autorización
            // El método se llama 'autorizacionComprobante' y espera 'claveAccesoComprobante'
            string sobreSoap = $@"
                <soapenv:Envelope xmlns:soapenv=""http://schemas.xmlsoap.org/soap/envelope/"" xmlns:ec=""http://ec.gob.sri.ws.autorizacion"">
                   <soapenv:Header/>
                   <soapenv:Body>
                      <ec:autorizacionComprobante>
                         <claveAccesoComprobante>{claveAcceso}</claveAccesoComprobante>
                      </ec:autorizacionComprobante>
                   </soapenv:Body>
                </soapenv:Envelope>";
            
            // 2. Preparar la Petición HTTP
            var httpContent = new StringContent(sobreSoap, Encoding.UTF8, "text/xml");

            // 3. Enviar la Petición POST
            HttpResponseMessage respuesta = await _httpClient.PostAsync(URL_AUTORIZACION_PRUEBAS, httpContent);

            // 4. Leer la Respuesta
            string respuestaSoap = await respuesta.Content.ReadAsStringAsync();

            // 5. Devolver el XML de la respuesta
            return respuestaSoap;
        }
    }
}