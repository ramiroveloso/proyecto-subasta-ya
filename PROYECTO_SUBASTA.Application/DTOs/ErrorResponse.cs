using System;

namespace PROYECTO_SUBASTA.Application.DTOs
{
    /// <summary>
    /// Esquema uniforme para respuestas de error de la API.
    /// </summary>
    public class ErrorResponse
    {
        public int StatusCode { get; set; }
        public string Mensaje { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
    }
}
