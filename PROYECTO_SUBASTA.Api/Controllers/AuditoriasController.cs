using Microsoft.AspNetCore.Mvc;
using PROYECTO_SUBASTA.Application.UseCases;
using System.Threading.Tasks;

namespace PROYECTO_SUBASTA.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuditoriasController : ControllerBase
    {
        private readonly IAuditoriaService _auditoriaService;

        public AuditoriasController(IAuditoriaService auditoriaService)
        {
            _auditoriaService = auditoriaService;
        }

        // GET: api/Auditorias?limite=100
        [HttpGet]
        public async Task<IActionResult> ObtenerLogs([FromQuery] int limite = 100)
        {
            var logs = await _auditoriaService.ObtenerLogsAsync(limite);
            return Ok(logs);
        }
    }
}
