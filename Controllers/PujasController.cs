using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using PROYECTO_SUBASTA.Hubs;
using PROYECTO_SUBASTA.UseCases;

namespace PROYECTO_SUBASTA.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PujasController : ControllerBase
    {
        private readonly PujaUseCases _pujaUseCases;
        private readonly IHubContext<SubastaHub> _hubContext;

        // Inyectamos IHubContext para tener acceso a SignalR desde el controlador
        public PujasController(PujaUseCases pujaUseCases, IHubContext<SubastaHub> hubContext)
        {
            _pujaUseCases = pujaUseCases;
            _hubContext = hubContext;
        }

        [HttpPost]
        public async Task<IActionResult> RegistrarPuja([FromBody] PujaRequestDto request)
        {
            if (request == null || request.Monto <= 0)
                return BadRequest(new { mensaje = "Datos de puja inválidos." });

            try
            {
                var nuevaPuja = await _pujaUseCases.RegistrarPujaAsync(request.SubastaId, request.UsuarioId, request.Monto);

                // NOTIFICACIÓN EN TIEMPO REAL:
                // Avisamos solo a los usuarios conectados al grupo de ESTA subasta.
                await _hubContext.Clients.Group(request.SubastaId.ToString())
                    .SendAsync("RecibirNuevaPuja", new
                    {
                        subastaId = request.SubastaId,
                        nuevoPrecio = nuevaPuja.Monto,
                        usuarioId = nuevaPuja.UsuarioId
                    });

                return Ok(nuevaPuja);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { mensaje = ex.Message });
            }
            catch (DbUpdateConcurrencyException)
            {
                return Conflict(new { mensaje = "Alguien más realizó una oferta al mismo tiempo. El precio ha cambiado, intenta nuevamente." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { mensaje = "Ocurrió un error al procesar la puja.", detalle = ex.Message });
            }
        }
    }

    public class PujaRequestDto
    {
        public int SubastaId { get; set; }
        public int UsuarioId { get; set; }
        public decimal Monto { get; set; }
    }
}