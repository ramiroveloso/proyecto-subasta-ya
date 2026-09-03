using Microsoft.AspNetCore.Mvc;
using PROYECTO_SUBASTA.Entities;
using PROYECTO_SUBASTA.Repositories;

namespace PROYECTO_SUBASTA.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CategoriasController : ControllerBase
    {
        private readonly ICategoriaRepository _categoriaRepository;

        public CategoriasController(ICategoriaRepository categoriaRepository)
        {
            _categoriaRepository = categoriaRepository;
        }

        [HttpGet]
        public async Task<IActionResult> ObtenerTodas()
        {
            // Delegamos la consulta a la abstracción de persistencia para mantener el controlador desacoplado de los detalles de la base de datos.
            var categorias = await _categoriaRepository.ObtenerTodasAsync();

            // Retornamos una respuesta HTTP 200 encapsulando el resultado obtenido por la capa inferior.
            return Ok(categorias);
        }

        [HttpPost]
        public async Task<IActionResult> Crear([FromBody] Categoria categoria)
        {
            // Validamos los datos de entrada a nivel de contrato HTTP para proteger el sistema de estados inconsistentes.
            if (categoria == null)
            {
                // Respondemos con un código 400 para informar al cliente que la estructura de la petición es incorrecta.
                return BadRequest("Los datos de la categoría son inválidos.");
            }

            // Solicitamos al repositorio que prepare la persistencia de la nueva entidad en memoria/contexto.
            await _categoriaRepository.CrearAsync(categoria);

            // Consolidamos la transacción en la base de datos de manera explícita para asegurar la persistencia.
            await _categoriaRepository.GuardarCambiosAsync();

            // Devolvemos la entidad creada confirmando el éxito de la operación HTTP.
            return Ok(categoria);
        }
    }
}