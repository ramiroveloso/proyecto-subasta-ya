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
        [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ObtenerTodas()
        {
            var categorias = await _obtenerCategoriasHandler.HandleAsync(new ObtenerCategoriasQuery());
            return Ok(categorias);
        }

        /// <summary>
        /// Obtiene una categoría específica mediante su identificador numérico.
        /// </summary>
        /// <param name="id">Identificador numérico de la categoría.</param>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(CategoriaResponseDto), StatusCodes.Status200OK)]
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
                    Mensaje = "El identificador de la categoría debe ser mayor a cero."
                });
            }

            var categoria = await _obtenerCategoriaPorIdHandler.HandleAsync(new ObtenerCategoriaPorIdQuery { Id = id });
            if (categoria == null)
            {
                return NotFound(new ErrorResponseDto
                {
                    StatusCode = StatusCodes.Status404NotFound,
                    Mensaje = $"No se encontró la categoría con ID {id}."
                });
            }

            return Ok(categoria);
        }

        /// <summary>
        /// Registra una nueva categoría en la plataforma.
        /// </summary>
        /// <param name="dto">Datos de la nueva categoría.</param>
        [HttpPost]
        [ProducesResponseType(typeof(CategoriaResponseDto), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> Crear([FromBody] CrearCategoriaDto dto)
        {
            if (dto == null)
            {
                return BadRequest(new ErrorResponseDto
                {
                    StatusCode = StatusCodes.Status400BadRequest,
                    Mensaje = "Los datos de la categoría son obligatorios."
                });
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