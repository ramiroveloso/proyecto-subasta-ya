using System;
using System.Threading.Tasks;
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

        public UsuarioUseCases(
            IRepository<Usuario> usuarioRepository,
            IBilleteraRepository billeteraRepository)
        {
            _usuarioRepository = usuarioRepository;
            _billeteraRepository = billeteraRepository;
        }

        // Firma requerida por los controladores
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

        // Firma desacoplada con DTO
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
            await _billeteraRepository.SaveChangesAsync();

            return new UsuarioResponseDto
            {
                Id = usuario.Id,
                Nombre = usuario.Nombre,
                Email = usuario.Email,
                FechaRegistro = usuario.FechaRegistro
            };
        }
    }
}