using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PROYECTO_SUBASTA.Application.DTOs;
using PROYECTO_SUBASTA.Application.Repositories;
using PROYECTO_SUBASTA.Application.UseCases;
using PROYECTO_SUBASTA.Domain.Entities;

namespace PROYECTO_SUBASTA.Application.Queries.Subastas
{
    public class ObtenerSubastasActivasQuery
    {
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 50;
    }

    public class ObtenerSubastasActivasQueryHandler
    {
        private readonly ISubastaRepository _subastaRepository;
        private readonly IAdjudicacionService _adjudicacionService;

        public ObtenerSubastasActivasQueryHandler(
            ISubastaRepository subastaRepository,
            IAdjudicacionService adjudicacionService)
        {
            _subastaRepository = subastaRepository ?? throw new ArgumentNullException(nameof(subastaRepository));
            _adjudicacionService = adjudicacionService ?? throw new ArgumentNullException(nameof(adjudicacionService));
        }

        public async Task<IEnumerable<SubastaResponseDto>> HandleAsync(ObtenerSubastasActivasQuery query, CancellationToken cancellationToken = default)
        {
            // Sincronizar y cerrar subastas vencidas antes de listar
            await _adjudicacionService.ProcesarSubastasVencidasAsync(cancellationToken);

            var subastas = await _subastaRepository.ObtenerActivasAsync();

            var pageNumber = query.PageNumber > 0 ? query.PageNumber : 1;
            var pageSize = query.PageSize > 0 ? query.PageSize : 50;

            var paginadas = subastas
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize);

            return paginadas.Select(MapearADto).ToList();
        }

        public static SubastaResponseDto MapearADto(Subasta subasta)
        {
            return new SubastaResponseDto
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
                }).OrderByDescending(p => p.Monto).ToList() ?? new()
            };
        }
    }
}
