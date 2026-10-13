using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PROYECTO_SUBASTA.Application.Commands.Subastas;
using PROYECTO_SUBASTA.Application.Configuration;
using PROYECTO_SUBASTA.Application.Exceptions;
using PROYECTO_SUBASTA.Application.Repositories;
using PROYECTO_SUBASTA.Application.UseCases;
using PROYECTO_SUBASTA.Domain.Entities;
using PROYECTO_SUBASTA.Domain.Exceptions;
using PROYECTO_SUBASTA.Infrastructure.Data;
using PROYECTO_SUBASTA.Infrastructure.Repositories;
using PROYECTO_SUBASTA.Infrastructure.Services;
using Xunit;

namespace PROYECTO_SUBASTA.Domain.Tests
{
    public class RegistrarPujaFase3Tests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly DbContextOptions<SubastaDbContext> _options;
        private readonly IServiceProvider _serviceProvider;

        public RegistrarPujaFase3Tests()
        {
            _connection = new SqliteConnection($"DataSource=test_fase3_{Guid.NewGuid():N};Mode=Memory;Cache=Shared");
            _connection.Open();

            _options = new DbContextOptionsBuilder<SubastaDbContext>()
                .UseSqlite(_connection)
                .Options;

            var services = new ServiceCollection();
            services.AddDbContext<SubastaDbContext>(opt => opt.UseSqlite(_connection));
            services.AddLogging();
            _serviceProvider = services.BuildServiceProvider();

            using var context = new SubastaDbContext(_options);
            context.Database.EnsureCreated();
        }

        public void Dispose()
        {
            _connection.Dispose();
        }

        private (RegistrarPujaCommandHandler Handler, SubastaDbContext Context) CrearHandler(
            SubastaDbContext context,
            AntiSnipingOptions? options = null,
            IAuditoriaService? customAuditoria = null)
        {
            var subastaRepo = new SubastaRepository(context);
            var billeteraRepo = new BilleteraRepository(context);
            var ledgerRepo = new Repository<TransactionLedger>(context);
            var unitOfWork = new UnitOfWork(context);

            var scopeFactory = _serviceProvider.GetRequiredService<IServiceScopeFactory>();
            var auditoria = customAuditoria ?? new AuditoriaService(context, scopeFactory, NullLogger<AuditoriaService>.Instance);
            var optWrapper = Options.Create(options ?? new AntiSnipingOptions { UmbralSegundos = 60, ExtensionSegundos = 120 });

            var handler = new RegistrarPujaCommandHandler(subastaRepo, billeteraRepo, ledgerRepo, unitOfWork, auditoria, optWrapper);
            return (handler, context);
        }

        // ========================================================================
        // TAREA 1 (B02): RECHAZOS SIN EFECTOS FINANCIEROS NI DE ESTADO
        // ========================================================================

        [Fact]
        public async Task Rechazo_SaldoInsuficiente_500ConPuja1000_LanzaExcepcionYDejaEntidadesIntactas()
        {
            // Arrange
            using var context = new SubastaDbContext(_options);
            var ahora = DateTime.UtcNow;

            var bPostor = await context.Billeteras.FindAsync(4); // Usuario 4
            bPostor!.SaldoTotal = 500m;
            bPostor.SaldoDisponible = 500m;
            bPostor.SaldoRetenido = 0m;

            var fechaFinOriginal = ahora.AddMinutes(30);
            var subasta = new Subasta
            {
                Id = 101,
                Titulo = "Test Saldo Insuficiente",
                Descripcion = "Item",
                PrecioBase = 1000m,
                IncrementoMinimo = 100m,
                FechaInicio = ahora.AddMinutes(-10),
                FechaFin = fechaFinOriginal,
                Estado = "ACTIVA",
                CategoriaId = 1,
                VendedorId = 1,
                Version = 1
            };
            context.Subastas.Add(subasta);
            await context.SaveChangesAsync();

            var (handler, _) = CrearHandler(context);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<DominioException>(() => handler.HandleAsync(new RegistrarPujaCommand
            {
                SubastaId = 101,
                UsuarioId = 4,
                Monto = 1000m,
                VersionCliente = 1
            }));
            Assert.Contains("Saldo disponible insuficiente", ex.Message);

            // Verificar en nuevo contexto que TODO quedó intacto
            using var assertContext = new SubastaDbContext(_options);
            var bAssert = await assertContext.Billeteras.FindAsync(4);
            Assert.Equal(500m, bAssert!.SaldoTotal);
            Assert.Equal(500m, bAssert.SaldoDisponible);
            Assert.Equal(0m, bAssert.SaldoRetenido);

            var sAssert = await assertContext.Subastas.Include(s => s.Pujas).FirstAsync(s => s.Id == 101);
            Assert.Equal(fechaFinOriginal, sAssert.FechaFin);
            Assert.Equal(1u, sAssert.Version);
            Assert.Empty(sAssert.Pujas);

            var ledgers = await assertContext.TransactionLedgers.Where(l => l.SubastaId == 101).ToListAsync();
            Assert.Empty(ledgers);

            // Se registró PUJA_RECHAZADA en scope aislado
            var logRechazo = await assertContext.LogsAuditoria.FirstOrDefaultAsync(l => l.Accion == "PUJA_RECHAZADA" && l.UsuarioId == 4);
            Assert.NotNull(logRechazo);
            Assert.Contains("SALDO_INSUFICIENTE", logRechazo.Detalle);
        }

