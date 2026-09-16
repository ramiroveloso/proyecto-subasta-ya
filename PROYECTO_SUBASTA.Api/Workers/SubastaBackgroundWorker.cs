using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PROYECTO_SUBASTA.Application.UseCases;

namespace PROYECTO_SUBASTA.Api.Workers
{
    public class SubastaBackgroundWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<SubastaBackgroundWorker> _logger;
        private readonly TimeSpan _periodo = TimeSpan.FromSeconds(5);

        public SubastaBackgroundWorker(IServiceScopeFactory scopeFactory, ILogger<SubastaBackgroundWorker> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("SubastaBackgroundWorker iniciado. Verificando subastas cada {Segundos} segundos.", _periodo.TotalSeconds);

            using var timer = new PeriodicTimer(_periodo);
            while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var adjudicacionService = scope.ServiceProvider.GetRequiredService<IAdjudicacionService>();
                    var res = await adjudicacionService.ProcesarSubastasVencidasAsync(stoppingToken);

                    if (res.Finalizadas > 0 || res.Desiertas > 0 || res.Activadas > 0)
                    {
                        _logger.LogInformation("[WORKER CICLO] Activadas: {Activadas}, Finalizadas con ganador: {Finalizadas}, Desiertas: {Desiertas}",
                            res.Activadas, res.Finalizadas, res.Desiertas);
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
