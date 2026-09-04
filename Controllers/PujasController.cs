using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PROYECTO_SUBASTA.UseCases;

namespace PROYECTO_SUBASTA.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PujasController : ControllerBase
    {
        private readonly PujaUseCases _pujaUseCases;

        public PujasController(PujaUseCases pujaUseCases)
        {
            _pujaUseCases = pujaUseCases;
        }

        [HttpPost]
        public async Task<IActionResult> RegistrarPuja([FromBody] PujaRequestDto request)
        {
            // Validación básica de entrada
            if (request == null || request.Monto <= 0)
            {
                return BadRequest(new { mensaje = "Datos de puja inválidos." });
            }

            try
            {
                // Delegamos toda la complejidad al caso de uso
                var nuevaPuja = await _pujaUseCases.RegistrarPujaAsync(request.SubastaId, request.UsuarioId, request.Monto);

                // Si todo sale bien, devolvemos un 200 OK con los datos de la puja
                return Ok(nuevaPuja);
            }
            catch (ArgumentException ex)
            {
                // Captura violaciones de negocio (saldo insuficiente, monto bajo, subasta cerrada) -> HTTP 400
                return BadRequest(new { mensaje = ex.Message });
            }
            catch (DbUpdateConcurrencyException)
            {
                // ¡LA MAGIA DE LA CONCURRENCIA!
                // Captura si otro usuario modificó la subasta una fracción de segundo antes -> HTTP 409
                return Conflict(new { mensaje = "Alguien más realizó una oferta al mismo tiempo. El precio ha cambiado, intenta nuevamente." });
            }
            catch (Exception ex)
            {
                // Falla general de sistema -> HTTP 500
                return StatusCode(500, new { mensaje = "Ocurrió un error al procesar la puja.", detalle = ex.Message });
            }
        }
    }

    // DTO (Data Transfer Object) para recibir solo los datos necesarios desde el frontend sin exponer las entidades
    public class PujaRequestDto
    {
        public int SubastaId { get; set; }

        // En una app real con JWT, el UsuarioId se sacaría del token (User.Claims), 
        // pero por ahora lo recibimos en el body para agilizar las pruebas.
        public int UsuarioId { get; set; }
        public decimal Monto { get; set; }
    }
}