        [Fact]
        public async Task Rechazo_IncrementoInsuficiente_LanzaExcepcionYDejaEntidadesIntactas()
        {
            // Arrange
            using var context = new SubastaDbContext(_options);
            var ahora = DateTime.UtcNow;

            var bPostor = await context.Billeteras.FindAsync(3); // Usuario 3 con $200.000
            var fechaFinOriginal = ahora.AddMinutes(30);
            var subasta = new Subasta
            {
                Id = 102,
                Titulo = "Test Incremento Insuficiente",
                Descripcion = "Item",
                PrecioBase = 10000m,
                IncrementoMinimo = 2000m,
                FechaInicio = ahora.AddMinutes(-10),
                FechaFin = fechaFinOriginal,
                Estado = "ACTIVA",
                CategoriaId = 1,
                VendedorId = 1,
                Version = 1
            };
            context.Subastas.Add(subasta);
            await context.SaveChangesAsync();

            var (handler, _) = CrearHandler(context);

            // Act: Oferta 10500 cuando el precio base era 10000 pero el incremento es 2000 (o puja inicial menor)
            // Caso: líder previo con 10000, próxima puja mínima 12000. Oferta 11000.
            var pujaPrevia = new Puja { SubastaId = 102, UsuarioId = 2, Monto = 10000m, FechaCreacion = ahora.AddMinutes(-5) };
            context.Pujas.Add(pujaPrevia);
            await context.SaveChangesAsync();

            var ex = await Assert.ThrowsAsync<DominioException>(() => handler.HandleAsync(new RegistrarPujaCommand
            {
                SubastaId = 102,
                UsuarioId = 3,
                Monto = 11000m,
                VersionCliente = 1
            }));
            Assert.Contains("incremento mínimo", ex.Message);

            // Assert: Sin efectos colaterales
            using var assertContext = new SubastaDbContext(_options);
            var bAssert = await assertContext.Billeteras.FindAsync(3);
            Assert.Equal(200000m, bAssert!.SaldoDisponible);
            Assert.Equal(0m, bAssert.SaldoRetenido);

            var sAssert = await assertContext.Subastas.Include(s => s.Pujas).FirstAsync(s => s.Id == 102);
            Assert.Equal(fechaFinOriginal, sAssert.FechaFin);
            Assert.Equal(1u, sAssert.Version);
            Assert.Single(sAssert.Pujas); // Solo la previa
        }

        [Fact]
        public async Task Rechazo_SubastaVencida_LanzaExcepcionYDejaEntidadesIntactas()
        {
            // Arrange
            using var context = new SubastaDbContext(_options);
            var ahora = DateTime.UtcNow;

            var subasta = new Subasta
            {
                Id = 103,
                Titulo = "Test Subasta Vencida",
                Descripcion = "Item",
                PrecioBase = 10000m,
                IncrementoMinimo = 1000m,
                FechaInicio = ahora.AddDays(-2),
                FechaFin = ahora.AddMinutes(-5), // Vencida
                Estado = "ACTIVA",
                CategoriaId = 1,
                VendedorId = 1,
                Version = 1
            };
            context.Subastas.Add(subasta);
            await context.SaveChangesAsync();

            var (handler, _) = CrearHandler(context);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<DominioException>(() => handler.HandleAsync(new RegistrarPujaCommand
            {
                SubastaId = 103,
                UsuarioId = 3,
                Monto = 15000m,
                VersionCliente = 1
            }));
            Assert.Contains("ha finalizado", ex.Message);

            using var assertContext = new SubastaDbContext(_options);
            var sAssert = await assertContext.Subastas.Include(s => s.Pujas).FirstAsync(s => s.Id == 103);
            Assert.Empty(sAssert.Pujas);
            Assert.Equal(1u, sAssert.Version);
        }

