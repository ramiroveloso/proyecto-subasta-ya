using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PROYECTO_SUBASTA.Application.Repositories;
using PROYECTO_SUBASTA.Application.UseCases;

namespace PROYECTO_SUBASTA.Application.Commands.Subastas
{
    public class ActivarSubastasProgramadasCommand
    {
    }

    public class ActivarSubastasProgramadasCommandHandler
    {
        private readonly ISubastaRepository _subastaRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IAuditoriaService _auditoriaService;
        private readonly ILogger<ActivarSubastasProgramadasCommandHandler> _logger;

        public ActivarSubastasProgramadasCommandHandler(
            ISubastaRepository subastaRepository,
            IUnitOfWork unitOfWork,
            IAuditoriaService auditoriaService,
            ILogger<ActivarSubastasProgramadasCommandHandler> logger)
        {
            _subastaRepository = subastaRepository ?? throw new ArgumentNullException(nameof(subastaRepository));
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _auditoriaService = auditoriaService ?? throw new ArgumentNullException(nameof(auditoriaService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<int> HandleAsync(ActivarSubastasProgramadasCommand command, CancellationToken cancellationToken = default)
        {
            var ahoraUtc = DateTime.UtcNow;
            var subastasPorActivar = (await _subastaRepository.ObtenerProgramadasParaActivarAsync(ahoraUtc)).ToList();

            if (!subastasPorActivar.Any())
            {
                return 0;
            }

            int activadas = 0;
            foreach (var s in subastasPorActivar)
            {
                var estadoPrevio = s.Estado;
                s.Activar(ahoraUtc);
                await _subastaRepository.ActualizarAsync(s);
                activadas++;

                await _auditoriaService.RegistrarAsync(
                    "CAMBIO_ESTADO_SUBASTA",
                    $"Subasta #{s.Id} ('{s.Titulo}') cambió de {estadoPrevio} a ACTIVA al iniciar su período.",
                    null
                );
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Se activaron {Cantidad} subastas programadas.", activadas);

            return activadas;
        }
    }
}
