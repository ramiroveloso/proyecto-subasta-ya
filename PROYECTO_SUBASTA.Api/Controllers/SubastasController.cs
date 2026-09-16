using Microsoft.AspNetCore.Mvc;
using PROYECTO_SUBASTA.Domain.Entities;
using PROYECTO_SUBASTA.Application.UseCases;
using System.Threading.Tasks;
using PROYECTO_SUBASTA.Application.DTOs;

namespace PROYECTO_SUBASTA.Api.Controllers
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
        public async Task<IActionResult> ObtenerActivas([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
        {
            // Paginación aplicada para evitar sobrecargar la base de datos 
            var subastas = await _subastaUseCases.ObtenerActivasPaginadasAsync(pageNumber, pageSize);
            return Ok(subastas);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> ObtenerPorId(int id)
        {
            var subasta = await _subastaUseCases.ObtenerPorIdAsync(id);

            if (subasta == null)
            {
                return NotFound($"No se encontró la subasta con el ID {id}.");
            }

            return Ok(subasta);
        }

        [HttpPost]
        public async Task<IActionResult> Crear([FromBody] Subasta subasta)
        {
            if (subasta == null)
            {
                return BadRequest("Los datos de la subasta son inválidos.");
            }

            // Sin try-catch: Las reglas de negocio rotas lanzan ArgumentException que el Middleware global captura y transforma en 400 Bad Request.
            await _subastaUseCases.CrearAsync(subasta);

            return CreatedAtAction(nameof(ObtenerPorId), new { id = subasta.Id }, subasta);
        }

        [HttpPost("{id}/pujas")]
        public async Task<IActionResult> RegistrarPuja(int id, [FromBody] RegistrarPujaDto dto)
        {
            if (dto == null)
            {
                return BadRequest("Los datos de la puja son inválidos.");
            }

            // Sin try-catch: Las excepciones de concurrencia (409) y negocio (400) fluyen limpiamente hacia el Middleware global.
            await _subastaUseCases.RegistrarPujaAsync(id, dto.UsuarioId, dto.Monto, dto.Version);

            return Ok(new { mensaje = "Puja registrada con éxito y saldo retenido en Escrow." });
        }
    }
}