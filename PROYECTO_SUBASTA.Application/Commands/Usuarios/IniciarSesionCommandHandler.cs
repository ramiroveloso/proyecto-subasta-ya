using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PROYECTO_SUBASTA.Application.DTOs;
using PROYECTO_SUBASTA.Application.Exceptions;
using PROYECTO_SUBASTA.Application.Repositories;
using PROYECTO_SUBASTA.Domain.Entities;

namespace PROYECTO_SUBASTA.Application.Commands.Usuarios
{
    public class IniciarSesionCommandHandler
    {
        private readonly IRepository<Usuario> _usuarioRepository;

        public IniciarSesionCommandHandler(IRepository<Usuario> usuarioRepository)
        {
            _usuarioRepository = usuarioRepository ?? throw new ArgumentNullException(nameof(usuarioRepository));
        }

        public async Task<IniciarSesionResponseDto> HandleAsync(IniciarSesionCommand command, CancellationToken cancellationToken = default)
        {
            if (command == null || string.IsNullOrWhiteSpace(command.Usuario))
            {
                throw new ReglaNegocioException("Debe ingresar un usuario o correo electrónico.");
            }

            var input = command.Usuario.Trim();
            var usuarios = await _usuarioRepository.GetAllAsync();

            var usuario = usuarios.FirstOrDefault(u =>
                string.Equals(u.Email, input, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(u.Nombre, input, StringComparison.OrdinalIgnoreCase));

            if (usuario == null)
            {
                throw new UnauthorizedAccessException("Credenciales incorrectas: usuario o correo no registrado.");
            }

            return new IniciarSesionResponseDto
            {
                Mensaje = "Inicio de sesión exitoso.",
                Usuario = new UsuarioResponseDto
                {
                    Id = usuario.Id,
                    Nombre = usuario.Nombre,
                    Email = usuario.Email,
                    FechaRegistro = usuario.FechaRegistro
                }
            };
        }
    }
}
