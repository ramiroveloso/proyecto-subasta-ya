using System;
using System.ComponentModel.DataAnnotations;
using PROYECTO_SUBASTA.Application.DTOs;

namespace PROYECTO_SUBASTA.Application.Commands.Usuarios
{
    public class IniciarSesionCommand
    {
        [Required(ErrorMessage = "Debe ingresar un usuario o correo electrónico.")]
        public string Usuario { get; set; } = string.Empty;

        public string Password { get; set; } = string.Empty;
    }

    public class IniciarSesionResponseDto
    {
        public string Mensaje { get; set; } = string.Empty;
        public UsuarioResponseDto Usuario { get; set; } = null!;
    }
}
