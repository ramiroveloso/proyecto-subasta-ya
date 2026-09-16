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
    }

    public class UsuarioResponseDto
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public DateTime FechaRegistro { get; set; }
    }
}