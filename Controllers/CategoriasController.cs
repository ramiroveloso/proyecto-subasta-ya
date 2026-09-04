using Microsoft.AspNetCore.Mvc;
using PROYECTO_SUBASTA.Entities;
using PROYECTO_SUBASTA.UseCases; // Cambiamos la referencia a la capa de UseCases

namespace PROYECTO_SUBASTA.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CategoriasController : ControllerBase
    {
        private readonly CategoriaUseCases _categoriaUseCases;

        // Inyectamos el servicio de aplicación (Caso de Uso) en lugar de la capa de acceso a datos.
        public CategoriasController(CategoriaUseCases categoriaUseCases)
        {
            _categoriaUseCases = categoriaUseCases;
        }

        [HttpGet]
        public async Task<IActionResult> ObtenerTodas()
        {
            // Delegamos la obtención de los datos a la capa de negocio.
            var categorias = await _categoriaUseCases.ObtenerTodasAsync();

            // Retornamos una respuesta HTTP 200 encapsulando el resultado.
            return Ok(categorias);
        }

        [HttpPost]
        public async Task<IActionResult> Crear([FromBody] Categoria categoria)
        {
            // Validación inicial a nivel de contrato HTTP.
            if (categoria == null)
            {
                return BadRequest("Los datos de la categoría son inválidos.");
            }

            try
            {
                // Delegamos la creación y persistencia de la categoría, junto con sus reglas de validación estricta, al caso de uso.
                var nuevaCategoria = await _categoriaUseCases.CrearAsync(categoria);

                // Devolvemos la entidad creada confirmando el éxito de la operación HTTP.
                return Ok(nuevaCategoria);
            }
            catch (ArgumentException ex)
            {
                // Capturamos violaciones a las reglas de negocio del dominio (ej. nombre vacío) y respondemos con un estado 400.
                return BadRequest(ex.Message);
            }
        }
    }
}