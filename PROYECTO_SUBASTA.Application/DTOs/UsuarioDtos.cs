using System;
using System.ComponentModel.DataAnnotations;

namespace PROYECTO_SUBASTA.Application.DTOs
{
    public class CrearUsuarioDto
    {
        [Required(ErrorMessage = "El nombre completo es obligatorio.")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "El nombre debe tener al menos 2 caracteres.")]
        public string Nombre { get; set; } = string.Empty;

        [Required(ErrorMessage = "El correo electrónico es obligatorio.")]
        [EmailAddress(ErrorMessage = "Ingrese un correo electrónico válido.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "La contraseña es obligatoria.")]
        [MinLength(4, ErrorMessage = "La contraseña debe tener al menos 4 caracteres.")]
        public string Password { get; set; } = string.Empty;
    }

    public class UsuarioResponseDto
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public DateTime FechaRegistro { get; set; }
    }

    public class ActividadesUsuarioDto
    {
        public List<ParticipacionSubastaDto> Participaciones { get; set; } = new();
        public List<PublicacionSubastaDto> Publicaciones { get; set; } = new();
        public MetricasActividadDto Metricas { get; set; } = new();
    }

    public class ParticipacionSubastaDto
    {
        public int SubastaId { get; set; }
        public string Titulo { get; set; } = string.Empty;
        public string UrlImagen { get; set; } = string.Empty;
        public string CategoriaNombre { get; set; } = string.Empty;
        public string Estado { get; set; } = string.Empty;
        public DateTime FechaFin { get; set; }
        public decimal MiMejorOferta { get; set; }
        public decimal OfertaLiderActual { get; set; }
        public bool EsLider { get; set; }
        public int? GanadorId { get; set; }
        public decimal? PrecioFinal { get; set; }
        public int CantidadMisPujas { get; set; }
        public int CantidadPujasTotal { get; set; }
    }

    public class PublicacionSubastaDto
    {
        public int SubastaId { get; set; }
        public string Titulo { get; set; } = string.Empty;
        public string UrlImagen { get; set; } = string.Empty;
        public string CategoriaNombre { get; set; } = string.Empty;
        public string Estado { get; set; } = string.Empty;
        public decimal PrecioBase { get; set; }
        public decimal OfertaLiderActual { get; set; }
        public int CantidadPujas { get; set; }
        public int? GanadorId { get; set; }
        public decimal? PrecioFinal { get; set; }
        public DateTime FechaFin { get; set; }
    }

    public class MetricasActividadDto
    {
        public int TotalPujas { get; set; }
        public int SubastasGanadas { get; set; }
        public int SubastasPublicadas { get; set; }
        public int SubastasVendidas { get; set; }
    }
}