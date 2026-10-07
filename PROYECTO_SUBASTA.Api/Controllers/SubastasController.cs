using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PROYECTO_SUBASTA.Application.Commands.Subastas;
using PROYECTO_SUBASTA.Application.DTOs;
using PROYECTO_SUBASTA.Application.Queries.Subastas;
using PROYECTO_SUBASTA.Application.UseCases;

namespace PROYECTO_SUBASTA.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class SubastasController : ControllerBase
    {
        private readonly ObtenerSubastasActivasQueryHandler _obtenerSubastasActivasHandler;
        private readonly ObtenerSubastaPorIdQueryHandler _obtenerSubastaPorIdHandler;
        private readonly CrearSubastaCommandHandler _crearSubastaHandler;
        private readonly RegistrarPujaCommandHandler _registrarPujaHandler;
        private readonly IAdjudicacionService _adjudicacionService;

        public SubastasController(
            ObtenerSubastasActivasQueryHandler obtenerSubastasActivasHandler,
            ObtenerSubastaPorIdQueryHandler obtenerSubastaPorIdHandler,
            CrearSubastaCommandHandler crearSubastaHandler,
            RegistrarPujaCommandHandler registrarPujaHandler,
            IAdjudicacionService adjudicacionService)
        {
            _obtenerSubastasActivasHandler = obtenerSubastasActivasHandler;
            _obtenerSubastaPorIdHandler = obtenerSubastaPorIdHandler;
            _crearSubastaHandler = crearSubastaHandler;
            _registrarPujaHandler = registrarPujaHandler;
            _adjudicacionService = adjudicacionService;
        }

        /// <summary>
        /// Recupera el catálogo de subastas activas de forma paginada para optimizar recursos.
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<SubastaResponseDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> ObtenerActivas([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 50)
        {
            var dtos = await _obtenerSubastasActivasHandler.HandleAsync(new ObtenerSubastasActivasQuery
            {
                PageNumber = pageNumber,
                PageSize = pageSize
            });

            return Ok(dtos);
        }

        /// <summary>
        /// Consulta la información detallada de una subasta específica y su historial de ofertas.
        /// </summary>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(SubastaResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> ObtenerPorId(int id)
        {
            var subastaDto = await _obtenerSubastaPorIdHandler.HandleAsync(new ObtenerSubastaPorIdQuery { Id = id });
            if (subastaDto == null)
            {
                return NotFound(new { mensaje = $"No se encontró la subasta con el ID {id}." });
            }

            return Ok(subastaDto);
        }

        /// <summary>
        /// Registra una nueva subasta en el sistema validando sus fechas, precio base e incrementos.
        /// </summary>
        [HttpPost]
        [ProducesResponseType(typeof(SubastaResponseDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Crear([FromBody] CrearSubastaDto dto)
        {
            if (dto == null)
            {
                return BadRequest("Los datos de la subasta son inválidos.");
            }

            var subasta = await _crearSubastaHandler.HandleAsync(dto);
            var responseDto = ObtenerSubastasActivasQueryHandler.MapearADto(subasta);

            return CreatedAtAction(nameof(ObtenerPorId), new { id = subasta.Id }, responseDto);
        }

        /// <summary>
        /// Registra una oferta de forma atómica en el motor de subastas con control de concurrencia optimista (OCC),
        /// retención en Escrow y aplicación automática de la regla Anti-Sniping.
        /// </summary>
        [HttpPost("{id}/pujas")]
        [ProducesResponseType(typeof(RegistrarPujaResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> RegistrarPuja(int id, [FromBody] RegistrarPujaDto dto)
        {
            if (dto == null)
            {
                return BadRequest("Los datos de la puja son inválidos.");
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
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> ProcesarCierres()
        {
            var res = await _adjudicacionService.ProcesarSubastasVencidasAsync();
            return Ok(new
            {
                mensaje = "Proceso de verificación y adjudicación ejecutado exitosamente.",
                subastasActivadas = res.Activadas,
                subastasFinalizadas = res.Finalizadas,
                subastasDesiertas = res.Desiertas
            });
        }
    }
}