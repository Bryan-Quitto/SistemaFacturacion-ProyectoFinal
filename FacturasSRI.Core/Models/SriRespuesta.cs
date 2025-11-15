// En: FacturasSRI.Core/Models/SriRespuesta.cs

using System;
using System.Collections.Generic;

namespace FacturasSRI.Core.Models
{
    /// <summary>
    /// Representa un error individual reportado por el SRI.
    /// </summary>
    public class SriError
    {
        public string Identificador { get; set; } = string.Empty;
        public string Mensaje { get; set; } = string.Empty;
        public string InformacionAdicional { get; set; } = string.Empty;
        public string Tipo { get; set; } = string.Empty;
    }

    /// <summary>
    /// Representa la respuesta del servicio de Recepción.
    /// </summary>
    public class RespuestaRecepcion
    {
        public string Estado { get; set; } = string.Empty; // "RECIBIDA" o "DEVUELTA"
        public List<SriError> Errores { get; set; } = new List<SriError>();
    }

    /// <summary>
    /// Representa la respuesta del servicio de Autorización.
    /// </summary>
    public class RespuestaAutorizacion
    {
        public string Estado { get; set; } = string.Empty; // "AUTORIZADO" o "NO AUTORIZADO"
        public string NumeroAutorizacion { get; set; } = string.Empty;
        public DateTime? FechaAutorizacion { get; set; }
        public List<SriError> Errores { get; set; } = new List<SriError>();
    }
}