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
        /// <param name="usuarioId">Identificador único del usuario titular.</param>
        [HttpGet("{usuarioId}")]
        [ProducesResponseType(typeof(BilleteraResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ObtenerPorUsuario(int usuarioId)
        {
            if (usuarioId <= 0)
            {
                return BadRequest(new ErrorResponseDto
                {
                    StatusCode = StatusCodes.Status400BadRequest,
                    Mensaje = "El identificador de usuario debe ser mayor a cero."
                });
            }

            var billetera = await _obtenerBilleteraHandler.HandleAsync(new ObtenerBilleteraPorUsuarioQuery { UsuarioId = usuarioId });
            if (billetera == null)
            {
                return NotFound(new ErrorResponseDto
                {
                    StatusCode = StatusCodes.Status404NotFound,
                    Mensaje = $"No se encontró billetera asociada al usuario con ID {usuarioId}."
                });
            }

            return Ok(billetera);
        }

        /// <summary>
        /// Obtiene el historial de movimientos contables registrados en el libro mayor de la billetera.
        /// </summary>
        /// <param name="usuarioId">Identificador único del usuario.</param>
        [HttpGet("{usuarioId}/movimientos")]
        [ProducesResponseType(typeof(IEnumerable<MovimientoLedgerDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ObtenerMovimientos(int usuarioId)
        {
            if (usuarioId <= 0)
            {
                return BadRequest(new ErrorResponseDto
                {
                    StatusCode = StatusCodes.Status400BadRequest,
                    Mensaje = "El identificador de usuario debe ser mayor a cero."
                });
            }

            var movimientos = await _obtenerMovimientosHandler.HandleAsync(new ObtenerMovimientosQuery { UsuarioId = usuarioId });
            return Ok(movimientos);
        }

        /// <summary>
        /// Permite la recarga de saldo simulado en la cuenta del usuario.
        /// Bloquea explícitamente operaciones inseguras de retención o liberación manual de Escrow.
        /// </summary>
        /// <param name="usuarioId">Identificador del usuario en la ruta.</param>
        /// <param name="dto">Datos del movimiento a registrar.</param>
        [HttpPost("{usuarioId}/movimientos")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> RegistrarMovimiento(int usuarioId, [FromBody] RegistrarMovimientoDto dto)
        {
            if (dto == null || dto.Monto <= 0)
            {
                return BadRequest(new ErrorResponseDto
                {
                    StatusCode = StatusCodes.Status400BadRequest,
                    Mensaje = "El monto a procesar debe ser mayor a cero."
                });
            }

            var idUsuario = usuarioId > 0 ? usuarioId : (dto.UsuarioId ?? 0);
            if (idUsuario <= 0)
            {
                return BadRequest(new ErrorResponseDto
                {
                    StatusCode = StatusCodes.Status400BadRequest,
                    Mensaje = "Debe especificar un identificador de usuario válido."
                });
            }

            var tipo = (dto.Tipo ?? string.Empty).Trim().ToUpperInvariant();

            // Bloqueo de seguridad: Las retenciones y liberaciones solo deben ser gestionadas por el motor transaccional de pujas/cierre
            if (tipo == "RETENCION" || tipo == "LIBERACION")
            {
                return BadRequest(new ErrorResponseDto
                {
                    StatusCode = StatusCodes.Status400BadRequest,
                    Mensaje = $"Operación '{tipo}' denegada por seguridad: Las retenciones y liberaciones son administradas exclusivamente por el motor de Escrow transaccional y no pueden ser ejecutadas directamente."
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

            return BadRequest(new ErrorResponseDto
            {
                StatusCode = StatusCodes.Status400BadRequest,
                Mensaje = $"Tipo de movimiento '{dto.Tipo}' no válido. Operaciones permitidas para clientes: DEPOSITO / CARGA."
            });
        }
    }
}