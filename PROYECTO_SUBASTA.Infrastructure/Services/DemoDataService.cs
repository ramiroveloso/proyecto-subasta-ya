using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PROYECTO_SUBASTA.Infrastructure.Data;
using System;
using System.Linq;

namespace PROYECTO_SUBASTA.Infrastructure.Services
{
    public class DemoDataService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IConfiguration _configuration;
        private readonly ILogger<DemoDataService> _logger;

        public DemoDataService(IServiceProvider serviceProvider, IConfiguration configuration, ILogger<DemoDataService> logger)
        {
            _serviceProvider = serviceProvider;
            _configuration = configuration;
            _logger = logger;
        }

        public void Initialize()
        {
            bool.TryParse(_configuration["Demo:ReiniciarFechas"], out var reiniciarFechas);

            if (!reiniciarFechas)
            {
                _logger.LogInformation("Demo:ReiniciarFechas está deshabilitado. No se modificarán las fechas de las subastas.");
                return;
            }

            _logger.LogInformation("Ejecutando reinicio de fechas para Demo...");

            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<SubastaDbContext>();

            var ahoraUtc = DateTime.UtcNow;

            var subasta1 = context.Subastas.Find(1);
            if (subasta1 != null)
            {
                subasta1.FechaInicio = ahoraUtc.AddMinutes(-20);
                subasta1.FechaFin = ahoraUtc.AddMinutes(25);
                subasta1.Estado = "ACTIVA";
                if (subasta1.Version == 0) subasta1.Version = 3;
            }

            var subasta2 = context.Subastas.Find(2);
            if (subasta2 != null)
            {
                subasta2.FechaInicio = ahoraUtc.AddMinutes(-58);
                subasta2.FechaFin = ahoraUtc.AddSeconds(90);
                subasta2.Estado = "ACTIVA";
                if (subasta2.Version == 0) subasta2.Version = 1;
            }

            var subasta3 = context.Subastas.Find(3);
            if (subasta3 != null)
            {
                subasta3.FechaInicio = ahoraUtc.AddHours(24);
                subasta3.FechaFin = ahoraUtc.AddHours(48);
                subasta3.Estado = "PROGRAMADA";
                if (subasta3.Version == 0) subasta3.Version = 1;
            }

            context.SaveChanges();
            _logger.LogInformation("Fechas de subastas de demo reiniciadas exitosamente.");
        }
    }
}
