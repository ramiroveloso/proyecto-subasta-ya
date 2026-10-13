using System;
using System.Collections.Generic;

namespace PROYECTO_SUBASTA.Application.DTOs
{
    /// <summary>
    /// Modelo unificado y tipado de respuesta para errores, fallos de validación y excepciones HTTP.
    /// </summary>
    public class ErrorResponseDto
    {
        public int StatusCode { get; set; }
        public string Mensaje { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public IDictionary<string, string[]>? Errores { get; set; }
    }
}
