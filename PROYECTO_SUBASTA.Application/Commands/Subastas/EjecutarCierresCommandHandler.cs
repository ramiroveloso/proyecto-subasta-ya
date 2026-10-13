using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PROYECTO_SUBASTA.Application.Queries.Subastas;

namespace PROYECTO_SUBASTA.Application.Commands.Subastas
{
    public class EjecutarCierresCommand
    {
    }

    public class EjecutarCierresResultadoDto
    {
        public int Activadas { get; set; }
        public int Finalizadas { get; set; }
        public int Desiertas { get; set; }
        public int Fallidas { get; set; }
        public int Omitidas { get; set; }
    }

    public class EjecutarCierresCommandHandler
    {
        private readonly ActivarSubastasProgramadasCommandHandler _activarHandler;
        private readonly ObtenerSubastasPorCerrarQueryHandler _obtenerPorCerrarHandler;
        private readonly CerrarSubastaCommandHandler _cerrarSubastaHandler;
        private readonly ILogger<EjecutarCierresCommandHandler> _logger;

        public EjecutarCierresCommandHandler(
            ActivarSubastasProgramadasCommandHandler activarHandler,
            ObtenerSubastasPorCerrarQueryHandler obtenerPorCerrarHandler,
            CerrarSubastaCommandHandler cerrarSubastaHandler,
            ILogger<EjecutarCierresCommandHandler> logger)
        {
            _activarHandler = activarHandler ?? throw new ArgumentNullException(nameof(activarHandler));
            _obtenerPorCerrarHandler = obtenerPorCerrarHandler ?? throw new ArgumentNullException(nameof(obtenerPorCerrarHandler));
            _cerrarSubastaHandler = cerrarSubastaHandler ?? throw new ArgumentNullException(nameof(cerrarSubastaHandler));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<EjecutarCierresResultadoDto> HandleAsync(EjecutarCierresCommand command, CancellationToken cancellationToken = default)
        {
            var resultado = new EjecutarCierresResultadoDto();

            // 1. Activar subastas programadas cuyo período ha iniciado
            try
            {
                resultado.Activadas = await _activarHandler.HandleAsync(new ActivarSubastasProgramadasCommand(), cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al activar subastas programadas durante el ciclo de cierres.");
            }

            // 2. Obtener los identificadores de subastas vencidas
            var vencidasIds = await _obtenerPorCerrarHandler.HandleAsync(new ObtenerSubastasPorCerrarQuery(), cancellationToken);

            // 3. Procesar cada subasta de forma aislada
            foreach (var subastaId in vencidasIds)
            {
                try
                {
                    var res = await _cerrarSubastaHandler.HandleAsync(new CerrarSubastaCommand { SubastaId = subastaId }, cancellationToken);
                    if (res.Exitoso)
                    {
                        if (res.Estado == "FINALIZADA")
                            resultado.Finalizadas++;
                        else if (res.Estado == "DESIERTA")
                            resultado.Desiertas++;
                        else
                            resultado.Omitidas++;
                    }
                    else
                    {
                        resultado.Omitidas++;
                    }
                }
                catch (Exception ex)
                {
                    resultado.Fallidas++;
                    _logger.LogWarning(ex, "Fallo al procesar el cierre de la subasta #{SubastaId}. Se continuará con las siguientes subastas del ciclo.", subastaId);
                }
            }

            return resultado;
        }
    }
}
