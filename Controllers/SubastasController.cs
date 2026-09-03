using Microsoft.AspNetCore.Mvc;
using PROYECTO_SUBASTA.Entities;
using PROYECTO_SUBASTA.Repositories;

namespace PROYECTO_SUBASTA.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SubastasController : ControllerBase
    {
        private readonly ISubastaRepository _subastaRepository;

        public SubastasController(ISubastaRepository subastaRepository)
        {
            _subastaRepository = subastaRepository;
        }

        [HttpGet]
        public async Task<IActionResult> ObtenerActivas()
        {
            // Consultamos las subastas activas a través de la interfaz para respetar el principio de inversión de dependencias.
            var subastas = await _subastaRepository.ObtenerActivasAsync();

            // Entregamos los datos serializados al cliente mediante el protocolo HTTP estándar.
            return Ok(subastas);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> ObtenerPorId(int id)
        {
            // Buscamos una entidad específica filtrando por su identificador único en la capa de datos.
            var subasta = await _subastaRepository.ObtenerPorIdAsync(id);

            // Evaluamos si el recurso solicitado existe para evitar fallos de referencias nulas en las capas superiores.
            if (subasta == null)
            {
                // Retornamos un código 404 indicando que el recurso requerido no se encuentra disponible.
                return NotFound($"No se encontró la subasta con el ID {id}.");
            }

            // Devolvemos el recurso encontrado con su respectivo código de éxito HTTP.
            return Ok(subasta);
        }

        [HttpPost]
        public async Task<IActionResult> Crear([FromBody] Subasta subasta)
        {
            // Verificamos que el payload recibido no sea nulo antes de procesar reglas de infraestructura.
            if (subasta == null)
            {
                // Rechazamos la petición para mantener la integridad de los datos entrantes.
                return BadRequest("Los datos de la subasta son inválidos.");
            }

            // Registramos la nueva entidad en el contexto de persistencia mediante el repositorio inyectado.
            await _subastaRepository.CrearAsync(subasta);

            // Ejecutamos la persistencia física de los cambios en el motor de base de datos.
            await _subastaRepository.GuardarCambiosAsync();

            // Respondemos con el código 201 Created y la ruta de acceso al nuevo recurso creado, cumpliendo con los estándares REST.
            return CreatedAtAction(nameof(ObtenerPorId), new { id = subasta.Id }, subasta);
        }
    }
}