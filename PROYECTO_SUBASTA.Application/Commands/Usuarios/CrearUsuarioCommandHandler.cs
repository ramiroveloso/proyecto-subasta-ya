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
    public class CrearUsuarioCommandHandler
    {
        private readonly IRepository<Usuario> _usuarioRepository;
        private readonly IBilleteraRepository _billeteraRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly Services.IPasswordHasher _passwordHasher;

        public CrearUsuarioCommandHandler(
            IRepository<Usuario> usuarioRepository,
            IBilleteraRepository billeteraRepository,
            IUnitOfWork unitOfWork,
            Services.IPasswordHasher passwordHasher)
        {
            _usuarioRepository = usuarioRepository ?? throw new ArgumentNullException(nameof(usuarioRepository));
            _billeteraRepository = billeteraRepository ?? throw new ArgumentNullException(nameof(billeteraRepository));
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
        }

        public async Task<UsuarioResponseDto> HandleAsync(CrearUsuarioCommand command, CancellationToken cancellationToken = default)
        {
            if (command == null)
            {
                throw new ReglaNegocioException("Los datos del usuario son obligatorios.");
            }

            if (string.IsNullOrWhiteSpace(command.Nombre))
            {
                throw new ReglaNegocioException("El nombre del usuario es obligatorio.");
            }

            if (string.IsNullOrWhiteSpace(command.Email))
            {
                throw new ReglaNegocioException("El correo electrónico del usuario es obligatorio.");
            }

            if (string.IsNullOrWhiteSpace(command.Password) || command.Password.Length < 4)
            {
                throw new ReglaNegocioException("La contraseña es obligatoria y debe tener al menos 4 caracteres.");
            }

            var emailNormalizado = command.Email.Trim();
            var usuarios = await _usuarioRepository.GetAllAsync();
            if (usuarios.Any(u => string.Equals(u.Email, emailNormalizado, StringComparison.OrdinalIgnoreCase)))
            {
                throw new ReglaNegocioException($"Ya existe un usuario registrado con el correo electrónico '{emailNormalizado}'.");
            }

            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            try
            {
                var usuario = new Usuario
                {
                    Nombre = command.Nombre.Trim(),
                    Email = emailNormalizado,
                    PasswordHash = _passwordHasher.Hash(command.Password),
                    FechaRegistro = DateTime.UtcNow
                };

                await _usuarioRepository.AddAsync(usuario);
                await _usuarioRepository.SaveChangesAsync();

                var billetera = new Billetera
                {
                    UsuarioId = usuario.Id,
                    SaldoTotal = 0,
                    SaldoRetenido = 0,
                    SaldoDisponible = 0,
                    Version = 1
                };

                await _billeteraRepository.AddAsync(billetera);
                await _unitOfWork.CommitTransactionAsync(cancellationToken);

                return new UsuarioResponseDto
                {
                    Id = usuario.Id,
                    Nombre = usuario.Nombre,
                    Email = usuario.Email,
                    FechaRegistro = usuario.FechaRegistro
                };
            }
            catch
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                throw;
            }
        }
    }
}
