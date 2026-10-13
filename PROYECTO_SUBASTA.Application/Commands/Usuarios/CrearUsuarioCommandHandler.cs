using System;
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

        public CrearUsuarioCommandHandler(
            IRepository<Usuario> usuarioRepository,
            IBilleteraRepository billeteraRepository,
            IUnitOfWork unitOfWork)
        {
            _usuarioRepository = usuarioRepository ?? throw new ArgumentNullException(nameof(usuarioRepository));
            _billeteraRepository = billeteraRepository ?? throw new ArgumentNullException(nameof(billeteraRepository));
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
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
                throw new ReglaNegocioException("El email del usuario es obligatorio.");
            }

            await _unitOfWork.BeginTransactionAsync();
            try
            {
                var usuario = new Usuario
                {
                    Nombre = command.Nombre.Trim(),
                    Email = command.Email.Trim(),
                    PasswordHash = "HASH_PRUEBA_123",
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
                await _unitOfWork.CommitTransactionAsync();

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
                await _unitOfWork.RollbackTransactionAsync();
                throw;
            }
        }
    }
}
