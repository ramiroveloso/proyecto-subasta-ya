using System;
using System.Collections.Generic;

namespace PROYECTO_SUBASTA.Application.DTOs
{
    /// <summary>
    /// DTO de respuesta integral para el historial de actividades de un usuario (participaciones, ventas y métricas).
    /// </summary>
    public class UsuarioActividadesResponseDto
    {
        public int UsuarioId { get; set; }
        public List<ParticipacionSubastaDto> Participaciones { get; set; } = new();
        public List<PublicacionSubastaDto> Publicaciones { get; set; } = new();
        public MetricasActividadDto Metricas { get; set; } = new();
    }

    /// <summary>
    /// Detalle de una subasta en la que el usuario ha participado pujando.
    /// </summary>
    public class ParticipacionSubastaDto
    {
        public int SubastaId { get; set; }
        public string Titulo { get; set; } = string.Empty;
        public string UrlImagen { get; set; } = string.Empty;
        public string CategoriaNombre { get; set; } = string.Empty;
        public decimal MiMayorOferta { get; set; }
        public decimal OfertaLiderActual { get; set; }
        public int CantidadOfertasUsuario { get; set; }
        public int TotalOfertasSubasta { get; set; }
        /// <summary>
        /// Estado de la participación del postor: LIDER, SUPERADO, GANADA, PERDIDA.
        /// </summary>
        public string EstadoParticipacion { get; set; } = string.Empty;
        public string EstadoSubasta { get; set; } = string.Empty;
        public DateTime FechaFin { get; set; }
    }

    /// <summary>
    /// Detalle de una subasta publicada por el usuario como vendedor.
    /// </summary>
    public class PublicacionSubastaDto
    {
        public int SubastaId { get; set; }
        public string Titulo { get; set; } = string.Empty;
        public string UrlImagen { get; set; } = string.Empty;
        public string CategoriaNombre { get; set; } = string.Empty;
        public decimal PrecioBase { get; set; }
        public decimal? MejorOferta { get; set; }
        public int CantidadPujas { get; set; }
        public int? GanadorId { get; set; }
        public string? GanadorNombre { get; set; }
        public string Estado { get; set; } = string.Empty;
        public DateTime FechaFin { get; set; }
    }

    /// <summary>
    /// Métricas cuantitativas del comportamiento del usuario en la plataforma.
    /// </summary>
    public class MetricasActividadDto
    {
        public int TotalPujasRealizadas { get; set; }
        public int TotalSubastasCreadas { get; set; }
        public int SubastasGanadas { get; set; }
        public int SubastasEnLiderazgo { get; set; }
    }
}