        [Fact]
        public async Task Rechazo_SubastaProgramada_LanzaExcepcionYDejaEntidadesIntactas()
        {
            // Arrange
            using var context = new SubastaDbContext(_options);
            var ahora = DateTime.UtcNow;

            var subasta = new Subasta
            {
                Id = 104,
                Titulo = "Test Subasta Programada",
                Descripcion = "Item",
                PrecioBase = 10000m,
                IncrementoMinimo = 1000m,
                FechaInicio = ahora.AddHours(2),
                FechaFin = ahora.AddHours(24),
                Estado = "PROGRAMADA",
                CategoriaId = 1,
                VendedorId = 1,
                Version = 1
            };
            context.Subastas.Add(subasta);
            await context.SaveChangesAsync();

            var (handler, _) = CrearHandler(context);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<DominioException>(() => handler.HandleAsync(new RegistrarPujaCommand
            {
                SubastaId = 104,
                UsuarioId = 3,
                Monto = 15000m,
                VersionCliente = 1
            }));
            Assert.Contains("no está activa", ex.Message);

            using var assertContext = new SubastaDbContext(_options);
            var sAssert = await assertContext.Subastas.Include(s => s.Pujas).FirstAsync(s => s.Id == 104);
            Assert.Empty(sAssert.Pujas);
            Assert.Equal(1u, sAssert.Version);
        }

        [Fact]
        public async Task Rechazo_VendedorPujando_LanzaExcepcionYDejaEntidadesIntactas()
        {
            // Arrange: Usuario 1 es el vendedor
            using var context = new SubastaDbContext(_options);
            var ahora = DateTime.UtcNow;

            var subasta = new Subasta
            {
                Id = 105,
                Titulo = "Test Vendedor Pujando",
                Descripcion = "Item",
                PrecioBase = 10000m,
                IncrementoMinimo = 1000m,
                FechaInicio = ahora.AddMinutes(-10),
                FechaFin = ahora.AddHours(2),
                Estado = "ACTIVA",
                CategoriaId = 1,
                VendedorId = 1,
                Version = 1
            };
            context.Subastas.Add(subasta);
            await context.SaveChangesAsync();

            var (handler, _) = CrearHandler(context);

            // Act & Assert: Usuario 1 intenta ofertar en su propia subasta
            var ex = await Assert.ThrowsAsync<DominioException>(() => handler.HandleAsync(new RegistrarPujaCommand
            {
                SubastaId = 105,
                UsuarioId = 1,
                Monto = 15000m,
                VersionCliente = 1
            }));
            Assert.Contains("vendedor no tiene permitido", ex.Message);

            using var assertContext = new SubastaDbContext(_options);
            var sAssert = await assertContext.Subastas.Include(s => s.Pujas).FirstAsync(s => s.Id == 105);
            Assert.Empty(sAssert.Pujas);
            Assert.Equal(1u, sAssert.Version);
        }

        // ========================================================================
        // TAREA 2 (B05): ANTI-SNIPING PARAMETRIZADO Y SUS CASOS EXACTOS
        // ========================================================================

