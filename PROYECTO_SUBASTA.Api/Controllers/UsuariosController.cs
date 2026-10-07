using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PROYECTO_SUBASTA.Application.Commands.Usuarios;
using PROYECTO_SUBASTA.Application.DTOs;
using PROYECTO_SUBASTA.Application.Queries.Usuarios;
using PROYECTO_SUBASTA.Application.UseCases;

namespace PROYECTO_SUBASTA.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class UsuariosController : ControllerBase
    {
        private readonly UsuarioUseCases _usuarioUseCases;
        private readonly ObtenerUsuariosQueryHandler _obtenerUsuariosHandler;
        private readonly ObtenerUsuarioPorIdQueryHandler _obtenerUsuarioPorIdHandler;
        private readonly IniciarSesionCommandHandler _iniciarSesionHandler;

        public UsuariosController(
            UsuarioUseCases usuarioUseCases,
            ObtenerUsuariosQueryHandler obtenerUsuariosHandler,
            ObtenerUsuarioPorIdQueryHandler obtenerUsuarioPorIdHandler,
            IniciarSesionCommandHandler iniciarSesionHandler)
        {
            _usuarioUseCases = usuarioUseCases;
            _obtenerUsuariosHandler = obtenerUsuariosHandler;
            _obtenerUsuarioPorIdHandler = obtenerUsuarioPorIdHandler;
            _iniciarSesionHandler = iniciarSesionHandler;
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
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> ObtenerPorId(int id)
        {
            var usuario = await _obtenerUsuarioPorIdHandler.HandleAsync(new ObtenerUsuarioPorIdQuery { Id = id });
            if (usuario == null)
            {
                return NotFound(new { mensaje = $"No se encontró el usuario con ID {id}." });
            }

            return Ok(usuario);
        }

        /// <summary>
        /// Registra un nuevo usuario creando automáticamente su billetera inicial asociada.
        /// </summary>
        [HttpPost]
        [ProducesResponseType(typeof(UsuarioResponseDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CrearUsuario([FromBody] CrearUsuarioDto dto)
        {
            var usuario = await _usuarioUseCases.CrearUsuarioConBilleteraAsync(dto);
            return CreatedAtAction(nameof(ObtenerPorId), new { id = usuario.Id }, usuario);
        }

        /// <summary>
        /// Inicia sesión autenticando al usuario por nombre de usuario o correo electrónico.
        /// </summary>
        [HttpPost("sesiones")]
        [ProducesResponseType(typeof(IniciarSesionResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> IniciarSesion([FromBody] IniciarSesionCommand command)
        {
            var respuesta = await _iniciarSesionHandler.HandleAsync(command);
            return Ok(respuesta);
        }
    }
}