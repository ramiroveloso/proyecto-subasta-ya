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
    public class UsuariosController : ControllerBase
    {
        private readonly ObtenerUsuariosQueryHandler _obtenerUsuariosHandler;
        private readonly ObtenerUsuarioPorIdQueryHandler _obtenerUsuarioPorIdHandler;
        private readonly IniciarSesionCommandHandler _iniciarSesionHandler;
        private readonly CrearUsuarioCommandHandler _crearUsuarioHandler;
        private readonly ObtenerActividadesUsuarioQueryHandler _obtenerActividadesHandler;

        public UsuariosController(
            ObtenerUsuariosQueryHandler obtenerUsuariosHandler,
            ObtenerUsuarioPorIdQueryHandler obtenerUsuarioPorIdHandler,
            IniciarSesionCommandHandler iniciarSesionHandler,
            CrearUsuarioCommandHandler crearUsuarioHandler,
            ObtenerActividadesUsuarioQueryHandler obtenerActividadesHandler)
        {
            _obtenerUsuariosHandler = obtenerUsuariosHandler;
            _obtenerUsuarioPorIdHandler = obtenerUsuarioPorIdHandler;
            _iniciarSesionHandler = iniciarSesionHandler;
            _crearUsuarioHandler = crearUsuarioHandler;
            _obtenerActividadesHandler = obtenerActividadesHandler;
        }

        /// <summary>
        /// Recupera el listado completo de usuarios registrados.
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<UsuarioResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ObtenerTodos()
        {
            var usuarios = await _obtenerUsuariosHandler.HandleAsync(new ObtenerUsuariosQuery());
            return Ok(usuarios);
        }

        /// <summary>
        /// Obtiene un usuario específico mediante su identificador.
        /// </summary>
        /// <param name="id">Identificador único del usuario.</param>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(UsuarioResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ObtenerPorId(int id)
        {
            if (id <= 0)
            {
                return BadRequest(new ErrorResponseDto
                {
                    StatusCode = StatusCodes.Status400BadRequest,
                    Mensaje = "El identificador de usuario debe ser mayor a cero."
                });
            }

            var usuario = await _obtenerUsuarioPorIdHandler.HandleAsync(new ObtenerUsuarioPorIdQuery { Id = id });
            if (usuario == null)
            {
                return NotFound(new ErrorResponseDto
                {
                    StatusCode = StatusCodes.Status404NotFound,
                    Mensaje = $"No se encontró el usuario con ID {id}."
                });
            }

            return Ok(usuario);
        }

        /// <summary>
        /// Consulta el historial completo de actividades de un usuario: participaciones en ofertas, publicaciones y métricas.
        /// </summary>
        /// <param name="id">Identificador único del usuario.</param>
        [HttpGet("{id}/actividades")]
        [ProducesResponseType(typeof(UsuarioActividadesResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ObtenerActividades(int id)
        {
            if (id <= 0)
            {
                return BadRequest(new ErrorResponseDto
                {
                    StatusCode = StatusCodes.Status400BadRequest,
                    Mensaje = "El identificador de usuario debe ser mayor a cero."
                });
            }

            var actividades = await _obtenerActividadesHandler.HandleAsync(new ObtenerActividadesUsuarioQuery { UsuarioId = id });
            if (actividades == null)
            {
                return NotFound(new ErrorResponseDto
                {
                    StatusCode = StatusCodes.Status404NotFound,
                    Mensaje = $"No se encontró el usuario con ID {id}."
                });
            }

            return Ok(actividades);
        }

        /// <summary>
        /// Registra un nuevo usuario creando automáticamente su billetera inicial asociada con saldo cero.
        /// </summary>
        /// <param name="dto">Datos del nuevo usuario.</param>
        [HttpPost]
        [ProducesResponseType(typeof(UsuarioResponseDto), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> CrearUsuario([FromBody] CrearUsuarioDto dto)
        {
            if (dto == null)
            {
                return BadRequest(new ErrorResponseDto
                {
                    StatusCode = StatusCodes.Status400BadRequest,
                    Mensaje = "Los datos del usuario son obligatorios."
                });
            }

            var command = new CrearUsuarioCommand
            {
                Nombre = dto.Nombre,
                Email = dto.Email
            };

            var usuario = await _crearUsuarioHandler.HandleAsync(command);
            return CreatedAtAction(nameof(ObtenerPorId), new { id = usuario.Id }, usuario);
        }

        /// <summary>
        /// Inicia sesión autenticando al usuario por nombre de usuario o correo electrónico.
        /// </summary>
        /// <param name="command">Credenciales del usuario.</param>
        [HttpPost("sesiones")]
        [ProducesResponseType(typeof(IniciarSesionResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> IniciarSesion([FromBody] IniciarSesionCommand command)
        {
            if (command == null || string.IsNullOrWhiteSpace(command.Usuario))
            {
                return BadRequest(new ErrorResponseDto
                {
                    StatusCode = StatusCodes.Status400BadRequest,
                    Mensaje = "Debe ingresar un usuario o correo electrónico válido."
                });
            }

            var respuesta = await _iniciarSesionHandler.HandleAsync(command);
            return Ok(respuesta);
        }
    }
}