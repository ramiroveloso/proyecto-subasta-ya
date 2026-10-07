using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using PROYECTO_SUBASTA.Application.Commands.Usuarios;
using PROYECTO_SUBASTA.Application.DTOs;
using PROYECTO_SUBASTA.Application.Exceptions;
using PROYECTO_SUBASTA.Application.Repositories;
using PROYECTO_SUBASTA.Domain.Entities;

namespace PROYECTO_SUBASTA.Application.UseCases
{
    public class UsuarioUseCases
    {
        private readonly IRepository<Usuario> _usuarioRepository;
        private readonly IBilleteraRepository _billeteraRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IniciarSesionCommandHandler _iniciarSesionHandler;

        public UsuarioUseCases(
            IRepository<Usuario> usuarioRepository,
            IBilleteraRepository billeteraRepository,
            IUnitOfWork unitOfWork,
            IniciarSesionCommandHandler iniciarSesionHandler)
        {
            _usuarioRepository = usuarioRepository ?? throw new ArgumentNullException(nameof(usuarioRepository));
            _billeteraRepository = billeteraRepository ?? throw new ArgumentNullException(nameof(billeteraRepository));
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _iniciarSesionHandler = iniciarSesionHandler ?? throw new ArgumentNullException(nameof(iniciarSesionHandler));
        }

        public async Task<IEnumerable<Usuario>> ObtenerTodosAsync()
        {
            return await _usuarioRepository.GetAllAsync();
        }

        public async Task<Usuario?> ObtenerPorIdAsync(int id)
        {
            return await _usuarioRepository.GetByIdAsync(id);
        }

        public async Task<IniciarSesionResponseDto> IniciarSesionAsync(string identificador)
        {
            return await _iniciarSesionHandler.HandleAsync(new IniciarSesionCommand { Usuario = identificador });
        }

        public async Task<Usuario> CrearUsuarioConBilleteraAsync(string nombre, string email)
        {
            var dto = new CrearUsuarioDto { Nombre = nombre, Email = email };
            var response = await CrearUsuarioConBilleteraAsync(dto);

            return new Usuario
            {
                Id = response.Id,
                Nombre = response.Nombre,
                Email = response.Email
            };
        }

        public async Task<UsuarioResponseDto> CrearUsuarioConBilleteraAsync(CrearUsuarioDto dto)
        {
            if (dto == null)
            {
                throw new ReglaNegocioException("Los datos del usuario son obligatorios.");
            }

            if (string.IsNullOrWhiteSpace(dto.Nombre))
            {
                throw new ReglaNegocioException("El nombre del usuario es obligatorio.");
            }

            if (string.IsNullOrWhiteSpace(dto.Email))
            {
                throw new ReglaNegocioException("El email del usuario es obligatorio.");
            }

            await _unitOfWork.BeginTransactionAsync();
            try
            {
                var usuario = new Usuario
                {
                    Nombre = dto.Nombre.Trim(),
                    Email = dto.Email.Trim(),
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