        [Fact]
        public async Task AntiSniping_Con30SegundosRestantes_ExtiendeFechaFinExactamenteEn120Segundos()
        {
            // Arrange: Subasta vence en 30s exactos respecto a ahora
            using var context = new SubastaDbContext(_options);
            var ahora = DateTime.UtcNow;
            var fechaFinPrevia = ahora.AddSeconds(30);

            var subasta = new Subasta
            {
                Id = 106,
                Titulo = "Test AntiSniping 30s",
                Descripcion = "Item",
                PrecioBase = 10000m,
                IncrementoMinimo = 1000m,
                FechaInicio = ahora.AddHours(-1),
                FechaFin = fechaFinPrevia,
                Estado = "ACTIVA",
                CategoriaId = 1,
                VendedorId = 1,
                Version = 1
            };
            context.Subastas.Add(subasta);
            await context.SaveChangesAsync();

            var (handler, _) = CrearHandler(context, new AntiSnipingOptions { UmbralSegundos = 60, ExtensionSegundos = 120 });

            // Act
            var res = await handler.HandleAsync(new RegistrarPujaCommand
            {
                SubastaId = 106,
                UsuarioId = 3,
                Monto = 15000m,
                VersionCliente = 1
            });

            // Assert
            Assert.True(res.AntiSnipingActivado);
            Assert.Equal(fechaFinPrevia.AddSeconds(120), res.FechaFin);

            using var assertContext = new SubastaDbContext(_options);
            var sAssert = await assertContext.Subastas.FindAsync(106);
            Assert.Equal(fechaFinPrevia.AddSeconds(120), sAssert!.FechaFin);

            // Verificar eventos de auditoría: PUJA_REGISTRADA y EXTENSION_ANTI_SNIPING
            var logs = await assertContext.LogsAuditoria
                .Where(l => l.Detalle.Contains("106"))
                .ToListAsync();
            Assert.Contains(logs, l => l.Accion == "PUJA_REGISTRADA");
            Assert.Contains(logs, l => l.Accion == "EXTENSION_ANTI_SNIPING");
        }

        [Fact]
        public async Task AntiSniping_Con90SegundosRestantes_FueraDeVentana_NoExtiendeFechaFin()
        {
            // Arrange: Subasta vence en 90s (umbral es 60s)
            using var context = new SubastaDbContext(_options);
            var ahora = DateTime.UtcNow;
            var fechaFinPrevia = ahora.AddSeconds(90);

            var subasta = new Subasta
            {
                Id = 107,
                Titulo = "Test AntiSniping 90s",
                Descripcion = "Item",
                PrecioBase = 10000m,
                IncrementoMinimo = 1000m,
                FechaInicio = ahora.AddHours(-1),
                FechaFin = fechaFinPrevia,
                Estado = "ACTIVA",
                CategoriaId = 1,
                VendedorId = 1,
                Version = 1
            };
            context.Subastas.Add(subasta);
            await context.SaveChangesAsync();

            var (handler, _) = CrearHandler(context, new AntiSnipingOptions { UmbralSegundos = 60, ExtensionSegundos = 120 });

            // Act
            var res = await handler.HandleAsync(new RegistrarPujaCommand
            {
                SubastaId = 107,
                UsuarioId = 3,
                Monto = 15000m,
                VersionCliente = 1
            });

            // Assert
            Assert.False(res.AntiSnipingActivado);
            Assert.Equal(fechaFinPrevia, res.FechaFin);

            using var assertContext = new SubastaDbContext(_options);
            var sAssert = await assertContext.Subastas.FindAsync(107);
            Assert.Equal(fechaFinPrevia, sAssert!.FechaFin);
        }

        [Fact]
        public async Task AntiSniping_PujaRechazadaCon30SegundosRestantes_NoExtiendeFechaFin()
        {
            // Arrange: Subasta vence en 30s, pero puja de postor sin saldo suficiente
            using var context = new SubastaDbContext(_options);
            var ahora = DateTime.UtcNow;
            var fechaFinPrevia = ahora.AddSeconds(30);

            var subasta = new Subasta
            {
                Id = 108,
                Titulo = "Test AntiSniping Rechazada",
                Descripcion = "Item",
                PrecioBase = 10000m,
                IncrementoMinimo = 1000m,
                FechaInicio = ahora.AddHours(-1),
                FechaFin = fechaFinPrevia,
                Estado = "ACTIVA",
                CategoriaId = 1,
                VendedorId = 1,
                Version = 1
            };
            context.Subastas.Add(subasta);
            await context.SaveChangesAsync();

            var (handler, _) = CrearHandler(context, new AntiSnipingOptions { UmbralSegundos = 60, ExtensionSegundos = 120 });

            // Act: Usuario 4 tiene $0 de saldo
            await Assert.ThrowsAsync<DominioException>(() => handler.HandleAsync(new RegistrarPujaCommand
            {
                SubastaId = 108,
                UsuarioId = 4,
                Monto = 15000m,
                VersionCliente = 1
            }));

            // Assert: FechaFin NO cambió
            using var assertContext = new SubastaDbContext(_options);
            var sAssert = await assertContext.Subastas.FindAsync(108);
            Assert.Equal(fechaFinPrevia, sAssert!.FechaFin);
        }

