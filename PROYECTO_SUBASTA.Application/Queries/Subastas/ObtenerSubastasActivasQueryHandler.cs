using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PROYECTO_SUBASTA.Application.DTOs;
using PROYECTO_SUBASTA.Application.Exceptions;
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
        public string? OrdenarPor { get; set; } = "cierre";
        public string? Orden { get; set; } = "asc";
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 50;
    }

    public class ObtenerSubastasActivasQuery : ObtenerSubastasQuery
    {
    }

    public class SubastasConsultaResultado
    {
        public IEnumerable<SubastaResponseDto> Items { get; set; } = Enumerable.Empty<SubastaResponseDto>();
        public int TotalCount { get; set; }
    }

    public class ObtenerSubastasActivasQueryHandler
    {
        private readonly ISubastaRepository _subastaRepository;
        private readonly ICategoriaRepository _categoriaRepository;

        private static readonly HashSet<string> EstadosValidos = new(StringComparer.OrdinalIgnoreCase)
        {
            "ACTIVA", "PROGRAMADA", "FINALIZADA", "DESIERTA"
        };

        public ObtenerSubastasActivasQueryHandler(
            ISubastaRepository subastaRepository,
            ICategoriaRepository categoriaRepository)
        {
            _subastaRepository = subastaRepository ?? throw new ArgumentNullException(nameof(subastaRepository));
            _categoriaRepository = categoriaRepository ?? throw new ArgumentNullException(nameof(categoriaRepository));
        }

        public async Task<SubastasConsultaResultado> HandleAsync(ObtenerSubastasQuery query, CancellationToken cancellationToken = default)
        {
            if (query == null)
            {
                query = new ObtenerSubastasQuery();
            }

            // 1. Validación de Estado (ACTIVA/PROGRAMADA/FINALIZADA/DESIERTA; valor desconocido -> 400; null/vacio/TODAS -> sin filtro)
            string? estado = null;
            if (!string.IsNullOrWhiteSpace(query.Estado) && !string.Equals(query.Estado.Trim(), "TODAS", StringComparison.OrdinalIgnoreCase))
            {
                var est = query.Estado.Trim().ToUpperInvariant();
                if (!EstadosValidos.Contains(est))
                {
                    throw new ReglaNegocioException($"El estado '{query.Estado}' no es válido. Los estados permitidos son: ACTIVA, PROGRAMADA, FINALIZADA, DESIERTA.");
                }
                estado = est;
            }

            // 2. Validación de Precios (precioMin > precioMax -> 400)
            if (query.PrecioMin.HasValue && query.PrecioMax.HasValue && query.PrecioMin.Value > query.PrecioMax.Value)
            {
                throw new ReglaNegocioException("El precio mínimo no puede ser mayor al precio máximo.");
            }

            // 3. Validación de CategoriaId inexistente -> lista vacía
            if (query.CategoriaId.HasValue)
            {
                var categoriaExiste = await _categoriaRepository.ObtenerPorIdAsync(query.CategoriaId.Value);
                if (categoriaExiste == null)
                {
                    return new SubastasConsultaResultado
                    {
                        Items = Enumerable.Empty<SubastaResponseDto>(),
                        TotalCount = 0
                    };
                }
            }

            var pageNumber = query.PageNumber > 0 ? query.PageNumber : 1;
            var pageSize = query.PageSize > 0 ? query.PageSize : 50;
            if (pageSize > 100)
            {
                pageSize = 100;
            }

            // 4. Aplicación de filtros, ordenamiento y paginación en base de datos (IQueryable)
            var (items, totalCount) = await _subastaRepository.ObtenerFiltradasAsync(
                query.CategoriaId,
                estado,
                query.PrecioMin,
                query.PrecioMax,
                query.OrdenarPor,
                query.Orden,
                pageNumber,
                pageSize,
                cancellationToken);

            return new SubastasConsultaResultado
            {
                Items = items.Select(SubastaMapper.MapearADto).ToList(),
                TotalCount = totalCount
            };
        }

        public async Task<IEnumerable<SubastaResponseDto>> HandleAsync(ObtenerSubastasActivasQuery query, CancellationToken cancellationToken = default)
        {
            var res = await HandleAsync((ObtenerSubastasQuery)query, cancellationToken);
            return res.Items;
        }
    }
}
