using System.Linq;
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

        // GET: api/Usuarios
        [HttpGet]
        public async Task<IActionResult> ObtenerTodos()
        {
            var usuarios = await _usuarioUseCases.ObtenerTodosAsync();
            var dtos = usuarios.Select(u => new
            {
                u.Id,
                u.Nombre,
                u.Email,
                u.FechaRegistro
            });
            return Ok(dtos);
        }
        // POST: api/Usuarios/login
        [HttpPost("login")]
        public async Task<IActionResult> IniciarSesion([FromBody] LoginDto dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.Usuario))
            {
                return BadRequest(new { mensaje = "Debe ingresar un usuario o correo electrónico." });
            }

            var usuarios = await _usuarioUseCases.ObtenerTodosAsync();
            var input = dto.Usuario.Trim();

            var usuario = usuarios.FirstOrDefault(u =>
                string.Equals(u.Email, input, System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(u.Nombre, input, System.StringComparison.OrdinalIgnoreCase));

            if (usuario == null)
            {
                return Unauthorized(new { mensaje = "Credenciales incorrectas: usuario o correo no registrado." });
            }

            return Ok(new
            {
                mensaje = "Inicio de sesión exitoso.",
                usuario = new
                {
                    id = usuario.Id,
                    nombre = usuario.Nombre,
                    email = usuario.Email
                }
            });
        }
    }

    public class CrearUsuarioDto
    {
        public string Nombre { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
    }

    public class LoginDto
    {
        public string Usuario { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}