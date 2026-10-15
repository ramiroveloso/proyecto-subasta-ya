using System.ComponentModel.DataAnnotations;

namespace PROYECTO_SUBASTA.Application.Commands.Usuarios
{
    public class CrearUsuarioCommand
    {
        [Required(ErrorMessage = "El nombre es obligatorio.")]
        public string Nombre { get; set; } = string.Empty;

        [Required(ErrorMessage = "El correo electrónico es obligatorio.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "La contraseña es obligatoria.")]
        [MinLength(4, ErrorMessage = "La contraseña debe tener al menos 4 caracteres.")]
        public string Password { get; set; } = string.Empty;
    }
}
