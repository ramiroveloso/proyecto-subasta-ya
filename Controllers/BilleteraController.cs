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

        // GET: api/Billetera/1
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

        // GET: api/Billetera/1/movimientos
        [HttpGet("{usuarioId}/movimientos")]
        public async Task<IActionResult> ObtenerMovimientos(int usuarioId)
        {
            // Retorna el historial diario de transacciones para alimentar la interfaz de auditoría financiera.
            var movimientos = await _billeteraService.ObtenerMovimientosAsync(usuarioId);
            return Ok(movimientos);
        }

        // POST: api/Billetera/cargar
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

        // POST: api/Billetera/retener
        [HttpPost("retener")]
        public async Task<IActionResult> RetenerSaldo([FromBody] OperacionFondosDto dto)
        {
            var resultado = await _billeteraService.RetenerSaldoAsync(dto.UsuarioId, dto.Monto, dto.SubastaId);
            if (!resultado)
            {
                return BadRequest(new { mensaje = "No se pudo retener el saldo. Verifique que exista saldo disponible suficiente." });
            }

            return Ok(new { mensaje = "Saldo retenido preventivamente para la puja." });
        }

        // POST: api/Billetera/liberar
        [HttpPost("liberar")]
        public async Task<IActionResult> LiberarSaldo([FromBody] OperacionFondosDto dto)
        {
            var resultado = await _billeteraService.LiberarSaldoAsync(dto.UsuarioId, dto.Monto, dto.SubastaId);
            if (!resultado)
            {
                return BadRequest(new { mensaje = "No se pudo liberar el saldo. Verifique los datos ingresados." });
            }

            return Ok(new { mensaje = "Saldo liberado y reintegrado al disponible." });
        }
    }

    // Objetos de Transferencia de Datos (DTOs) para desacoplar el contrato de API del modelo de dominio.
    public class CargarSaldoDto
    {
        public int UsuarioId { get; set; }
        public decimal Monto { get; set; }
    }

    public class OperacionFondosDto
    {
        public int UsuarioId { get; set; }
        public decimal Monto { get; set; }
        public int SubastaId { get; set; }
    }
}