using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PROYECTO_SUBASTA.Application.Commands.Subastas;
using PROYECTO_SUBASTA.Application.Repositories;
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

            // Configurar saldos de billeteras existentes para los tests
            var b1 = initContext.Billeteras.First(b => b.UsuarioId == 1);
            b1.SaldoTotal = 0; b1.SaldoDisponible = 0; b1.SaldoRetenido = 0;

            var b2 = initContext.Billeteras.First(b => b.UsuarioId == 2);
            b2.SaldoTotal = 50000; b2.SaldoDisponible = 50000; b2.SaldoRetenido = 0;

            var b3 = initContext.Billeteras.First(b => b.UsuarioId == 3);
            b3.SaldoTotal = 80000; b3.SaldoDisponible = 80000; b3.SaldoRetenido = 0;

            var b4 = initContext.Billeteras.First(b => b.UsuarioId == 4);
            b4.SaldoTotal = 1000; b4.SaldoDisponible = 1000; b4.SaldoRetenido = 0;

            initContext.SaveChanges();
        }

        public void Dispose()
        {
            _masterConnection.Dispose();
        }

        [Fact]
        public async Task RegistrarPuja_PrimerPostor_RetieneSaldoGarantiaYRegistraAsientoLedger()
        {
            // Arrange
            using var context = new SubastaDbContext(_options);
            var ahora = DateTime.UtcNow;
            var subasta = new Subasta
            {
                Id = 10,
                Titulo = "MacBook Pro",
                Descripcion = "Laptop",
                PrecioBase = 10000,
                IncrementoMinimo = 1000,
                FechaInicio = ahora.AddHours(-1),
                FechaFin = ahora.AddHours(2),
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

            var command = new RegistrarPujaCommand
            {
                SubastaId = 10,
                UsuarioId = 2,
                Monto = 12000,
                VersionCliente = 1
            };

            // Act
            var resultado = await handler.HandleAsync(command);

            // Assert
            Assert.NotNull(resultado);
            Assert.Equal((uint)2, resultado.Version);
            Assert.False(resultado.AntiSnipingActivado);

            // Verificar billetera del postor 2
            var billeteraPostor = await context.Billeteras.FirstAsync(b => b.UsuarioId == 2);
            Assert.Equal(50000, billeteraPostor.SaldoTotal);
            Assert.Equal(12000, billeteraPostor.SaldoRetenido);
            Assert.Equal(38000, billeteraPostor.SaldoDisponible);

            // Verificar asiento contable de retención en el ledger
            var ledger = await context.TransactionLedgers.FirstOrDefaultAsync(t => t.BilleteraId == billeteraPostor.Id && t.SubastaId == 10);
            Assert.NotNull(ledger);
            Assert.Equal(TipoTransaccion.RETENCION, ledger.Tipo);
            Assert.Equal(12000, ledger.Monto);
        }

        [Fact]
        public async Task RegistrarPuja_Sobrepuja_RetieneNuevoPostorYLiberarPostorAnteriorAtomicamente()
        {
            // Arrange
            using var context = new SubastaDbContext(_options);
            var ahora = DateTime.UtcNow;
            var subasta = new Subasta
            {
                Id = 11,
                Titulo = "iPhone 15",
                Descripcion = "Smartphone",
                PrecioBase = 10000,
                IncrementoMinimo = 1000,
                FechaInicio = ahora.AddHours(-1),
                FechaFin = ahora.AddHours(2),
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

            // Primer puja: Postor 2 oferta 12000
            await handler.HandleAsync(new RegistrarPujaCommand
            {
                SubastaId = 11,
                UsuarioId = 2,
                Monto = 12000,
                VersionCliente = 1
            });

            // Act: Sobrepuja: Postor 3 oferta 15000
            var resultadoSobrepuja = await handler.HandleAsync(new RegistrarPujaCommand
            {
                SubastaId = 11,
                UsuarioId = 3,
                Monto = 15000,
                VersionCliente = 2
            });

            // Assert
            Assert.NotNull(resultadoSobrepuja);
            Assert.Equal((uint)3, resultadoSobrepuja.Version);

            // Verificar postor 3 (nuevo líder retenido)
            var b3 = await context.Billeteras.FirstAsync(b => b.UsuarioId == 3);
            Assert.Equal(15000, b3.SaldoRetenido);
            Assert.Equal(65000, b3.SaldoDisponible);

            // Verificar postor 2 (líder superado: su saldo retenido debe haber vuelto a 0 y disponible reintegrado)
            var b2 = await context.Billeteras.FirstAsync(b => b.UsuarioId == 2);
            Assert.Equal(0, b2.SaldoRetenido);
            Assert.Equal(50000, b2.SaldoDisponible);

            // Verificar que exista el asiento de LIBERACION para el postor 2
            var ledgerLiberacion = await context.TransactionLedgers
                .FirstOrDefaultAsync(t => t.BilleteraId == b2.Id && t.SubastaId == 11 && t.Tipo == TipoTransaccion.LIBERACION);
            Assert.NotNull(ledgerLiberacion);
            Assert.Equal(12000, ledgerLiberacion.Monto);
        }

        [Fact]
        public async Task RegistrarPuja_SaldoInsuficiente_LanzaDominioExceptionYNoAlteraBilleteras()
        {
            // Arrange
            using var context = new SubastaDbContext(_options);
            var ahora = DateTime.UtcNow;
            var subasta = new Subasta
            {
                Id = 12,
                Titulo = "PlayStation 5",
                Descripcion = "Consola",
                PrecioBase = 5000,
                IncrementoMinimo = 500,
                FechaInicio = ahora.AddHours(-1),
                FechaFin = ahora.AddHours(1),
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

            var b4 = await context.Billeteras.FirstAsync(b => b.UsuarioId == 4);
            Assert.Equal(1000, b4.SaldoDisponible);
            Assert.Equal(0, b4.SaldoRetenido);

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
        public async Task AdjudicacionService_SubastaVencidaConPujas_FinalizaYTransfiereFondosEntreBilleteras()
        {
            // Arrange
            using var context = new SubastaDbContext(_options);
            var ahora = DateTime.UtcNow;

            var subasta = new Subasta
            {
                Id = 14,
                Titulo = "Cámara Vintage",
                Descripcion = "Fotografía",
                PrecioBase = 10000,
                IncrementoMinimo = 1000,
                FechaInicio = ahora.AddDays(-2),
                FechaFin = ahora.AddHours(-1), // Ya vencida
                Estado = "ACTIVA",
                CategoriaId = 1,
                VendedorId = 1,
                Version = 2
            };
            context.Subastas.Add(subasta);

            // Puja ganadora de Postor 2
            context.Pujas.Add(new Puja
            {
                Id = 100,
                SubastaId = 14,
                UsuarioId = 2,
                Monto = 15000,
                FechaCreacion = ahora.AddHours(-2)
            });

            // Billetera de postor 2 con saldo retenido previamente
            var bComprador = await context.Billeteras.FirstAsync(b => b.UsuarioId == 2);
            bComprador.SaldoRetenido = 15000;
            bComprador.SaldoDisponible = 35000;

            var bVendedor = await context.Billeteras.FirstAsync(b => b.UsuarioId == 1);
            var saldoVendedorInicial = bVendedor.SaldoTotal;

            await context.SaveChangesAsync();

            var scopeFactory = _serviceProvider.GetRequiredService<IServiceScopeFactory>();
            var auditoria = new AuditoriaService(context, scopeFactory, NullLogger<AuditoriaService>.Instance);
            var service = new AdjudicacionService(context, auditoria, NullLogger<AdjudicacionService>.Instance);

            // Act
            var resultado = await service.ProcesarSubastasVencidasAsync();

            // Assert
            Assert.Equal(1, resultado.Finalizadas);

            var subastaFinalizada = await context.Subastas.FirstAsync(s => s.Id == 14);
            Assert.Equal("FINALIZADA", subastaFinalizada.Estado);
            Assert.Equal(2, subastaFinalizada.GanadorId);
            Assert.Equal(15000, subastaFinalizada.PrecioFinal);

            // Billetera comprador: SaldoRetenido fue debitado hacia 0, SaldoTotal reducido en 15000
            var bCompradorFinal = await context.Billeteras.FirstAsync(b => b.UsuarioId == 2);
            Assert.Equal(0, bCompradorFinal.SaldoRetenido);
            Assert.Equal(35000, bCompradorFinal.SaldoTotal);
            Assert.Equal(35000, bCompradorFinal.SaldoDisponible);

            // Billetera vendedor: SaldoTotal y SaldoDisponible acreditados en 15000
            var bVendedorFinal = await context.Billeteras.FirstAsync(b => b.UsuarioId == 1);
            Assert.Equal(saldoVendedorInicial + 15000, bVendedorFinal.SaldoTotal);
            Assert.Equal(saldoVendedorInicial + 15000, bVendedorFinal.SaldoDisponible);

            // Asientos contables de liquidación PAGO y COBRO
            var ledgerPago = await context.TransactionLedgers.FirstOrDefaultAsync(t => t.SubastaId == 14 && t.Tipo == TipoTransaccion.PAGO);
            var ledgerCobro = await context.TransactionLedgers.FirstOrDefaultAsync(t => t.SubastaId == 14 && t.Tipo == TipoTransaccion.COBRO);
            Assert.NotNull(ledgerPago);
            Assert.NotNull(ledgerCobro);
            Assert.Equal(15000, ledgerPago.Monto);
            Assert.Equal(15000, ledgerCobro.Monto);
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
            Assert.Equal(1, resultado.Desiertas);

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
            Assert.Equal(1, resultado.Activadas);

            var subastaActiva = await context.Subastas.FirstAsync(s => s.Id == 16);
            Assert.Equal("ACTIVA", subastaActiva.Estado);
        }
    }
}