        // ========================================================================
        // TAREA 3 & 4 (A07/A08): CONCURRENCIA OPTIMISTA PREVENTIVA Y MENSAJE 409
        // ========================================================================

        [Fact]
        public async Task Concurrencia_VersionClienteDesactualizada_LanzaConcurrenciaExceptionAntesDeModificar()
        {
            // Arrange: Subasta en Version = 2. Cliente envía VersionCliente = 1
            using var context = new SubastaDbContext(_options);
            var ahora = DateTime.UtcNow;

            var subasta = new Subasta
            {
                Id = 109,
                Titulo = "Test OCC Preventivo",
                Descripcion = "Item",
                PrecioBase = 10000m,
                IncrementoMinimo = 1000m,
                FechaInicio = ahora.AddHours(-1),
                FechaFin = ahora.AddHours(2),
                Estado = "ACTIVA",
                CategoriaId = 1,
                VendedorId = 1,
                Version = 2 // Versión en base
            };
            context.Subastas.Add(subasta);
            await context.SaveChangesAsync();

            var (handler, _) = CrearHandler(context);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<ConcurrenciaException>(() => handler.HandleAsync(new RegistrarPujaCommand
            {
                SubastaId = 109,
                UsuarioId = 3,
                Monto = 15000m,
                VersionCliente = 1 // Desactualizada
            }));

            Assert.Equal("El recurso fue modificado por otra operación; reintenta.", ex.Message);

            // Verificar que no se alteró nada
            using var assertContext = new SubastaDbContext(_options);
            var sAssert = await assertContext.Subastas.Include(s => s.Pujas).FirstAsync(s => s.Id == 109);
            Assert.Equal(2u, sAssert.Version);
            Assert.Empty(sAssert.Pujas);
        }

        // ========================================================================
        // TAREA 6 (B12): ROLLBACK ANTE FALLO EN AUDITORÍA / INFRAESTRUCTURA
        // ========================================================================

        private class FailingAuditoriaService : IAuditoriaService
        {
            public Task RegistrarAsync(string accion, string detalle, int? usuarioId = null)
            {
                if (accion == "PUJA_REGISTRADA")
                {
                    throw new InvalidOperationException("Fallo simulado e intencional de persistencia en auditoría transaccional.");
                }
                return Task.CompletedTask;
            }

            public Task RegistrarFalloAsync(string accion, string detalle, int? usuarioId = null)
            {
                return Task.CompletedTask;
            }

            public Task<IEnumerable<LogAuditoria>> ObtenerLogsAsync(int limite = 100)
            {
                return Task.FromResult(Enumerable.Empty<LogAuditoria>());
            }
        }

        [Fact]
        public async Task B12_FalloEnAuditoria_ReviertePujaRetencionesLiberacionesYVersion()
        {
            // Arrange: Subasta con líder previo (Usuario 2, $15.000). Usuario 3 oferta $20.000.
            using var context = new SubastaDbContext(_options);
            var ahora = DateTime.UtcNow;

            var subasta = new Subasta
            {
                Id = 110,
                Titulo = "Test Rollback Atómico B12",
                Descripcion = "Item",
                PrecioBase = 10000m,
                IncrementoMinimo = 1000m,
                FechaInicio = ahora.AddHours(-1),
                FechaFin = ahora.AddHours(2),
                Estado = "ACTIVA",
                CategoriaId = 1,
                VendedorId = 1,
                Version = 1
            };
            context.Subastas.Add(subasta);

            // Líder anterior: Usuario 2
            var pujaLiderAnterior = new Puja
            {
                Id = 200,
                SubastaId = 110,
                UsuarioId = 2,
                Monto = 15000m,
                FechaCreacion = ahora.AddMinutes(-30)
            };
            context.Pujas.Add(pujaLiderAnterior);

            // Saldo previo Usuario 2
            var b2 = await context.Billeteras.FindAsync(2);
            b2!.SaldoTotal = 150000m;
            b2.SaldoRetenido = 45000m; // Incluye los 15.000
            b2.SaldoDisponible = 105000m;

            // Saldo previo Usuario 3
            var b3 = await context.Billeteras.FindAsync(3);
            b3!.SaldoTotal = 200000m;
            b3.SaldoRetenido = 0m;
            b3.SaldoDisponible = 200000m;

            await context.SaveChangesAsync();

            var failingAuditoria = new FailingAuditoriaService();
            var (handler, _) = CrearHandler(context, customAuditoria: failingAuditoria);

            // Act: Usuario 3 intenta pujar $20.000, pero la auditoría transaccional falla
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => handler.HandleAsync(new RegistrarPujaCommand
            {
                SubastaId = 110,
                UsuarioId = 3,
                Monto = 20000m,
                VersionCliente = 1
            }));
            Assert.Contains("Fallo simulado", ex.Message);

