using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PROYECTO_SUBASTA.Application.Commands.Subastas;
using PROYECTO_SUBASTA.Application.DTOs;
using PROYECTO_SUBASTA.Application.Queries.Subastas;

namespace PROYECTO_SUBASTA.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public class SubastasController : ControllerBase
    {
        private readonly ObtenerSubastasQueryHandler _obtenerSubastasHandler;
        private readonly ObtenerSubastaPorIdQueryHandler _obtenerSubastaPorIdHandler;
        private readonly CrearSubastaCommandHandler _crearSubastaHandler;
        private readonly RegistrarPujaCommandHandler _registrarPujaHandler;
        private readonly CerrarSubastasVencidasCommandHandler _cerrarSubastasHandler;

        public SubastasController(
            ObtenerSubastasQueryHandler obtenerSubastasHandler,
            ObtenerSubastaPorIdQueryHandler obtenerSubastaPorIdHandler,
            CrearSubastaCommandHandler crearSubastaHandler,
            RegistrarPujaCommandHandler registrarPujaHandler,
            CerrarSubastasVencidasCommandHandler cerrarSubastasHandler)
        {
            _obtenerSubastasHandler = obtenerSubastasHandler;
            _obtenerSubastaPorIdHandler = obtenerSubastaPorIdHandler;
            _crearSubastaHandler = crearSubastaHandler;
            _registrarPujaHandler = registrarPujaHandler;
            _cerrarSubastasHandler = cerrarSubastasHandler;
        }

        /// <summary>
        /// Recupera el catálogo de subastas con filtros opcionales (categoría, estado, precio mínimo/máximo, orden y paginación).
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<SubastaResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> ObtenerSubastas([FromQuery] ObtenerSubastasQuery query)
        {
            var (items, total) = await _obtenerSubastasHandler.HandleAsync(query ?? new ObtenerSubastasQuery());
            Response.Headers["X-Total-Count"] = total.ToString();
            Response.Headers["Access-Control-Expose-Headers"] = "X-Total-Count";
            return Ok(items);
        }

        /// <summary>
        /// Consulta la información detallada de una subasta específica y su historial de ofertas.
        /// </summary>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(SubastaResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> ObtenerPorId(int id)
        {
            if (id <= 0)
            {
                return BadRequest(new ErrorResponse { StatusCode = 400, Mensaje = "El identificador de subasta debe ser mayor a cero.", Timestamp = System.DateTime.UtcNow });
            }

            var subastaDto = await _obtenerSubastaPorIdHandler.HandleAsync(new ObtenerSubastaPorIdQuery { Id = id });
            if (subastaDto == null)
            {
                return NotFound(new ErrorResponse { StatusCode = 404, Mensaje = $"No se encontró la subasta con el ID {id}.", Timestamp = System.DateTime.UtcNow });
            }

            return Ok(subastaDto);
        }

        /// <summary>
        /// Registra una nueva subasta en el sistema validando sus fechas, precio base e incrementos.
        /// </summary>
        [HttpPost]
        [ProducesResponseType(typeof(SubastaResponseDto), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Crear([FromBody] CrearSubastaDto dto)
        {
            var command = new CrearSubastaCommand
            {
                Titulo = dto.Titulo,
                Descripcion = dto.Descripcion,
                UrlImagen = dto.UrlImagen,
                PrecioBase = dto.PrecioBase,
                IncrementoMinimo = dto.IncrementoMinimo,
                FechaInicio = dto.FechaInicio,
                FechaFin = dto.FechaFin,
                CategoriaId = dto.CategoriaId,
                VendedorId = dto.VendedorId
            };

            var responseDto = await _crearSubastaHandler.HandleAsync(command);

            return CreatedAtAction(nameof(ObtenerPorId), new { id = responseDto.Id }, responseDto);
        }

        /// <summary>
        /// Registra una oferta de forma atómica en el motor de subastas con control de concurrencia optimista (OCC),
        /// retención en Escrow y aplicación automática de la regla Anti-Sniping.
        /// </summary>
        [HttpPost("{id}/pujas")]
        [ProducesResponseType(typeof(RegistrarPujaResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
        public async Task<IActionResult> RegistrarPuja(int id, [FromBody] RegistrarPujaDto dto)
        {
            if (dto == null)
            {
                return BadRequest(new ErrorResponse { StatusCode = 400, Mensaje = "Los datos de la puja son inválidos.", Timestamp = System.DateTime.UtcNow });
            }

            var respuesta = await _registrarPujaHandler.HandleAsync(new RegistrarPujaCommand
            {
                SubastaId = id,
                UsuarioId = dto.UsuarioId,
                Monto = dto.Monto,
                VersionCliente = dto.Version
            });

            return Ok(respuesta);
        }

        /// <summary>
        /// Ejecuta el proceso de sincronización, adjudicación y cierre para subastas vencidas.
        /// </summary>
        [HttpPost("cierres")]
        [ProducesResponseType(typeof(CierreSubastasResultDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> ProcesarCierres()
        {
            var res = await _cerrarSubastasHandler.HandleAsync(new CerrarSubastasVencidasCommand());
            return Ok(res);
        }
    }
}