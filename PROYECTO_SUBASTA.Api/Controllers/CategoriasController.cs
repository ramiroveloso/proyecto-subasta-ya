using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PROYECTO_SUBASTA.Application.UseCases;
using PROYECTO_SUBASTA.Domain.Entities;

namespace PROYECTO_SUBASTA.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class CategoriasController : ControllerBase
    {
        private readonly CategoriaUseCases _categoriaUseCases;

        public CategoriasController(CategoriaUseCases categoriaUseCases)
        {
            _categoriaUseCases = categoriaUseCases;
        }

        /// <summary>
        /// Recupera el catálogo completo de categorías disponibles en el sistema.
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<Categoria>), StatusCodes.Status200OK)]
        public async Task<IActionResult> ObtenerTodas()
        {
            var categorias = await _categoriaUseCases.ObtenerTodasAsync();
            return Ok(categorias);
        }

        /// <summary>
        /// Obtiene una categoría específica mediante su identificador numérico.
        /// </summary>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(Categoria), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> ObtenerPorId(int id)
        {
            var categoria = await _categoriaUseCases.ObtenerPorIdAsync(id);
            if (categoria == null)
            {
                return NotFound(new { mensaje = $"No se encontró la categoría con ID {id}." });
            }

            return Ok(categoria);
        }

        /// <summary>
        /// Registra una nueva categoría en la plataforma.
        /// </summary>
        [HttpPost]
        [ProducesResponseType(typeof(Categoria), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Crear([FromBody] Categoria categoria)
        {
            if (categoria == null)
            {
                return BadRequest("Los datos de la categoría son inválidos.");
            }

            var nuevaCategoria = await _categoriaUseCases.CrearAsync(categoria);
            return CreatedAtAction(nameof(ObtenerPorId), new { id = nuevaCategoria.Id }, nuevaCategoria);
        }
    }
}