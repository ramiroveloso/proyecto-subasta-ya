using Microsoft.AspNetCore.Mvc;
using PROYECTO_SUBASTA.Application.DTOs;
using PROYECTO_SUBASTA.Application.UseCases;
using System;
using System.Threading.Tasks;

namespace PROYECTO_SUBASTA.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class BilleterasController : ControllerBase
    {
        private readonly IBilleteraService _billeteraService;

        public BilleterasController(IBilleteraService billeteraService)
        {
            _billeteraService = billeteraService;
        }

        // GET: api/Billeteras/1
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

        // GET: api/Billeteras/1/movimientos
        [HttpGet("{usuarioId}/movimientos")]
        public async Task<IActionResult> ObtenerMovimientos(int usuarioId)
        {
            var movimientos = await _billeteraService.ObtenerMovimientosAsync(usuarioId);
            return Ok(movimientos);
        }

        // POST: api/Billeteras/1/movimientos
        [HttpPost("{usuarioId}/movimientos")]
        public async Task<IActionResult> RegistrarMovimiento(int usuarioId, [FromBody] RegistrarMovimientoDto dto)
        {
            if (dto == null || dto.Monto <= 0)
            {
                return BadRequest(new { mensaje = "El monto a procesar debe ser mayor a 0." });
            }

            var idUsuario = usuarioId > 0 ? usuarioId : (dto.UsuarioId ?? 0);
            var tipo = (dto.Tipo ?? string.Empty).Trim().ToUpperInvariant();

            if (string.IsNullOrEmpty(tipo) || tipo == "CARGA" || tipo == "DEPOSITO")
            {
                var resultado = await _billeteraService.CargarSaldoAsync(idUsuario, dto.Monto);
                if (!resultado)
                {
                    return BadRequest(new { mensaje = "No se pudo realizar la carga. Verifique que el usuario exista y el monto sea mayor a 0." });
                }

                return Ok(new { mensaje = "Saldo acreditado correctamente." });
            }
            else if (tipo == "RETENCION")
            {
                if (!dto.SubastaId.HasValue || dto.SubastaId.Value <= 0)
                {
                    return BadRequest(new { mensaje = "Debe vincular un ID de subasta válido para la retención." });
                }

                var resultado = await _billeteraService.RetenerSaldoAsync(idUsuario, dto.Monto, dto.SubastaId.Value);
                if (!resultado)
                {
                    return BadRequest(new { mensaje = "No se pudo retener el saldo. Verifique que exista saldo disponible suficiente." });
                }

                return Ok(new { mensaje = "Saldo retenido preventivamente para la puja." });
            }
            else if (tipo == "LIBERACION")
            {
                if (!dto.SubastaId.HasValue || dto.SubastaId.Value <= 0)
                {
                    return BadRequest(new { mensaje = "Debe vincular un ID de subasta válido para la liberación." });
                }

                var resultado = await _billeteraService.LiberarSaldoAsync(idUsuario, dto.Monto, dto.SubastaId.Value);
                if (!resultado)
                {
                    return BadRequest(new { mensaje = "No se pudo liberar el saldo. Verifique los datos ingresados." });
                }

                return Ok(new { mensaje = "Saldo liberado y reintegrado al disponible." });
            }
            else
            {
                return BadRequest(new { mensaje = $"Tipo de movimiento '{dto.Tipo}' no válido. Opciones permitidas: DEPOSITO, RETENCION, LIBERACION." });
            }
        }
    }
}