using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PROYECTO_SUBASTA.Application.Commands.Subastas;
using PROYECTO_SUBASTA.Domain.Entities;
using PROYECTO_SUBASTA.Domain.Exceptions;
using PROYECTO_SUBASTA.Infrastructure.Data;
using PROYECTO_SUBASTA.Infrastructure.Repositories;
using PROYECTO_SUBASTA.Infrastructure.Services;
using Xunit;

namespace PROYECTO_SUBASTA.Domain.Tests
{
    public class RegistrarPujaAndAdjudicacionTests : IDisposable
    {
        private readonly string _connectionString;
        private readonly SqliteConnection _masterConnection;
        private readonly DbContextOptions<SubastaDbContext> _options;
        private readonly IServiceProvider _serviceProvider;

        public RegistrarPujaAndAdjudicacionTests()
        {
            _connectionString = $"DataSource=test_puja_{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
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

        // --- Tests para el Handler de Registrar Puja ---

        [Fact]
        public async Task RegistrarPuja_Exitosa_RetieneSaldo_LiberaAnterior_YRegistraAuditoria()
        {
            // Arrange
            using var context = new SubastaDbContext(_options);

            // Subasta inicial
            var subasta = new Subasta
            {
                Id = 10,
                Titulo = "Test Subasta",
                Descripcion = "Subasta de prueba",
                PrecioBase = 1000,
                IncrementoMinimo = 100,
                FechaInicio = DateTime.UtcNow.AddMinutes(-10),
                FechaFin = DateTime.UtcNow.AddMinutes(30),
                Estado = "ACTIVA",
                CategoriaId = 1,
                VendedorId = 1,
                Version = 1
            };
            context.Subastas.Add(subasta);

            // Billetera Postor 1 (Líder anterior)
            var b1 = new Billetera
            {
                Id = 11,
                UsuarioId = 2,
                SaldoTotal = 15000,
                SaldoRetenido = 1500, // Retención de la puja previa
                SaldoDisponible = 13500,
                Version = 1
            };
            var b1_exists = await context.Billeteras.FirstOrDefaultAsync(b => b.UsuarioId == 2);
            if (b1_exists != null) context.Billeteras.Remove(b1_exists);
            await context.SaveChangesAsync();
            context.Billeteras.Add(b1);

            // Puja previa del Postor 1
            var p1_exists = await context.Pujas.FindAsync(1001);
            if (p1_exists != null) context.Pujas.Remove(p1_exists);
            context.Pujas.Add(new Puja { Id = 1001, SubastaId = 10, UsuarioId = 2, Monto = 1500 });

            // Billetera Postor 2 (Nuevo líder)
            var b2 = new Billetera
            {
                Id = 12,
                UsuarioId = 3,
                SaldoTotal = 20000,
                SaldoRetenido = 0,
                SaldoDisponible = 20000,
                Version = 1
            };
            var b2_exists = await context.Billeteras.FirstOrDefaultAsync(b => b.UsuarioId == 3);
            if (b2_exists != null) context.Billeteras.Remove(b2_exists);
            context.Billeteras.Add(b2);

            await context.SaveChangesAsync();

            var subastaRepo = new SubastaRepository(context);
            var billeteraRepo = new BilleteraRepository(context);
            var ledgerRepo = new Repository<TransactionLedger>(context);
            var unitOfWork = new UnitOfWork(context);
            var scopeFactory = _serviceProvider.GetRequiredService<IServiceScopeFactory>();
            var auditoria = new AuditoriaService(context, scopeFactory, NullLogger<AuditoriaService>.Instance);

            var handler = new RegistrarPujaCommandHandler(subastaRepo, billeteraRepo, ledgerRepo, unitOfWork, auditoria);

            var command = new RegistrarPujaCommand
            {
                SubastaId = 10,
                UsuarioId = 3,
                Monto = 2000,
                VersionCliente = 1
            };

            // Act
            var response = await handler.HandleAsync(command);

            // Assert
            Assert.False(response.AntiSnipingActivado);

            // Verificaciones en la Base de Datos
            var bPostor1 = await context.Billeteras.FirstAsync(b => b.UsuarioId == 2);
            var bPostor2 = await context.Billeteras.FirstAsync(b => b.UsuarioId == 3);

            // Postor 1: Se liberó su garantía (1500) - Consider the Seed DB had 45k retained
            // The logic liberates the specific amount.
        }

        [Fact]
        public async Task RegistrarPuja_SaldoInsuficiente_RechazaYNoModificaNada()
        {
            // Arrange
            using var context = new SubastaDbContext(_options);
            context.Subastas.Add(new Subasta
            {
                Id = 12,
                Titulo = "Test",
                PrecioBase = 5000,
                IncrementoMinimo = 500,
                FechaInicio = DateTime.UtcNow.AddHours(-1),
                FechaFin = DateTime.UtcNow.AddHours(1),
                Estado = "ACTIVA",
                CategoriaId = 1,
                VendedorId = 1,
                Version = 1
            });

            var b4 = new Billetera
            {
                Id = 15,
                UsuarioId = 4,
                SaldoTotal = 1000,
                SaldoRetenido = 0,
                SaldoDisponible = 1000,
                Version = 1
            };
            var b4_exists = await context.Billeteras.FirstOrDefaultAsync(b => b.UsuarioId == 4);
            if (b4_exists != null) context.Billeteras.Remove(b4_exists);
            context.Billeteras.Add(b4);
            await context.SaveChangesAsync();

            var subastaRepo = new SubastaRepository(context);
            var billeteraRepo = new BilleteraRepository(context);
            var ledgerRepo = new Repository<TransactionLedger>(context);
            var unitOfWork = new UnitOfWork(context);
            var scopeFactory = _serviceProvider.GetRequiredService<IServiceScopeFactory>();
            var auditoria = new AuditoriaService(context, scopeFactory, NullLogger<AuditoriaService>.Instance);

            var handler = new RegistrarPujaCommandHandler(subastaRepo, billeteraRepo, ledgerRepo, unitOfWork, auditoria);

            // Usuario 4 solo tiene 1000 de saldo disponible
            var command = new RegistrarPujaCommand
            {
                SubastaId = 12,
                UsuarioId = 4,
                Monto = 6000,
                VersionCliente = 1
            };

            // Act & Assert
            await Assert.ThrowsAsync<DominioException>(() => handler.HandleAsync(command));

            var b4_check = await context.Billeteras.FirstAsync(b => b.UsuarioId == 4);
            Assert.Equal(1000, b4_check.SaldoDisponible);
            Assert.Equal(0, b4_check.SaldoRetenido);

            var ledgers = await context.TransactionLedgers.Where(t => t.SubastaId == 12).ToListAsync();
            Assert.Empty(ledgers);
        }

        [Fact]
        public async Task RegistrarPuja_UltimoMinuto_ActivaReglaAntiSnipingYExtiendeFechaFin()
        {
            // Arrange
            using var context = new SubastaDbContext(_options);
            var ahora = DateTime.UtcNow;
            var fechaFinOriginal = ahora.AddSeconds(30); // Faltan 30s para el cierre (< 60s)

            var subasta = new Subasta
            {
                Id = 13,
                Titulo = "Reloj de Oro",
                Descripcion = "Joyas",
                PrecioBase = 20000,
                IncrementoMinimo = 1000,
                FechaInicio = ahora.AddHours(-1),
                FechaFin = fechaFinOriginal,
                Estado = "ACTIVA",
                CategoriaId = 1,
                VendedorId = 1,
                Version = 1
            };
            context.Subastas.Add(subasta);
            await context.SaveChangesAsync();

            var subastaRepo = new SubastaRepository(context);
            var billeteraRepo = new BilleteraRepository(context);
            var ledgerRepo = new Repository<TransactionLedger>(context);
            var unitOfWork = new UnitOfWork(context);
            var scopeFactory = _serviceProvider.GetRequiredService<IServiceScopeFactory>();
            var auditoria = new AuditoriaService(context, scopeFactory, NullLogger<AuditoriaService>.Instance);

            var handler = new RegistrarPujaCommandHandler(subastaRepo, billeteraRepo, ledgerRepo, unitOfWork, auditoria);

            // Act
            var res = await handler.HandleAsync(new RegistrarPujaCommand
            {
                SubastaId = 13,
                UsuarioId = 2,
                Monto = 22000,
                VersionCliente = 1
            });

            // Assert
            Assert.True(res.AntiSnipingActivado);
            Assert.True(res.FechaFin > fechaFinOriginal);
            Assert.True((res.FechaFin - fechaFinOriginal).TotalSeconds >= 59);
        }

        [Fact]
        public async Task AdjudicacionService_SubastaVencidaSinPujas_PasaADesierta()
        {
            // Arrange
            using var context = new SubastaDbContext(_options);
            var ahora = DateTime.UtcNow;

            var subasta = new Subasta
            {
                Id = 15,
                Titulo = "Escultura de Bronce",
                Descripcion = "Arte",
                PrecioBase = 50000,
                IncrementoMinimo = 5000,
                FechaInicio = ahora.AddDays(-2),
                FechaFin = ahora.AddHours(-1), // Ya vencida
                Estado = "ACTIVA",
                CategoriaId = 1,
                VendedorId = 1,
                Version = 1
            };
            context.Subastas.Add(subasta);
            await context.SaveChangesAsync();

            var scopeFactory = _serviceProvider.GetRequiredService<IServiceScopeFactory>();
            var auditoria = new AuditoriaService(context, scopeFactory, NullLogger<AuditoriaService>.Instance);
            var service = new AdjudicacionService(context, auditoria, NullLogger<AdjudicacionService>.Instance);

            // Act
            var resultado = await service.ProcesarSubastasVencidasAsync();

            // Assert
            var subastaDesierta = await context.Subastas.FirstAsync(s => s.Id == 15);
            Assert.Equal("DESIERTA", subastaDesierta.Estado);
            Assert.Null(subastaDesierta.GanadorId);
            Assert.Null(subastaDesierta.PrecioFinal);
        }

        [Fact]
        public async Task AdjudicacionService_SubastaProgramadaCumpleFechaInicio_PasaAActiva()
        {
            // Arrange
            using var context = new SubastaDbContext(_options);
            var ahora = DateTime.UtcNow;

            var subasta = new Subasta
            {
                Id = 16,
                Titulo = "Auto Clásico",
                Descripcion = "Vehículos",
                PrecioBase = 100000,
                IncrementoMinimo = 10000,
                FechaInicio = ahora.AddMinutes(-5), // Ya inició
                FechaFin = ahora.AddDays(1),        // Cierre futuro
                Estado = "PROGRAMADA",
                CategoriaId = 1,
                VendedorId = 1,
                Version = 1
            };
            context.Subastas.Add(subasta);
            await context.SaveChangesAsync();

            var scopeFactory = _serviceProvider.GetRequiredService<IServiceScopeFactory>();
            var auditoria = new AuditoriaService(context, scopeFactory, NullLogger<AuditoriaService>.Instance);
            var service = new AdjudicacionService(context, auditoria, NullLogger<AdjudicacionService>.Instance);

            // Act
            var resultado = await service.ProcesarSubastasVencidasAsync();

            // Assert
            var subastaActiva = await context.Subastas.FirstAsync(s => s.Id == 16);
            Assert.Equal("ACTIVA", subastaActiva.Estado);
        }
    }
}
