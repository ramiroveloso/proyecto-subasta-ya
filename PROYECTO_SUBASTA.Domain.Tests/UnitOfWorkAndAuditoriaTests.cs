using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PROYECTO_SUBASTA.Domain.Entities;
using PROYECTO_SUBASTA.Infrastructure.Data;
using PROYECTO_SUBASTA.Infrastructure.Repositories;
using PROYECTO_SUBASTA.Infrastructure.Services;
using Xunit;

namespace PROYECTO_SUBASTA.Domain.Tests
{
    public class UnitOfWorkAndAuditoriaTests : IDisposable
    {
        private readonly string _connectionString;
        private readonly SqliteConnection _masterConnection;
        private readonly DbContextOptions<SubastaDbContext> _options;
        private readonly IServiceProvider _serviceProvider;

        public UnitOfWorkAndAuditoriaTests()
        {
            _connectionString = $"DataSource=test_{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
            _masterConnection = new SqliteConnection(_connectionString);
            _masterConnection.Open();

            _options = new DbContextOptionsBuilder<SubastaDbContext>()
                .UseSqlite(_connectionString)
                .Options;

            var services = new ServiceCollection();
            services.AddDbContext<SubastaDbContext>(opt => opt.UseSqlite(_connectionString));
            services.AddLogging();
            _serviceProvider = services.BuildServiceProvider();

            using var initContext = new SubastaDbContext(_options);
            initContext.Database.EnsureCreated();
        }

        public void Dispose()
        {
            _masterConnection.Dispose();
        }

        [Fact]
        public async Task RollbackTransaccional_DescartaLogDeAuditoriaYCambiosDeNegocio()
        {
            // Arrange
            using var context = new SubastaDbContext(_options);
            var scopeFactory = _serviceProvider.GetRequiredService<IServiceScopeFactory>();
            var logger = NullLogger<AuditoriaService>.Instance;

            var auditoriaService = new AuditoriaService(context, scopeFactory, logger);
            var unitOfWork = new UnitOfWork(context);

            // Iniciar transacción de negocio
            await unitOfWork.BeginTransactionAsync();

            // 1. Mutar entidad de negocio (billetera)
            var billetera = await context.Billeteras.FindAsync(1);
            Assert.NotNull(billetera);
            billetera.CargarSaldo(50000m);

            // 2. Registrar evento de auditoría de negocio dentro de la transacción activa
            await auditoriaService.RegistrarAsync(
                "PUJA_REGISTRADA",
                "Puja exitosa registrada para Subasta #1",
                usuarioId: 1
            );

            // Act: Simular que la operación falla (por OCC, validación o error) y se invoca Rollback
            await unitOfWork.RollbackTransactionAsync();

            // Assert: Verificar en un nuevo contexto que NO quedaron logs fantasma ni saldo modificado
            using var assertContext = new SubastaDbContext(_options);
            var billeteraSinModificar = await assertContext.Billeteras.FindAsync(1);
            Assert.NotNull(billeteraSinModificar);
            Assert.Equal(0m, billeteraSinModificar.SaldoTotal);

            var logsFantasma = await assertContext.LogsAuditoria
                .Where(l => l.Accion == "PUJA_REGISTRADA")
                .ToListAsync();

            Assert.Empty(logsFantasma);
        }

