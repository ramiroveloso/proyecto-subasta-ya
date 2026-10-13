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
    public class SubastasController : ControllerBase
    {
        private readonly ObtenerSubastasActivasQueryHandler _obtenerSubastasHandler;
        private readonly ObtenerSubastaPorIdQueryHandler _obtenerSubastaPorIdHandler;
        private readonly CrearSubastaCommandHandler _crearSubastaHandler;
        private readonly RegistrarPujaCommandHandler _registrarPujaHandler;
        private readonly EjecutarCierresCommandHandler _ejecutarCierresHandler;

        public SubastasController(
            ObtenerSubastasActivasQueryHandler obtenerSubastasHandler,
            ObtenerSubastaPorIdQueryHandler obtenerSubastaPorIdHandler,
            CrearSubastaCommandHandler crearSubastaHandler,
            RegistrarPujaCommandHandler registrarPujaHandler,
            EjecutarCierresCommandHandler ejecutarCierresHandler)
        {
            _obtenerSubastasHandler = obtenerSubastasHandler;
            _obtenerSubastaPorIdHandler = obtenerSubastaPorIdHandler;
            _crearSubastaHandler = crearSubastaHandler;
            _registrarPujaHandler = registrarPujaHandler;
            _ejecutarCierresHandler = ejecutarCierresHandler;
        }

        /// <summary>
        /// Recupera el catálogo de subastas filtrado, ordenado y paginado directamente en base de datos.
        /// </summary>
        /// <param name="categoriaId">Identificador opcional de categoría para filtrar.</param>
        /// <param name="estado">Estado de la subasta (ACTIVA, PROGRAMADA, FINALIZADA, DESIERTA). Opcional: si se omite, devuelve todas.</param>
        /// <param name="precioMin">Filtro de precio mínimo base.</param>
        /// <param name="precioMax">Filtro de precio máximo base.</param>
        /// <param name="ordenarPor">Criterio de ordenamiento (cierre, precio, titulo, pujas). Por defecto cierre.</param>
        /// <param name="orden">Dirección de ordenamiento (asc, desc). Por defecto asc.</param>
        /// <param name="pageNumber">Número de página (iniciando en 1).</param>
        /// <param name="pageSize">Cantidad de registros por página (máximo 100).</param>
        /// <returns>Colección de subastas que cumplen los criterios y total en cabecera X-Total-Count.</returns>
        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<SubastaResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ObtenerSubastas(
            [FromQuery] int? categoriaId = null,
            [FromQuery] string? estado = null,
            [FromQuery] decimal? precioMin = null,
            [FromQuery] decimal? precioMax = null,
            [FromQuery] string? ordenarPor = "cierre",
            [FromQuery] string? orden = "asc",
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 50)
        {
            var resultado = await _obtenerSubastasHandler.HandleAsync(new ObtenerSubastasQuery
            {
                CategoriaId = categoriaId,
                Estado = estado,
                PrecioMin = precioMin,
                PrecioMax = precioMax,
                OrdenarPor = ordenarPor,
                Orden = orden,
                PageNumber = pageNumber,
                PageSize = pageSize
            });

            Response.Headers["X-Total-Count"] = resultado.TotalCount.ToString();
            Response.Headers["Access-Control-Expose-Headers"] = "X-Total-Count";

            return Ok(resultado.Items);
        }

        /// <summary>
        /// Consulta la información detallada de una subasta específica y su historial de ofertas.
        /// </summary>
        /// <param name="id">Identificador único de la subasta.</param>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(SubastaResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ObtenerPorId(int id)
        {
            if (id <= 0)
            {
                return BadRequest(new ErrorResponseDto
                {
                    StatusCode = StatusCodes.Status400BadRequest,
                    Mensaje = "El identificador de la subasta debe ser mayor a cero."
                });
            }

            var subastaDto = await _obtenerSubastaPorIdHandler.HandleAsync(new ObtenerSubastaPorIdQuery { Id = id });
            if (subastaDto == null)
            {
                return NotFound(new ErrorResponseDto
                {
                    StatusCode = StatusCodes.Status404NotFound,
                    Mensaje = $"No se encontró la subasta con el ID {id}."
                });
            }

            return Ok(subastaDto);
        }

        /// <summary>
        /// Registra una nueva subasta en el sistema validando sus fechas, precio base e incrementos.
        /// </summary>
        /// <param name="dto">Datos de configuración y publicación de la subasta.</param>
        [HttpPost]
        [ProducesResponseType(typeof(SubastaResponseDto), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> Crear([FromBody] CrearSubastaDto dto)
        {
            if (dto == null)
            {
                return BadRequest(new ErrorResponseDto
                {
                    StatusCode = StatusCodes.Status400BadRequest,
                    Mensaje = "Los datos de la subasta son obligatorios."
                });
            }

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
        /// <param name="id">Identificador único de la subasta a ofertar.</param>
        /// <param name="dto">Datos de la puja y versión cliente para control OCC.</param>
        [HttpPost("{id}/pujas")]
        [ProducesResponseType(typeof(RegistrarPujaResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> RegistrarPuja(int id, [FromBody] RegistrarPujaDto dto)
        {
            if (dto == null)
            {
                return BadRequest(new ErrorResponseDto
                {
                    StatusCode = StatusCodes.Status400BadRequest,
                    Mensaje = "Los datos de la puja son obligatorios."
                });
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
        [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ProcesarCierres()
        {
            var res = await _ejecutarCierresHandler.HandleAsync(new EjecutarCierresCommand());
            return Ok(new
            {
                mensaje = "Proceso de verificación y adjudicación ejecutado exitosamente.",
                subastasActivadas = res.Activadas,
                subastasFinalizadas = res.Finalizadas,
                subastasDesiertas = res.Desiertas,
                subastasFallidas = res.Fallidas
            });
        }
    }
}