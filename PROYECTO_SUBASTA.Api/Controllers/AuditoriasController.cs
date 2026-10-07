using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PROYECTO_SUBASTA.Application.UseCases;
using PROYECTO_SUBASTA.Domain.Entities;

namespace PROYECTO_SUBASTA.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class AuditoriasController : ControllerBase
    {
        private readonly IAuditoriaService _auditoriaService;

        public AuditoriasController(IAuditoriaService auditoriaService)
        {
            _auditoriaService = auditoriaService;
        }

        /// <summary>
        /// Obtiene el registro cronológico de eventos y auditoría del sistema.
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<LogAuditoria>), StatusCodes.Status200OK)]
        public async Task<IActionResult> ObtenerLogs([FromQuery] int limite = 100)
        {
            var logs = await _auditoriaService.ObtenerLogsAsync(limite);
            return Ok(logs);
        }
    }
}
