using Microsoft.AspNetCore.Mvc;
using PROYECTO_SUBASTA.Entities;
using PROYECTO_SUBASTA.Repositories;
using System;
using System.Threading.Tasks;

namespace PROYECTO_SUBASTA.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UsuariosController : ControllerBase
    {
        private readonly IRepository<Usuario> _usuarioRepository;
        private readonly IBilleteraRepository _billeteraRepository;

        public UsuariosController(
            IRepository<Usuario> usuarioRepository,
            IBilleteraRepository billeteraRepository)
        {
            _usuarioRepository = usuarioRepository;
            _billeteraRepository = billeteraRepository;
        }

        // POST: api/Usuarios
        [HttpPost]
        public async Task<IActionResult> CrearUsuario([FromBody] CrearUsuarioDto dto)
        {
            var usuario = new Usuario
            {
                Nombre = dto.Nombre,
                Email = dto.Email,
                PasswordHash = "HASH_PRUEBA_123", // Reemplazar luego con hashing real (BCrypt/Identity)
                FechaRegistro = DateTime.UtcNow
            };

            await _usuarioRepository.AddAsync(usuario);
            await _usuarioRepository.SaveChangesAsync();

            // Creación automática de la Billetera para el nuevo usuario
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

            return Ok(new { mensaje = "Usuario y billetera creados correctamente", usuarioId = usuario.Id });
        }
    }

    public class CrearUsuarioDto
    {
        public string Nombre { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
    }
}
