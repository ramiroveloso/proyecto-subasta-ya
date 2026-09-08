using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using PROYECTO_SUBASTA.Application.UseCases;

namespace PROYECTO_SUBASTA.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UsuariosController : ControllerBase
    {
        private readonly UsuarioUseCases _usuarioUseCases;

        public UsuariosController(UsuarioUseCases usuarioUseCases)
        {
            _usuarioUseCases = usuarioUseCases;
        }

        // POST: api/Usuarios
        [HttpPost]
        public async Task<IActionResult> CrearUsuario([FromBody] CrearUsuarioDto dto)
        {
            try
            {
                var usuario = await _usuarioUseCases.CrearUsuarioConBilleteraAsync(dto.Nombre, dto.Email);
                return Ok(new { mensaje = "Usuario y billetera creados correctamente", usuarioId = usuario.Id });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }

    public class CrearUsuarioDto
    {
        public string Nombre { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
    }
}