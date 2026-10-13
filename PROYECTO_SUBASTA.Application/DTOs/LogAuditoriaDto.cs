using System;

namespace PROYECTO_SUBASTA.Application.DTOs
{
    /// <summary>
    /// DTO para la transferencia de registros de auditoría y trazabilidad cronológica.
    /// </summary>
    public class LogAuditoriaDto
    {
        public int Id { get; set; }
        public int? UsuarioId { get; set; }
        public string Accion { get; set; } = string.Empty;
        public string Detalle { get; set; } = string.Empty;
        public DateTime FechaRegistro { get; set; }
    }
}