            // Assert en nuevo contexto: NINGÚN cambio de negocio sobrevivió
            using var assertContext = new SubastaDbContext(_options);

            // 1. Subasta y versión intactas
            var sAssert = await assertContext.Subastas.Include(s => s.Pujas).FirstAsync(s => s.Id == 110);
            Assert.Equal(1u, sAssert.Version);
            Assert.Single(sAssert.Pujas); // Solo la puja previa de Usuario 2
            Assert.DoesNotContain(sAssert.Pujas, p => p.UsuarioId == 3);

            // 2. Billetera de postor 3 sin retención
            var b3Assert = await assertContext.Billeteras.FindAsync(3);
            Assert.Equal(200000m, b3Assert!.SaldoTotal);
            Assert.Equal(0m, b3Assert.SaldoRetenido);
            Assert.Equal(200000m, b3Assert.SaldoDisponible);

            // 3. Billetera de líder anterior (postor 2) sin liberación indebida
            var b2Assert = await assertContext.Billeteras.FindAsync(2);
            Assert.Equal(150000m, b2Assert!.SaldoTotal);
            Assert.Equal(45000m, b2Assert.SaldoRetenido);
            Assert.Equal(105000m, b2Assert.SaldoDisponible);

            // 4. Ledger sin movimientos nuevos de retención o liberación para esta puja
            var ledgerNuevos = await assertContext.TransactionLedgers
                .Where(l => l.SubastaId == 110 && l.Monto == 20000m)
                .ToListAsync();
            Assert.Empty(ledgerNuevos);

            // 5. No existe registro de auditoría de éxito
            var logsExito = await assertContext.LogsAuditoria
                .Where(l => l.Accion == "PUJA_REGISTRADA" && l.UsuarioId == 3)
                .ToListAsync();
            Assert.Empty(logsExito);
        }

        // ========================================================================
        // TAREA 7: RESPUESTA CON VERSIÓN NUEVA, FECHAFIN, LÍDER Y ANTI-SNIPING
        // ========================================================================

        [Fact]
        public async Task Tarea7_RespuestaEndpoint_ContieneVersionNuevaFechaFinLiderYAntiSniping()
        {
            // Arrange
            using var context = new SubastaDbContext(_options);
            var ahora = DateTime.UtcNow;

            var subasta = new Subasta
            {
                Id = 111,
                Titulo = "Test Respuesta DTO",
                Descripcion = "Item",
                PrecioBase = 10000m,
                IncrementoMinimo = 1000m,
                FechaInicio = ahora.AddHours(-1),
                FechaFin = ahora.AddHours(2),
                Estado = "ACTIVA",
                CategoriaId = 1,
                VendedorId = 1,
                Version = 1
            };
            context.Subastas.Add(subasta);
            await context.SaveChangesAsync();

            var (handler, _) = CrearHandler(context);

            // Act
            var res = await handler.HandleAsync(new RegistrarPujaCommand
            {
                SubastaId = 111,
                UsuarioId = 3,
                Monto = 15000m,
                VersionCliente = 1
            });

            // Assert
            Assert.NotNull(res);
            Assert.Equal(2u, res.Version); // Versión nueva
            Assert.Equal(subasta.FechaFin, res.FechaFin);
            Assert.Equal(3, res.LiderId);
            Assert.NotNull(res.Lider);
            Assert.False(res.AntiSnipingActivado);
            Assert.NotNull(res.Puja);
            Assert.Equal(15000m, res.Puja.Monto);
        }
    }
}
