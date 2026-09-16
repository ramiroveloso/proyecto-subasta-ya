using System.ComponentModel.DataAnnotations;

namespace PROYECTO_SUBASTA.Application.DTOs
{
    public class CrearCategoriaDto
    {
        [Required(ErrorMessage = "El nombre de la categoría es obligatorio.")]
        [StringLength(100, MinimumLength = 3, ErrorMessage = "El nombre debe contener entre 3 y 100 caracteres.")]
        public string Nombre { get; set; } = string.Empty;

        public string UrlIcono { get; set; } = string.Empty;
    }

    public class CategoriaResponseDto
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string UrlIcono { get; set; } = string.Empty;
    }
}
