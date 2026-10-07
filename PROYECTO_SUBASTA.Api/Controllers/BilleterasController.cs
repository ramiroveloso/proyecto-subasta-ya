using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PROYECTO_SUBASTA.Application.Commands.Billeteras;
using PROYECTO_SUBASTA.Application.DTOs;
using PROYECTO_SUBASTA.Application.Queries.Billeteras;

namespace PROYECTO_SUBASTA.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class BilleterasController : ControllerBase
    {
        private readonly ObtenerBilleteraPorUsuarioQueryHandler _obtenerBilleteraHandler;
        private readonly ObtenerMovimientosQueryHandler _obtenerMovimientosHandler;
        private readonly CargarSaldoCommandHandler _cargarSaldoHandler;

        public BilleterasController(
            ObtenerBilleteraPorUsuarioQueryHandler obtenerBilleteraHandler,
            ObtenerMovimientosQueryHandler obtenerMovimientosHandler,
            CargarSaldoCommandHandler cargarSaldoHandler)
        {
            _obtenerBilleteraHandler = obtenerBilleteraHandler;
            _obtenerMovimientosHandler = obtenerMovimientosHandler;
            _cargarSaldoHandler = cargarSaldoHandler;
        }

        /// <summary>
        /// Consulta el estado financiero de la billetera de un usuario (saldo total, disponible y retenido en Escrow).
        /// </summary>
        [HttpGet("{usuarioId}")]
        [ProducesResponseType(typeof(BilleteraResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> ObtenerPorUsuario(int usuarioId)
        {
            var billetera = await _obtenerBilleteraHandler.HandleAsync(new ObtenerBilleteraPorUsuarioQuery { UsuarioId = usuarioId });
            if (billetera == null)
            {
                return NotFound(new { mensaje = $"No se encontró billetera asociada al usuario con ID {usuarioId}." });
            }

            return Ok(billetera);
        }

        /// <summary>
        /// Obtiene el historial de movimientos contables registrados en el libro mayor de la billetera.
        /// </summary>
        [HttpGet("{usuarioId}/movimientos")]
        [ProducesResponseType(typeof(IEnumerable<MovimientoLedgerDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> ObtenerMovimientos(int usuarioId)
        {
            var movimientos = await _obtenerMovimientosHandler.HandleAsync(new ObtenerMovimientosQuery { UsuarioId = usuarioId });
            return Ok(movimientos);
        }

        /// <summary>
        /// Permite la recarga de saldo simulado en la cuenta del usuario.
        /// Bloquea explícitamente operaciones inseguras de retención o liberación manual de Escrow.
        /// </summary>
        [HttpPost("{usuarioId}/movimientos")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> RegistrarMovimiento(int usuarioId, [FromBody] RegistrarMovimientoDto dto)
        {
            if (dto == null || dto.Monto <= 0)
            {
                return BadRequest(new { mensaje = "El monto a procesar debe ser mayor a cero." });
            }

            var idUsuario = usuarioId > 0 ? usuarioId : (dto.UsuarioId ?? 0);
            if (idUsuario <= 0)
            {
                return BadRequest(new { mensaje = "Debe especificar un identificador de usuario válido." });
            }

            var tipo = (dto.Tipo ?? string.Empty).Trim().ToUpperInvariant();

            // Bloqueo de seguridad: Las retenciones y liberaciones solo deben ser gestionadas por el motor transaccional de pujas/cierre
            if (tipo == "RETENCION" || tipo == "LIBERACION")
            {
                return BadRequest(new
                {
                    mensaje = $"Operación '{tipo}' denegada por seguridad: Las retenciones y liberaciones son administradas exclusivamente por el motor de Escrow transaccional y no pueden ser ejecutadas directamente."
                });
            }

            if (string.IsNullOrEmpty(tipo) || tipo == "CARGA" || tipo == "DEPOSITO")
            {
                await _cargarSaldoHandler.HandleAsync(new CargarSaldoCommand
                {
                    UsuarioId = idUsuario,
                    Monto = dto.Monto
                });

                return Ok(new { mensaje = "Saldo acreditado correctamente." });
            }

            return BadRequest(new
            {
                mensaje = $"Tipo de movimiento '{dto.Tipo}' no válido. Operaciones permitidas para clientes: DEPOSITO / CARGA."
            });
        }
    }
}