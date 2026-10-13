using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Primitives;
using PROYECTO_SUBASTA.Infrastructure.Data;
using PROYECTO_SUBASTA.Infrastructure.Services;
using Xunit;

namespace PROYECTO_SUBASTA.Domain.Tests
{
    public class DemoDataServiceTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly DbContextOptions<SubastaDbContext> _options;

        private class FakeConfiguration : IConfiguration
        {
            private readonly Dictionary<string, string?> _data;
            public FakeConfiguration(Dictionary<string, string?> data) => _data = data;
            public string? this[string key]
            {
                get => _data.TryGetValue(key, out var val) ? val : null;
                set => _data[key] = value;
            }
            public IEnumerable<IConfigurationSection> GetChildren() => Enumerable.Empty<IConfigurationSection>();
            public IChangeToken GetReloadToken() => default!;
            public IConfigurationSection GetSection(string key) => default!;
        }

        public DemoDataServiceTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            _options = new DbContextOptionsBuilder<SubastaDbContext>()
                .UseSqlite(_connection)
                .Options;

            using var context = new SubastaDbContext(_options);
            context.Database.EnsureCreated();
        }

        public void Dispose()
        {
            _connection.Dispose();
        }

        [Fact]
        public async Task DemoDataService_CuandoSubastasSonActivasOProgramadas_ReubicaFechasYPreservaInvariantes()
        {
            // Arrange
            var configuration = new FakeConfiguration(new Dictionary<string, string?>
            {
                { "Demo:ReiniciarFechas", "true" }
            });

            using var context = new SubastaDbContext(_options);
            var service = new DemoDataService(context, configuration, NullLogger<DemoDataService>.Instance);
            var ahoraAntes = DateTime.UtcNow;

            // Act
            await service.ReubicarFechasDemoSiCorrespondeAsync();

            // Assert
            using var assertContext = new SubastaDbContext(_options);
            var s1 = await assertContext.Subastas.FindAsync(1);
            var s2 = await assertContext.Subastas.FindAsync(2);
            var s3 = await assertContext.Subastas.FindAsync(3);

            Assert.NotNull(s1);
            Assert.Equal("ACTIVA", s1.Estado);
            Assert.True(s1.FechaFin > ahoraAntes);
            Assert.True(s1.FechaFin <= DateTime.UtcNow.AddMinutes(26));
            Assert.Null(s1.GanadorId);
            Assert.Null(s1.PrecioFinal);

            Assert.NotNull(s2);
            Assert.Equal("ACTIVA", s2.Estado);
            Assert.True(s2.FechaFin > ahoraAntes);
            Assert.True(s2.FechaFin <= DateTime.UtcNow.AddSeconds(130));
            Assert.Null(s2.GanadorId);
            Assert.Null(s2.PrecioFinal);

            Assert.NotNull(s3);
            Assert.Equal("PROGRAMADA", s3.Estado);
            Assert.True(s3.FechaInicio > ahoraAntes);
            Assert.Null(s3.GanadorId);
            Assert.Null(s3.PrecioFinal);
        }

        [Fact]
        public async Task DemoDataService_CuandoSubastaEstaFinalizadaODesierta_NuncaCambiaEstadoNiFechasNiGanador()
        {
            // Arrange: Poner subastas 1 y 2 en FINALIZADA y DESIERTA artificialmente para probar invariabilidad
            using (var setupContext = new SubastaDbContext(_options))
            {
                var s1 = await setupContext.Subastas.FindAsync(1);
                var s2 = await setupContext.Subastas.FindAsync(2);

                s1!.Estado = "FINALIZADA";
                s1.GanadorId = 2;
                s1.PrecioFinal = 60000m;
                s1.FechaInicio = new DateTime(2025, 1, 1, 10, 0, 0, DateTimeKind.Utc);
                s1.FechaFin = new DateTime(2025, 1, 2, 10, 0, 0, DateTimeKind.Utc);

                s2!.Estado = "DESIERTA";
                s2.GanadorId = null;
                s2.PrecioFinal = null;
                s2.FechaInicio = new DateTime(2025, 2, 1, 10, 0, 0, DateTimeKind.Utc);
                s2.FechaFin = new DateTime(2025, 2, 2, 10, 0, 0, DateTimeKind.Utc);

                await setupContext.SaveChangesAsync();
            }

            var configuration = new FakeConfiguration(new Dictionary<string, string?>
            {
                { "Demo:ReiniciarFechas", "true" }
            });

            using var context = new SubastaDbContext(_options);
            var service = new DemoDataService(context, configuration, NullLogger<DemoDataService>.Instance);

            // Act: Intentar reubicar
            await service.ReubicarFechasDemoSiCorrespondeAsync();

            // Assert: Verificar que s1 y s2 quedaron INMUTABLES
            using var assertContext = new SubastaDbContext(_options);
            var s1Final = await assertContext.Subastas.FindAsync(1);
            var s2Final = await assertContext.Subastas.FindAsync(2);

            Assert.NotNull(s1Final);
            Assert.Equal("FINALIZADA", s1Final.Estado);
            Assert.Equal(2, s1Final.GanadorId);
            Assert.Equal(60000m, s1Final.PrecioFinal);
            Assert.Equal(new DateTime(2025, 1, 1, 10, 0, 0, DateTimeKind.Utc), s1Final.FechaInicio);
            Assert.Equal(new DateTime(2025, 1, 2, 10, 0, 0, DateTimeKind.Utc), s1Final.FechaFin);

            Assert.NotNull(s2Final);
            Assert.Equal("DESIERTA", s2Final.Estado);
            Assert.Null(s2Final.GanadorId);
            Assert.Null(s2Final.PrecioFinal);
            Assert.Equal(new DateTime(2025, 2, 1, 10, 0, 0, DateTimeKind.Utc), s2Final.FechaInicio);
            Assert.Equal(new DateTime(2025, 2, 2, 10, 0, 0, DateTimeKind.Utc), s2Final.FechaFin);
        }

        [Fact]
        public async Task DemoDataService_CuandoFlagEstaDesactivado_NoModificaNingunaSubasta()
        {
            // Arrange
            var configuration = new FakeConfiguration(new Dictionary<string, string?>
            {
                { "Demo:ReiniciarFechas", "false" }
            });

            DateTime s1FinOriginal;
            using (var inspectContext = new SubastaDbContext(_options))
            {
                var s1 = await inspectContext.Subastas.FindAsync(1);
                s1FinOriginal = s1!.FechaFin;
            }

            using var context = new SubastaDbContext(_options);
            var service = new DemoDataService(context, configuration, NullLogger<DemoDataService>.Instance);

            // Act
            await service.ReubicarFechasDemoSiCorrespondeAsync();

            // Assert
            using var assertContext = new SubastaDbContext(_options);
            var s1Assert = await assertContext.Subastas.FindAsync(1);
            Assert.Equal(s1FinOriginal, s1Assert!.FechaFin);
        }
    }
}
