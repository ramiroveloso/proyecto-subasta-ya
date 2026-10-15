using System;
using System.Threading;
using System.Threading.Tasks;
using PROYECTO_SUBASTA.Application.DTOs;
using PROYECTO_SUBASTA.Application.Mappers;
using PROYECTO_SUBASTA.Application.Repositories;

namespace PROYECTO_SUBASTA.Application.Queries.Subastas
{
    public class ObtenerSubastaPorIdQuery
    {
        public int Id { get; set; }
    }

    public class ObtenerSubastaPorIdQueryHandler
    {
        private readonly ISubastaRepository _subastaRepository;

        public ObtenerSubastaPorIdQueryHandler(ISubastaRepository subastaRepository)
        {
            _subastaRepository = subastaRepository ?? throw new ArgumentNullException(nameof(subastaRepository));
        }

        public async Task<SubastaResponseDto?> HandleAsync(ObtenerSubastaPorIdQuery query, CancellationToken cancellationToken = default)
        {
            if (query == null || query.Id <= 0)
                return null;

            var subasta = await _subastaRepository.ObtenerPorIdAsync(query.Id);
            if (subasta == null)
                return null;

            return SubastaMapper.MapearADto(subasta);
        }
    }
}
