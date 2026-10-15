using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PROYECTO_SUBASTA.Application.Commands.Categorias;
using PROYECTO_SUBASTA.Application.DTOs;
using PROYECTO_SUBASTA.Application.Queries.Categorias;

namespace PROYECTO_SUBASTA.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public class CategoriasController : ControllerBase
    {
        private readonly ObtenerCategoriasQueryHandler _obtenerCategoriasHandler;
        private readonly ObtenerCategoriaPorIdQueryHandler _obtenerCategoriaPorIdHandler;
        private readonly CrearCategoriaCommandHandler _crearCategoriaHandler;

        public CategoriasController(
            ObtenerCategoriasQueryHandler obtenerCategoriasHandler,
            ObtenerCategoriaPorIdQueryHandler obtenerCategoriaPorIdHandler,
            CrearCategoriaCommandHandler crearCategoriaHandler)
        {
            _obtenerCategoriasHandler = obtenerCategoriasHandler;
            _obtenerCategoriaPorIdHandler = obtenerCategoriaPorIdHandler;
            _crearCategoriaHandler = crearCategoriaHandler;
        }

        /// <summary>
        /// Recupera el catálogo completo de categorías disponibles en el sistema.
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<CategoriaResponseDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> ObtenerTodas()
        {
            var categorias = await _obtenerCategoriasHandler.HandleAsync(new ObtenerCategoriasQuery());
            return Ok(categorias);
        }

        /// <summary>
        /// Obtiene una categoría específica mediante su identificador numérico.
        /// </summary>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(CategoriaResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> ObtenerPorId(int id)
        {
            if (id <= 0)
            {
                return BadRequest(new ErrorResponse { StatusCode = 400, Mensaje = "El identificador de categoría debe ser mayor a cero.", Timestamp = System.DateTime.UtcNow });
            }

            var categoria = await _obtenerCategoriaPorIdHandler.HandleAsync(new ObtenerCategoriaPorIdQuery { Id = id });
            if (categoria == null)
            {
                return NotFound(new ErrorResponse { StatusCode = 404, Mensaje = $"No se encontró la categoría con ID {id}.", Timestamp = System.DateTime.UtcNow });
            }

            return Ok(categoria);
        }

        /// <summary>
        /// Registra una nueva categoría en la plataforma.
        /// </summary>
        [HttpPost]
        [ProducesResponseType(typeof(CategoriaResponseDto), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Crear([FromBody] CrearCategoriaDto dto)
        {
            if (dto == null)
            {
                return BadRequest(new ErrorResponse { StatusCode = 400, Mensaje = "Los datos de la categoría son inválidos.", Timestamp = System.DateTime.UtcNow });
            }

            var command = new CrearCategoriaCommand
            {
                Nombre = dto.Nombre,
                UrlIcono = dto.UrlIcono
            };

            var nuevaCategoria = await _crearCategoriaHandler.HandleAsync(command);
            return CreatedAtAction(nameof(ObtenerPorId), new { id = nuevaCategoria.Id }, nuevaCategoria);
        }
    }
}