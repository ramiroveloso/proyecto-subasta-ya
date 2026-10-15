using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PROYECTO_SUBASTA.Application.DTOs;
using PROYECTO_SUBASTA.Application.Mappers;
using PROYECTO_SUBASTA.Application.Repositories;

namespace PROYECTO_SUBASTA.Application.Queries.Subastas
{
    public class ObtenerSubastasQuery
    {
        public int? CategoriaId { get; set; }
        public string? Estado { get; set; }
        public decimal? PrecioMin { get; set; }
        public decimal? PrecioMax { get; set; }
        public string? Orden { get; set; } // "cierre" | "cierre_desc" | "precio" | "precio_desc" | "pujas"
        public int Pagina { get; set; } = 1;
        public int TamanoPagina { get; set; } = 20;

        // Aliases para compatibilidad con parámetros previos
        public int? PageNumber { get => Pagina; set { if (value.HasValue) Pagina = value.Value; } }
        public int? PageSize { get => TamanoPagina; set { if (value.HasValue) TamanoPagina = value.Value; } }
    }

    public class ObtenerSubastasQueryHandler
    {
        private readonly ISubastaRepository _subastaRepository;

        public ObtenerSubastasQueryHandler(ISubastaRepository subastaRepository)
        {
            _subastaRepository = subastaRepository ?? throw new ArgumentNullException(nameof(subastaRepository));
        }

        public async Task<(IEnumerable<SubastaResponseDto> Items, int TotalCount)> HandleAsync(ObtenerSubastasQuery query, CancellationToken cancellationToken = default)
        {
            query ??= new ObtenerSubastasQuery();

            var pagina = query.Pagina > 0 ? query.Pagina : 1;
            var tamanoPagina = Math.Clamp(query.TamanoPagina > 0 ? query.TamanoPagina : 20, 1, 100);

            var (subastas, total) = await _subastaRepository.BuscarAsync(
                query.CategoriaId,
                query.Estado,
                query.PrecioMin,
                query.PrecioMax,
                query.Orden,
                pagina,
                tamanoPagina,
                cancellationToken
            );

            var dtos = subastas.Select(SubastaMapper.MapearADto).ToList();
            return (dtos, total);
        }
    }
}
