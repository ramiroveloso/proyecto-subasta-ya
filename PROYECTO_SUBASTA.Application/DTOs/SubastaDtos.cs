using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace PROYECTO_SUBASTA.Application.DTOs
{
    /// <summary>
    /// DTO para la creacion y parametrizacion de una nueva subasta con validaciones declarativas estrictas.
    /// </summary>
    public class CrearSubastaDto : IValidatableObject
    {
        [Required(ErrorMessage = "El titulo de la subasta es obligatorio.")]
        [StringLength(150, MinimumLength = 3, ErrorMessage = "El titulo debe tener entre 3 y 150 caracteres.")]
        public string Titulo { get; set; } = string.Empty;

        [StringLength(1000, ErrorMessage = "La descripcion no puede superar los 1000 caracteres.")]
        public string Descripcion { get; set; } = string.Empty;

        [Required(ErrorMessage = "La URL de la imagen es obligatoria.")]
        [Url(ErrorMessage = "La URL de la imagen debe ser una direccion web valida.")]
        public string UrlImagen { get; set; } = string.Empty;

        [Range(0.01, double.MaxValue, ErrorMessage = "El precio base debe ser mayor a cero.")]
        public decimal PrecioBase { get; set; }

        [Range(0.01, double.MaxValue, ErrorMessage = "El incremento minimo debe ser mayor a cero.")]
        public decimal IncrementoMinimo { get; set; }

        [Required(ErrorMessage = "La fecha de inicio es obligatoria.")]
        public DateTime FechaInicio { get; set; }

        [Required(ErrorMessage = "La fecha de fin es obligatoria.")]
        public DateTime FechaFin { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Debe especificar una categoria valida.")]
        public int CategoriaId { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Debe especificar un vendedor valido.")]
        public int VendedorId { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (string.IsNullOrWhiteSpace(Titulo))
            {
                yield return new ValidationResult("El titulo de la subasta no puede estar vacio ni contener solo espacios.", new[] { nameof(Titulo) });
            }

            if (!string.IsNullOrWhiteSpace(UrlImagen))
            {
                if (!Uri.TryCreate(UrlImagen, UriKind.Absolute, out var uriResult) ||
                    (uriResult.Scheme != Uri.UriSchemeHttp && uriResult.Scheme != Uri.UriSchemeHttps))
                {
                    yield return new ValidationResult(
                        "La URL de la imagen debe ser una URL absoluta con protocolo http o https.",
                        new[] { nameof(UrlImagen) });
                }
            }

            if (FechaFin <= FechaInicio)
            {
                yield return new ValidationResult(
                    "La fecha de finalizacion debe ser posterior a la fecha de inicio.",
                    new[] { nameof(FechaFin) });
            }

            if (FechaFin <= DateTime.UtcNow)
            {
                yield return new ValidationResult(
                    "La fecha de finalizacion debe ser una fecha futura.",
                    new[] { nameof(FechaFin) });
            }
        }
    }

    /// <summary>
    /// DTO de lectura completo para exponer el estado de la subasta y su oferta mas alta.
    /// </summary>
    public class SubastaResponseDto
    {
        public int Id { get; set; }
        public string Titulo { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        public string UrlImagen { get; set; } = string.Empty;
        public decimal PrecioBase { get; set; }
        public decimal IncrementoMinimo { get; set; }
        public DateTime FechaInicio { get; set; }
        public DateTime FechaFin { get; set; }
        public string Estado { get; set; } = string.Empty;
        public int CategoriaId { get; set; }
        public string CategoriaNombre { get; set; } = string.Empty;
        public int VendedorId { get; set; }
        public int? GanadorId { get; set; }
        public decimal? PrecioFinal { get; set; }
        public uint Version { get; set; }
        public List<PujaItemDto> Pujas { get; set; } = new();
    }

    /// <summary>
    /// DTO de lectura para representar una oferta en el historial anonimizado.
    /// </summary>
    public class PujaItemDto
    {
        public int Id { get; set; }
        public int SubastaId { get; set; }
        public int UsuarioId { get; set; }
        public decimal Monto { get; set; }
        public DateTime FechaCreacion { get; set; }
        public string PostorAnonimo { get; set; } = string.Empty;
    }
}
