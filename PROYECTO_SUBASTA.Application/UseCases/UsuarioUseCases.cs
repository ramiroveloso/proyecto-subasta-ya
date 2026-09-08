using System;
using System.Threading.Tasks;
using PROYECTO_SUBASTA.Domain.Entities;
using PROYECTO_SUBASTA.Application.Repositories;

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

        public async Task<Usuario> CrearUsuarioConBilleteraAsync(string nombre, string email)
        {
            if (string.IsNullOrWhiteSpace(nombre))
            {
                throw new ArgumentException("El nombre del usuario es obligatorio.");
            }

            if (string.IsNullOrWhiteSpace(email))
            {
                throw new ArgumentException("El email del usuario es obligatorio.");
            }

            var usuario = new Usuario
            {
                Nombre = nombre,
                Email = email,
                PasswordHash = "HASH_PRUEBA_123", // Pendiente integrar hashing real (BCrypt/Identity)
                FechaRegistro = DateTime.UtcNow
            };

            await _usuarioRepository.AddAsync(usuario);
            await _usuarioRepository.SaveChangesAsync();

            // Creación automática de la billetera asociada al usuario
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

            return usuario;
        }
    }
}