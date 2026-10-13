using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PROYECTO_SUBASTA.Application.UseCases;
using PROYECTO_SUBASTA.Infrastructure.Data;

namespace PROYECTO_SUBASTA.Infrastructure.Services
{
    public class DemoDataService : IDemoDataService
    {
        private readonly SubastaDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly ILogger<DemoDataService> _logger;

        public DemoDataService(
            SubastaDbContext context,
            IConfiguration configuration,
            ILogger<DemoDataService> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task ReubicarFechasDemoSiCorrespondeAsync(CancellationToken cancellationToken = default)
        {
            var reiniciarFechasConfig = _configuration["Demo:ReiniciarFechas"];
            var reiniciarFechas = bool.TryParse(reiniciarFechasConfig, out var flag) && flag;
            if (!reiniciarFechas)
            {
                _logger.LogInformation("Demo:ReiniciarFechas está desactivado. Fechas fijas de semillas preservadas.");
                return;
            }

            var ahoraUtc = DateTime.UtcNow;
            var subastasMovidas = new System.Collections.Generic.List<int>();

            var s1 = await _context.Subastas.FirstOrDefaultAsync(s => s.Id == 1, cancellationToken);
            if (s1 != null)
            {
                if (s1.Estado == "ACTIVA" || s1.Estado == "PROGRAMADA")
                {
                    s1.FechaInicio = ahoraUtc.AddMinutes(-20);
                    s1.FechaFin = ahoraUtc.AddMinutes(25);
                    s1.Estado = "ACTIVA";
                    subastasMovidas.Add(1);
                    _logger.LogInformation("DemoDataService: Subasta #1 reubicada con éxito (Estado: {Estado}, Cierre: {FechaFin:yyyy-MM-dd HH:mm:ss} UTC).", s1.Estado, s1.FechaFin);
                }
                else
                {
                    _logger.LogInformation("DemoDataService: Subasta #1 omitida por encontrarse en estado inmutable '{Estado}'.", s1.Estado);
                }
            }

            var s2 = await _context.Subastas.FirstOrDefaultAsync(s => s.Id == 2, cancellationToken);
            if (s2 != null)
            {
                if (s2.Estado == "ACTIVA" || s2.Estado == "PROGRAMADA")
                {
                    s2.FechaInicio = ahoraUtc.AddMinutes(-58);
                    s2.FechaFin = ahoraUtc.AddSeconds(120);
                    s2.Estado = "ACTIVA";
                    subastasMovidas.Add(2);
                    _logger.LogInformation("DemoDataService: Subasta #2 reubicada con éxito (Estado: {Estado}, Cierre: {FechaFin:yyyy-MM-dd HH:mm:ss} UTC).", s2.Estado, s2.FechaFin);
                }
                else
                {
                    _logger.LogInformation("DemoDataService: Subasta #2 omitida por encontrarse en estado inmutable '{Estado}'.", s2.Estado);
                }
            }

            var s3 = await _context.Subastas.FirstOrDefaultAsync(s => s.Id == 3, cancellationToken);
            if (s3 != null)
            {
                if (s3.Estado == "ACTIVA" || s3.Estado == "PROGRAMADA")
                {
                    s3.FechaInicio = ahoraUtc.AddHours(24);
                    s3.FechaFin = ahoraUtc.AddHours(48);
                    s3.Estado = "PROGRAMADA";
                    subastasMovidas.Add(3);
                    _logger.LogInformation("DemoDataService: Subasta #3 reubicada con éxito (Estado: {Estado}, Inicio: {FechaInicio:yyyy-MM-dd HH:mm:ss} UTC).", s3.Estado, s3.FechaInicio);
                }
                else
                {
                    _logger.LogInformation("DemoDataService: Subasta #3 omitida por encontrarse en estado inmutable '{Estado}'.", s3.Estado);
                }
            }

            if (subastasMovidas.Count > 0)
            {
                await _context.SaveChangesAsync(cancellationToken);
            }

            _logger.LogInformation("DemoDataService: Finalizada reubicación para demostración. Subastas modificadas: [{Subastas}].", string.Join(", ", subastasMovidas));
        }
    }
}
