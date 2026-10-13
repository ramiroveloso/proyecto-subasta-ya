using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PROYECTO_SUBASTA.Application.Commands.Subastas;
using PROYECTO_SUBASTA.Application.Queries.Subastas;

namespace PROYECTO_SUBASTA.Api.Workers
{
    public class SubastaBackgroundWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<SubastaBackgroundWorker> _logger;
        private readonly TimeSpan _periodo = TimeSpan.FromSeconds(5);

        public SubastaBackgroundWorker(IServiceScopeFactory scopeFactory, ILogger<SubastaBackgroundWorker> logger)
        {
            _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("SubastaBackgroundWorker iniciado. Verificando subastas cada {Segundos} segundos.", _periodo.TotalSeconds);

            using var timer = new PeriodicTimer(_periodo);
            while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    // 1. Activar subastas programadas que alcanzaron su fecha de inicio
                    try
                    {
                        using var scopeActivacion = _scopeFactory.CreateScope();
                        var activarHandler = scopeActivacion.ServiceProvider.GetRequiredService<ActivarSubastasProgramadasCommandHandler>();
                        var activadas = await activarHandler.HandleAsync(new ActivarSubastasProgramadasCommand(), stoppingToken);
                        if (activadas > 0)
                        {
                            _logger.LogInformation("[WORKER ACTIVACION] {Cantidad} subastas programadas pasaron a ACTIVA.", activadas);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "[WORKER ACTIVACION] Error durante la activación de subastas programadas.");
                    }

                    // 2. Obtener identificadores de subastas vencidas (operación de solo lectura)
                    List<int> idsVencidos;
                    using (var scopeQuery = _scopeFactory.CreateScope())
                    {
                        var queryHandler = scopeQuery.ServiceProvider.GetRequiredService<ObtenerSubastasPorCerrarQueryHandler>();
                        var resIds = await queryHandler.HandleAsync(new ObtenerSubastasPorCerrarQuery(), stoppingToken);
                        idsVencidos = resIds.ToList();
                    }

                    // 3. Procesar cada subasta en un scope independiente, capturando excepciones para no cortar el ciclo
                    foreach (var id in idsVencidos)
                    {
                        if (stoppingToken.IsCancellationRequested) break;

                        try
                        {
                            using var subastaScope = _scopeFactory.CreateScope();
                            var cerrarHandler = subastaScope.ServiceProvider.GetRequiredService<CerrarSubastaCommandHandler>();
                            var resultado = await cerrarHandler.HandleAsync(new CerrarSubastaCommand { SubastaId = id }, stoppingToken);

                            if (resultado.Exitoso && (resultado.Estado == "FINALIZADA" || resultado.Estado == "DESIERTA"))
                            {
                                _logger.LogInformation("[WORKER CIERRE] Subasta #{SubastaId} procesada. Estado: {Estado}", id, resultado.Estado);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "[WORKER CIERRE] Error al procesar subasta #{SubastaId}. Se continúa con la siguiente subasta sin interrumpir el ciclo.", id);
                        }
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Excepción no controlada durante el ciclo de verificación del Worker.");
                }
            }

            _logger.LogInformation("SubastaBackgroundWorker detenido limpiamente.");
        }
    }
}
