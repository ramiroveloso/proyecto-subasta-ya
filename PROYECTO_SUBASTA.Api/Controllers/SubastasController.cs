using Microsoft.AspNetCore.Mvc;
using PROYECTO_SUBASTA.Domain.Entities;
using PROYECTO_SUBASTA.Application.UseCases;
using System.Threading.Tasks;
using System.Linq;
using PROYECTO_SUBASTA.Application.DTOs;

namespace PROYECTO_SUBASTA.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SubastasController : ControllerBase
    {
        private readonly SubastaUseCases _subastaUseCases;

        public SubastasController(SubastaUseCases subastaUseCases)
        {
            _subastaUseCases = subastaUseCases;
        }

        [HttpGet]
        public async Task<IActionResult> ObtenerActivas([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 50)
        {
            // Paginación aplicada para evitar sobrecargar la base de datos y mapeo a DTO para evitar ciclos de serialización
            var subastas = await _subastaUseCases.ObtenerActivasPaginadasAsync(pageNumber, pageSize);
            var dtos = subastas.Select(subasta => new SubastaResponseDto
            {
                Id = subasta.Id,
                Titulo = subasta.Titulo,
                Descripcion = subasta.Descripcion,
                UrlImagen = subasta.UrlImagen,
                PrecioBase = subasta.PrecioBase,
                IncrementoMinimo = subasta.IncrementoMinimo,
                FechaInicio = subasta.FechaInicio,
                FechaFin = subasta.FechaFin,
                Estado = subasta.Estado,
                CategoriaId = subasta.CategoriaId,
                CategoriaNombre = subasta.Categoria?.Nombre ?? string.Empty,
                VendedorId = subasta.VendedorId,
                GanadorId = subasta.GanadorId,
                PrecioFinal = subasta.PrecioFinal,
                Version = subasta.Version,
                Pujas = subasta.Pujas?.Select(p => new PujaItemDto
                {
                    Id = p.Id,
                    SubastaId = p.SubastaId,
                    UsuarioId = p.UsuarioId,
                    Monto = p.Monto,
                    FechaCreacion = p.FechaCreacion,
                    PostorAnonimo = $"Postor #{(p.UsuarioId * 33 + 100):X}"
                }).ToList() ?? new()
            }).ToList();

            return Ok(dtos);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> ObtenerPorId(int id)
        {
            var subasta = await _subastaUseCases.ObtenerPorIdAsync(id);

            if (subasta == null)
            {
                return NotFound(new { mensaje = $"No se encontró la subasta con el ID {id}." });
            }

            var responseDto = new SubastaResponseDto
            {
                Id = subasta.Id,
                Titulo = subasta.Titulo,
                Descripcion = subasta.Descripcion,
                UrlImagen = subasta.UrlImagen,
                PrecioBase = subasta.PrecioBase,
                IncrementoMinimo = subasta.IncrementoMinimo,
                FechaInicio = subasta.FechaInicio,
                FechaFin = subasta.FechaFin,
                Estado = subasta.Estado,
                CategoriaId = subasta.CategoriaId,
                CategoriaNombre = subasta.Categoria?.Nombre ?? string.Empty,
                VendedorId = subasta.VendedorId,
                GanadorId = subasta.GanadorId,
                PrecioFinal = subasta.PrecioFinal,
                Version = subasta.Version,
                Pujas = subasta.Pujas?.Select(p => new PujaItemDto
                {
                    Id = p.Id,
                    SubastaId = p.SubastaId,
                    UsuarioId = p.UsuarioId,
                    Monto = p.Monto,
                    FechaCreacion = p.FechaCreacion,
                    PostorAnonimo = $"Postor #{(p.UsuarioId * 33 + 100):X}"
                }).ToList() ?? new()
            };

            return Ok(responseDto);
        }

        [HttpPost]
        public async Task<IActionResult> Crear([FromBody] Subasta subasta)
        {
            if (subasta == null)
            {
                return BadRequest("Los datos de la subasta son inválidos.");
            }

            await _subastaUseCases.CrearAsync(subasta);

            return CreatedAtAction(nameof(ObtenerPorId), new { id = subasta.Id }, subasta);
        }

        [HttpPost("{id}/pujas")]
        public async Task<IActionResult> RegistrarPuja(int id, [FromBody] RegistrarPujaDto dto)
        {
            if (dto == null)
            {
                return BadRequest("Los datos de la puja son inválidos.");
            }

            var resultado = await _subastaUseCases.RegistrarPujaAsync(id, dto.UsuarioId, dto.Monto, dto.Version);

            return Ok(new
            {
                mensaje = resultado.AntiSnipingActivado
                    ? "Puja registrada con éxito. ¡Regla Anti-Sniping activada (+60s)!"
                    : "Puja registrada con éxito y saldo retenido en Escrow.",
                version = resultado.SubastaVersion,
                fechaFin = resultado.Subasta.FechaFin,
                antiSnipingActivado = resultado.AntiSnipingActivado,
                puja = new PujaItemDto
                {
                    Id = resultado.Puja.Id,
                    SubastaId = resultado.Puja.SubastaId,
                    UsuarioId = resultado.Puja.UsuarioId,
                    Monto = resultado.Puja.Monto,
                    FechaCreacion = resultado.Puja.FechaCreacion,
                    PostorAnonimo = $"Postor #{(resultado.Puja.UsuarioId * 33 + 100):X}"
                }
            });
        }

        // POST: api/Subastas/cierres
        [HttpPost("cierres")]
        public async Task<IActionResult> ProcesarCierres([FromServices] IAdjudicacionService adjudicacionService)
        {
            var res = await adjudicacionService.ProcesarSubastasVencidasAsync();
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