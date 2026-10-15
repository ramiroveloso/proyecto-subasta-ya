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
        private readonly IUsuarioRepository _usuarioRepository;
        private readonly Services.IPasswordHasher _passwordHasher;

        public IniciarSesionCommandHandler(IUsuarioRepository usuarioRepository, Services.IPasswordHasher passwordHasher)
        {
            _usuarioRepository = usuarioRepository ?? throw new ArgumentNullException(nameof(usuarioRepository));
            _passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
        }

        public async Task<IniciarSesionResponseDto> HandleAsync(IniciarSesionCommand command, CancellationToken cancellationToken = default)
        {
            if (command == null || string.IsNullOrWhiteSpace(command.Usuario))
            {
                throw new ReglaNegocioException("Debe ingresar un usuario o correo electrónico.");
            }

            var input = command.Usuario.Trim();
            var usuario = await _usuarioRepository.ObtenerPorEmailONombreAsync(input, cancellationToken);

            if (usuario == null)
            {
                throw new UnauthorizedAccessException("Credenciales incorrectas.");
            }

            if (string.IsNullOrEmpty(command.Password) || !_passwordHasher.Verificar(command.Password, usuario.PasswordHash))
            {
                throw new UnauthorizedAccessException("Credenciales incorrectas.");
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
