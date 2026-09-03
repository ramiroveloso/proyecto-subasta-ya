using Microsoft.AspNetCore.Mvc;
using PROYECTO_SUBASTA.UseCases;
using System.Threading.Tasks;

namespace PROYECTO_SUBASTA.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class BilleteraController : ControllerBase
    {
        private readonly IBilleteraService _billeteraService;

        public BilleteraController(IBilleteraService billeteraService)
        {
            _billeteraService = billeteraService;
        }
        
        // GET: api/billetera/1
        [HttpGet("{usuarioId}")]
        public async Task<IActionResult> ObtenerPorUsuario(int usuarioId)
        {
            var billetera = await _billeteraService.ObtenerBilleteraPorUsuarioAsync(usuarioId);
            if (billetera == null)
            {
                return NotFound(new { mensaje = $"No se encontró billetera asociada al usuario con ID {usuarioId}." });
            }

            return Ok(billetera);
        }

        // POST: api/billetera/cargar
        [HttpPost("cargar")]
        public async Task<IActionResult> CargarSaldo([FromBody] CargarSaldoDto dto)
        {
            var resultado = await _billeteraService.CargarSaldoAsync(dto.UsuarioId, dto.Monto);
            if (!resultado)
            {
                return BadRequest(new { mensaje = "No se pudo realizar la carga. Verifique que el usuario exista y el monto sea mayor a 0." });
            }

            return Ok(new { mensaje = "Saldo acreditado correctamente." });
        }
    }

    public class CargarSaldoDto
    {
        public int UsuarioId { get; set; }
        public decimal Monto { get; set; }
    }
}
