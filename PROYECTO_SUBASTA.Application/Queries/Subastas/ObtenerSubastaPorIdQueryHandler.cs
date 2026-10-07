using System;
using System.Threading;
using System.Threading.Tasks;
using PROYECTO_SUBASTA.Application.DTOs;
using PROYECTO_SUBASTA.Application.Repositories;
using PROYECTO_SUBASTA.Application.UseCases;

namespace PROYECTO_SUBASTA.Application.Queries.Subastas
{
    public class ObtenerSubastaPorIdQuery
    {
        public int Id { get; set; }
    }

    public class ObtenerSubastaPorIdQueryHandler
    {
        private readonly ISubastaRepository _subastaRepository;
        private readonly IAdjudicacionService _adjudicacionService;

        public ObtenerSubastaPorIdQueryHandler(
            ISubastaRepository subastaRepository,
            IAdjudicacionService adjudicacionService)
        {
            _subastaRepository = subastaRepository ?? throw new ArgumentNullException(nameof(subastaRepository));
            _adjudicacionService = adjudicacionService ?? throw new ArgumentNullException(nameof(adjudicacionService));
        }

        public async Task<SubastaResponseDto?> HandleAsync(ObtenerSubastaPorIdQuery query, CancellationToken cancellationToken = default)
        {
            if (query == null || query.Id <= 0)
                return null;

            // Sincronizar y cerrar subastas vencidas antes de obtener por ID
            await _adjudicacionService.ProcesarSubastasVencidasAsync(cancellationToken);

            var subasta = await _subastaRepository.ObtenerPorIdAsync(query.Id);
            if (subasta == null)
                return null;

            return ObtenerSubastasActivasQueryHandler.MapearADto(subasta);
        }
    }
}
