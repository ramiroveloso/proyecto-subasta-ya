using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using PROYECTO_SUBASTA.Application.UseCases;

namespace PROYECTO_SUBASTA.Api.Controllers
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
            // Sin try-catch. Si ocurre una ArgumentException por reglas de negocio, 
            // el middleware global la interceptará y responderá con un HTTP 400 Bad Request.
            var usuario = await _usuarioUseCases.CrearUsuarioConBilleteraAsync(dto.Nombre, dto.Email);
            return Ok(new { mensaje = "Usuario y billetera creados correctamente", usuarioId = usuario.Id });
        }
    }

    public class CrearUsuarioDto
    {
        public string Nombre { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
    }
}