        [Fact]
        public async Task TransaccionConFalloConcurrencia_InvocaRollback_YNoGuardaLogsFantasma()
        {
            // Arrange
            using var context = new SubastaDbContext(_options);
            var scopeFactory = _serviceProvider.GetRequiredService<IServiceScopeFactory>();
            var logger = NullLogger<AuditoriaService>.Instance;

            var auditoriaService = new AuditoriaService(context, scopeFactory, logger);
            var unitOfWork = new UnitOfWork(context);

            await unitOfWork.BeginTransactionAsync();

            var billetera = await context.Billeteras.FindAsync(1);
            Assert.NotNull(billetera);
            billetera.CargarSaldo(30000m);

            // Se intenta registrar evento de auditoría de negocio dentro de la transacción
            await auditoriaService.RegistrarAsync(
                "PUJA_TRANSACCIONAL",
                "Intento de puja",
                usuarioId: 1
            );

            // Act: Simular captura de conflicto OCC y ejecución defensiva de Rollback
            bool excepcionOcurrida = false;
            try
            {
                throw new DbUpdateConcurrencyException("Simulación de colisión OCC");
            }
            catch (DbUpdateConcurrencyException)
            {
                excepcionOcurrida = true;
                await unitOfWork.RollbackTransactionAsync();
            }

            // Assert: Confirmar que la transacción se canceló y no dejó rastro en la base de datos
            Assert.True(excepcionOcurrida);

            using var assertContext = new SubastaDbContext(_options);
            var logsFantasma = await assertContext.LogsAuditoria
                .Where(l => l.Accion == "PUJA_TRANSACCIONAL")
                .ToListAsync();

            Assert.Empty(logsFantasma);

            var billeteraSinCambios = await assertContext.Billeteras.FindAsync(1);
            Assert.NotNull(billeteraSinCambios);
            Assert.Equal(0m, billeteraSinCambios.SaldoTotal);
        }

        [Fact]
        public async Task CommitTransaccional_PersisteEntidadYLogDeAuditoriaDeFormaAtomica()
        {
            // Arrange
            using var context = new SubastaDbContext(_options);
            var scopeFactory = _serviceProvider.GetRequiredService<IServiceScopeFactory>();
            var logger = NullLogger<AuditoriaService>.Instance;

            var auditoriaService = new AuditoriaService(context, scopeFactory, logger);
            var unitOfWork = new UnitOfWork(context);

            // Act
            await unitOfWork.BeginTransactionAsync();

            var billetera = await context.Billeteras.FindAsync(1);
            Assert.NotNull(billetera);
            billetera.CargarSaldo(75000m);

            await auditoriaService.RegistrarAsync(
                "PUJA_CONFIRMADA",
                "Puja confirmada y persistida en UnitOfWork",
                usuarioId: 1
            );

            await unitOfWork.CommitTransactionAsync();

            // Assert
            using var assertContext = new SubastaDbContext(_options);
            var billeteraActualizada = await assertContext.Billeteras.FindAsync(1);
            Assert.NotNull(billeteraActualizada);
            Assert.Equal(75000m, billeteraActualizada.SaldoTotal);

            var logGuardado = await assertContext.LogsAuditoria
                .FirstOrDefaultAsync(l => l.Accion == "PUJA_CONFIRMADA");

            Assert.NotNull(logGuardado);
            Assert.Equal(1, logGuardado.UsuarioId);
        }

        [Fact]
        public async Task RegistrarFalloAsync_EnScopeAislado_PersisteLogCorrectamente()
        {
            // Arrange
            using var context = new SubastaDbContext(_options);
            var scopeFactory = _serviceProvider.GetRequiredService<IServiceScopeFactory>();
            var logger = NullLogger<AuditoriaService>.Instance;

            var auditoriaService = new AuditoriaService(context, scopeFactory, logger);

            // Act: Registrar evento aislado (usado ante fallos capturados en ExceptionMiddleware)
            await auditoriaService.RegistrarFalloAsync(
                "PUJA_RECHAZADA_CONCURRENCIA",
                "Colisión detectada por token de concurrencia desactualizado",
                usuarioId: 2
            );

            // Assert: Verificar que el log de auditoría se persistió exitosamente en su propio scope
            using var assertContext = new SubastaDbContext(_options);
            var logFallo = await assertContext.LogsAuditoria
                .FirstOrDefaultAsync(l => l.Accion == "PUJA_RECHAZADA_CONCURRENCIA");

            Assert.NotNull(logFallo);
            Assert.Equal(2, logFallo.UsuarioId);
            Assert.Contains("Colisión detectada", logFallo.Detalle);
        }
    }
}
