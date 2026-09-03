using Microsoft.AspNetCore.Mvc;
using PROYECTO_SUBASTA.Entities;
using PROYECTO_SUBASTA.UseCases;

namespace PROYECTO_SUBASTA.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SubastasController : ControllerBase
    {
        private readonly SubastaUseCases _subastaUseCases;

        public SubastasController(SubastaUseCases subastaUseCases)
        {
            _subastaUseCases = subastaUseCases;
        }

        [HttpGet]
        public async Task<IActionResult> ObtenerActivas()
        {
            // Delegamos la obtención de las subastas activas al caso de uso para mantener al controlador libre de reglas lógicas.
            var subastas = await _subastaUseCases.ObtenerActivasAsync();

            // Empaquetamos la colección resultante en una respuesta HTTP 200 OK para el cliente.
            return Ok(subastas);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> ObtenerPorId(int id)
        {
            // Solicitamos al caso de uso la búsqueda del recurso específico garantizando la separación de capas.
            var subasta = await _subastaUseCases.ObtenerPorIdAsync(id);

            // Comprobamos si la entidad existe en el sistema para evitar respuestas nulas hacia la capa de presentación.
            if (subasta == null)
            {
                // Devolvemos un código de estado HTTP 404 informando que el identificador consultado no existe.
                return NotFound($"No se encontró la subasta con el ID {id}.");
            }

            // Retornamos el recurso encontrado con un estado de éxito HTTP estándar.
            return Ok(subasta);
        }

        [HttpPost]
        public async Task<IActionResult> Crear([FromBody] Subasta subasta)
        {
            // Validamos preliminarmente que el cuerpo de la petición HTTP no llegue vacío.
            if (subasta == null)
            {
                // Rechazamos la solicitud con un estado HTTP 400 por estructura de datos inválida.
                return BadRequest("Los datos de la subasta son inválidos.");
            }

            // Intentamos ejecutar la lógica de negocio a través del caso de uso, capturando posibles excepciones de validación.
            try
            {
                // Delegamos la creación de la subasta y sus reglas de validación estricta al servicio de aplicación.
                await _subastaUseCases.CrearAsync(subasta);

                // Respondemos con un código HTTP 201 Created adjuntando la ruta de acceso al nuevo recurso generado.
                return CreatedAtAction(nameof(ObtenerPorId), new { id = subasta.Id }, subasta);
            }
            catch (ArgumentException ex)
            {
                // Capturamos violaciones a las reglas de negocio del dominio y respondemos con un estado 400 Bad Request explicativo.
                return BadRequest(ex.Message);
            }
        }
    }
}