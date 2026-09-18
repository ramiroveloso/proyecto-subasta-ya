using Microsoft.AspNetCore.Mvc;
using PROYECTO_SUBASTA.Domain.Entities;
using PROYECTO_SUBASTA.Application.UseCases;
using System.Linq;
using System.Threading.Tasks;

namespace PROYECTO_SUBASTA.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CategoriasController : ControllerBase
    {
        private readonly CategoriaUseCases _categoriaUseCases;

        public CategoriasController(CategoriaUseCases categoriaUseCases)
        {
            _categoriaUseCases = categoriaUseCases;
        }

        // GET: api/Categorias
        [HttpGet]
        public async Task<IActionResult> ObtenerTodas()
        {
            var categorias = await _categoriaUseCases.ObtenerTodasAsync();
            return Ok(categorias);
        }

        // GET: api/Categorias/1
        [HttpGet("{id}")]
        public async Task<IActionResult> ObtenerPorId(int id)
        {
            var categorias = await _categoriaUseCases.ObtenerTodasAsync();
            var categoria = categorias.FirstOrDefault(c => c.Id == id);
            if (categoria == null)
            {
                return NotFound(new { mensaje = $"No se encontró la categoría con ID {id}." });
            }

            return Ok(categoria);
        }

        // POST: api/Categorias
        [HttpPost]
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