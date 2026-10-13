using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PROYECTO_SUBASTA.Infrastructure.Data;

namespace PROYECTO_SUBASTA.IntegrationTests
{
    public class MySqlWebApplicationFactory : WebApplicationFactory<Program>
    {
        private readonly string? _mySqlConnectionString;

        public MySqlWebApplicationFactory()
        {
            _mySqlConnectionString = Environment.GetEnvironmentVariable("TEST_MYSQL_CONNECTION");
        }

        public bool TieneConexionConfigurada => !string.IsNullOrWhiteSpace(_mySqlConnectionString);

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((context, config) =>
            {
                var overrides = new Dictionary<string, string?>
                {
                    ["Worker:Habilitado"] = "false",
                    ["Demo:ReiniciarFechas"] = "false"
                };

                if (TieneConexionConfigurada)
                {
                    overrides["ConnectionStrings:DefaultConnection"] = _mySqlConnectionString;
                }

                config.AddInMemoryCollection(overrides);
            });

            builder.ConfigureServices(services =>
            {
                if (!TieneConexionConfigurada)
                {
                    return;
                }

                // Reemplazar SubastaDbContext por la conexión a la base de pruebas MySQL
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<SubastaDbContext>));
                if (descriptor != null)
                {
                    services.Remove(descriptor);
                }

                services.AddDbContext<SubastaDbContext>(options =>
                {
                    var serverVersion = ServerVersion.AutoDetect(_mySqlConnectionString);
                    options.UseMySql(_mySqlConnectionString!, serverVersion);
                });
            });
        }

        public async Task ReiniciarBaseDeDatosAsync()
        {
            if (!TieneConexionConfigurada) return;

            using var scope = Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<SubastaDbContext>();

            // Borrar y recrear esquema migrado limpio para la suite de pruebas
            await context.Database.EnsureDeletedAsync();
            await context.Database.MigrateAsync();
        }
    }
}
