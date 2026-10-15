using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PROYECTO_SUBASTA.Application.Commands.Usuarios;
using PROYECTO_SUBASTA.Application.DTOs;
using PROYECTO_SUBASTA.Application.Queries.Usuarios;

namespace PROYECTO_SUBASTA.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public class UsuariosController : ControllerBase
    {
        private readonly CrearUsuarioCommandHandler _crearUsuarioHandler;
        private readonly ObtenerUsuariosQueryHandler _obtenerUsuariosHandler;
        private readonly ObtenerUsuarioPorIdQueryHandler _obtenerUsuarioPorIdHandler;
        private readonly IniciarSesionCommandHandler _iniciarSesionHandler;
        private readonly ObtenerActividadesUsuarioQueryHandler _obtenerActividadesHandler;

        public UsuariosController(
            CrearUsuarioCommandHandler crearUsuarioHandler,
            ObtenerUsuariosQueryHandler obtenerUsuariosHandler,
            ObtenerUsuarioPorIdQueryHandler obtenerUsuarioPorIdHandler,
            IniciarSesionCommandHandler iniciarSesionHandler,
            ObtenerActividadesUsuarioQueryHandler obtenerActividadesHandler)
        {
            _crearUsuarioHandler = crearUsuarioHandler;
            _obtenerUsuariosHandler = obtenerUsuariosHandler;
            _obtenerUsuarioPorIdHandler = obtenerUsuarioPorIdHandler;
            _iniciarSesionHandler = iniciarSesionHandler;
            _obtenerActividadesHandler = obtenerActividadesHandler;
        }

        /// <summary>
        /// Recupera el listado completo de usuarios registrados.
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<UsuarioResponseDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> ObtenerTodos()
        {
            var usuarios = await _obtenerUsuariosHandler.HandleAsync(new ObtenerUsuariosQuery());
            return Ok(usuarios);
        }

        /// <summary>
        /// Obtiene un usuario específico mediante su identificador.
        /// </summary>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(UsuarioResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> ObtenerPorId(int id)
        {
            if (id <= 0)
            {
                return BadRequest(new ErrorResponse { StatusCode = 400, Mensaje = "El identificador de usuario debe ser mayor a cero.", Timestamp = System.DateTime.UtcNow });
            }

            var usuario = await _obtenerUsuarioPorIdHandler.HandleAsync(new ObtenerUsuarioPorIdQuery { Id = id });
            if (usuario == null)
            {
                return NotFound(new ErrorResponse { StatusCode = 404, Mensaje = $"No se encontró el usuario con ID {id}.", Timestamp = System.DateTime.UtcNow });
            }

            return Ok(usuario);
        }

        /// <summary>
        /// Registra un nuevo usuario creando automáticamente su billetera inicial asociada.
        /// </summary>
        [HttpPost]
        [ProducesResponseType(typeof(UsuarioResponseDto), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CrearUsuario([FromBody] CrearUsuarioDto dto)
        {
            var command = new CrearUsuarioCommand
            {
                Nombre = dto.Nombre,
                Email = dto.Email,
                Password = dto.Password
            };

            var usuario = await _crearUsuarioHandler.HandleAsync(command);
            return CreatedAtAction(nameof(ObtenerPorId), new { id = usuario.Id }, usuario);
        }

        /// <summary>
        /// Inicia sesión autenticando al usuario por nombre de usuario o correo electrónico.
        /// </summary>
        [HttpPost("sesiones")]
        [ProducesResponseType(typeof(IniciarSesionResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> IniciarSesion([FromBody] IniciarSesionCommand command)
        {
            var respuesta = await _iniciarSesionHandler.HandleAsync(command);
            return Ok(respuesta);
        }

        /// <summary>
        /// Obtiene el resumen consolidado de actividades (participaciones, publicaciones y métricas) de un usuario.
        /// </summary>
        [HttpGet("{id}/actividades")]
        [ProducesResponseType(typeof(ActividadesUsuarioDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> ObtenerActividades(int id)
        {
            if (id <= 0)
            {
                return BadRequest(new ErrorResponse { StatusCode = 400, Mensaje = "El identificador de usuario debe ser mayor a cero.", Timestamp = System.DateTime.UtcNow });
            }

            var actividades = await _obtenerActividadesHandler.HandleAsync(new ObtenerActividadesUsuarioQuery { UsuarioId = id });
            return Ok(actividades);
        }
    }
}