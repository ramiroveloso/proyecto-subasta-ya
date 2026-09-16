using Microsoft.AspNetCore.Mvc;
using PROYECTO_SUBASTA.Domain.Entities;
using PROYECTO_SUBASTA.Application.UseCases;
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

        [HttpGet]
        public async Task<IActionResult> ObtenerTodas()
        {
            var categorias = await _categoriaUseCases.ObtenerTodasAsync();
            return Ok(categorias);
        }

        [HttpPost]
        public async Task<IActionResult> Crear([FromBody] Categoria categoria)
        {
            // Sin try-catch. Si el dominio rechaza la categoría con ArgumentException, 
            // el middleware global la atrapará y devolverá un 400 Bad Request automáticamente.
            var nuevaCategoria = await _categoriaUseCases.CrearAsync(categoria);
            return Ok(nuevaCategoria);
        }
    }
}