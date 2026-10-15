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
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
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
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> ObtenerPorUsuario(int usuarioId)
        {
            if (usuarioId <= 0)
            {
                return BadRequest(new ErrorResponse { StatusCode = 400, Mensaje = "El identificador de usuario debe ser mayor a cero.", Timestamp = System.DateTime.UtcNow });
            }

            var billetera = await _obtenerBilleteraHandler.HandleAsync(new ObtenerBilleteraPorUsuarioQuery { UsuarioId = usuarioId });
            if (billetera == null)
            {
                return NotFound(new ErrorResponse { StatusCode = 404, Mensaje = $"No se encontró billetera asociada al usuario con ID {usuarioId}.", Timestamp = System.DateTime.UtcNow });
            }

            return Ok(billetera);
        }

        /// <summary>
        /// Obtiene el historial de movimientos contables registrados en el libro mayor de la billetera.
        /// </summary>
        [HttpGet("{usuarioId}/movimientos")]
        [ProducesResponseType(typeof(IEnumerable<MovimientoLedgerDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> ObtenerMovimientos(int usuarioId)
        {
            var movimientos = await _obtenerMovimientosHandler.HandleAsync(new ObtenerMovimientosQuery { UsuarioId = usuarioId });
            return Ok(movimientos);
        }

        /// <summary>
        /// Registra un depósito de fondos en la billetera del usuario.
        /// </summary>
        [HttpPost("{usuarioId}/depositos")]
        [ProducesResponseType(typeof(MovimientoLedgerDto), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Depositar(int usuarioId, [FromBody] CargarSaldoDto dto)
        {
            var idUsuario = usuarioId > 0 ? usuarioId : (dto.UsuarioId ?? 0);
            var command = new CargarSaldoCommand
            {
                UsuarioId = idUsuario,
                Monto = dto.Monto
            };

            var movimiento = await _cargarSaldoHandler.HandleAsync(command);
            return CreatedAtAction(nameof(ObtenerPorUsuario), new { usuarioId = idUsuario }, movimiento);
        }
    }
}