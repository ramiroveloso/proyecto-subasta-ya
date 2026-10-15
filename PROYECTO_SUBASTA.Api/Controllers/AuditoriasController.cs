using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PROYECTO_SUBASTA.Application.DTOs;
using PROYECTO_SUBASTA.Application.Queries.Auditoria;

namespace PROYECTO_SUBASTA.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public class AuditoriasController : ControllerBase
    {
        private readonly ObtenerAuditoriaQueryHandler _obtenerAuditoriaHandler;

        public AuditoriasController(ObtenerAuditoriaQueryHandler obtenerAuditoriaHandler)
        {
            _obtenerAuditoriaHandler = obtenerAuditoriaHandler;
        }

        /// <summary>
        /// Obtiene el registro cronológico de eventos y auditoría del sistema.
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<LogAuditoriaDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> ObtenerLogs([FromQuery] int limite = 100)
        {
            var logs = await _obtenerAuditoriaHandler.HandleAsync(new ObtenerAuditoriaQuery { Limite = limite });
            return Ok(logs);
        }
    }
}
