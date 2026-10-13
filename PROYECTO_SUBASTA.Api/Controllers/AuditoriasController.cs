using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PROYECTO_SUBASTA.Application.DTOs;
using PROYECTO_SUBASTA.Application.Queries.Auditorias;

namespace PROYECTO_SUBASTA.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class AuditoriasController : ControllerBase
    {
        private readonly ObtenerLogsAuditoriaQueryHandler _obtenerLogsAuditoriaHandler;

        public AuditoriasController(ObtenerLogsAuditoriaQueryHandler obtenerLogsAuditoriaHandler)
        {
            _obtenerLogsAuditoriaHandler = obtenerLogsAuditoriaHandler;
        }

        /// <summary>
        /// Obtiene el registro cronológico de eventos y auditoría del sistema.
        /// </summary>
        /// <param name="limite">Cantidad máxima de registros de auditoría a retornar (mayor a cero).</param>
        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<LogAuditoriaDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ObtenerLogs([FromQuery] int limite = 100)
        {
            if (limite <= 0)
            {
                return BadRequest(new ErrorResponseDto
                {
                    StatusCode = StatusCodes.Status400BadRequest,
                    Mensaje = "El límite de registros de auditoría debe ser mayor a cero."
                });
            }

            var logs = await _obtenerLogsAuditoriaHandler.HandleAsync(new ObtenerLogsAuditoriaQuery { Limite = limite });
            return Ok(logs);
        }
    }
}
