using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PROYECTO_SUBASTA.Application.Repositories;

namespace PROYECTO_SUBASTA.Application.Queries.Subastas
{
    public class ObtenerSubastasPorCerrarQuery
    {
    }

    public class ObtenerSubastasPorCerrarQueryHandler
    {
        private readonly ISubastaRepository _subastaRepository;

        public ObtenerSubastasPorCerrarQueryHandler(ISubastaRepository subastaRepository)
        {
            _subastaRepository = subastaRepository ?? throw new ArgumentNullException(nameof(subastaRepository));
        }

        public async Task<IEnumerable<int>> HandleAsync(ObtenerSubastasPorCerrarQuery query, CancellationToken cancellationToken = default)
        {
            var ahoraUtc = DateTime.UtcNow;
            return await _subastaRepository.ObtenerVencidasParaCerrarIdsAsync(ahoraUtc);
        }
    }
}
