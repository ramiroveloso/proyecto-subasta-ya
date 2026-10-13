using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PROYECTO_SUBASTA.Application.Commands.Billeteras;
using PROYECTO_SUBASTA.Application.Commands.Subastas;
using PROYECTO_SUBASTA.Application.DTOs;
using PROYECTO_SUBASTA.Application.Exceptions;
using PROYECTO_SUBASTA.Application.Queries.Billeteras;
using PROYECTO_SUBASTA.Application.Queries.Subastas;
using PROYECTO_SUBASTA.Application.Repositories;
using PROYECTO_SUBASTA.Domain.Entities;
using PROYECTO_SUBASTA.Domain.Exceptions;
using PROYECTO_SUBASTA.Infrastructure.Data;
using PROYECTO_SUBASTA.Infrastructure.Repositories;
using PROYECTO_SUBASTA.Infrastructure.Services;
using Xunit;

namespace PROYECTO_SUBASTA.Domain.Tests
{
    /// <summary>
    /// Suite de pruebas integrales para FASE 4 — CIERRE Y LIQUIDACIÓN:
    /// Verifica criterios B06, B07, C04 y A08.
    /// NOTA DE PROVEEDOR: Ejecuta sobre SQLite in-memory para aislamiento unitario/integración.
    /// </summary>
    public class CierreSubastaFase4Tests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly DbContextOptions<SubastaDbContext> _options;
        private readonly IServiceProvider _serviceProvider;

        public CierreSubastaFase4Tests()
        {
            _connection = new SqliteConnection($"DataSource=test_fase4_{Guid.NewGuid():N};Mode=Memory;Cache=Shared");
            _connection.Open();

            _options = new DbContextOptionsBuilder<SubastaDbContext>()
                .UseSqlite(_connection)
                .Options;

            var services = new ServiceCollection();
            services.AddDbContext<SubastaDbContext>(opt => opt.UseSqlite(_connection));
            services.AddLogging();
            _serviceProvider = services.BuildServiceProvider();

            using var initContext = new SubastaDbContext(_options);
            initContext.Database.EnsureCreated();

            // Configurar billeteras iniciales
            var b1 = initContext.Billeteras.First(b => b.UsuarioId == 1);
            b1.SaldoTotal = 10000m; b1.SaldoDisponible = 10000m; b1.SaldoRetenido = 0m;

            var b2 = initContext.Billeteras.First(b => b.UsuarioId == 2);
            b2.SaldoTotal = 50000m; b2.SaldoDisponible = 50000m; b2.SaldoRetenido = 0m;

            var b3 = initContext.Billeteras.First(b => b.UsuarioId == 3);
            b3.SaldoTotal = 100000m; b3.SaldoDisponible = 100000m; b3.SaldoRetenido = 0m;

            initContext.SaveChanges();
        }

        public void Dispose()
        {
            _connection.Dispose();
        }

        private (CerrarSubastaCommandHandler cerrarHandler,
                 ActivarSubastasProgramadasCommandHandler activarHandler,
                 EjecutarCierresCommandHandler ejecutarCierresHandler,
                 SubastaDbContext context) CrearContextoYHandlers()
        {
            var context = new SubastaDbContext(_options);
            var scopeFactory = _serviceProvider.GetRequiredService<IServiceScopeFactory>();
            var subastaRepo = new SubastaRepository(context);
            var billeteraRepo = new BilleteraRepository(context);
            var ledgerRepo = new Repository<TransactionLedger>(context);
            var unitOfWork = new UnitOfWork(context);
            var auditoria = new AuditoriaService(context, scopeFactory, NullLogger<AuditoriaService>.Instance);

            var cerrarHandler = new CerrarSubastaCommandHandler(
                subastaRepo,
                billeteraRepo,
                ledgerRepo,
                unitOfWork,
                auditoria,
                NullLogger<CerrarSubastaCommandHandler>.Instance);

            var activarHandler = new ActivarSubastasProgramadasCommandHandler(
                subastaRepo,
                unitOfWork,
                auditoria,
                NullLogger<ActivarSubastasProgramadasCommandHandler>.Instance);

            var queryHandler = new ObtenerSubastasPorCerrarQueryHandler(subastaRepo);

            var ejecutarCierresHandler = new EjecutarCierresCommandHandler(
                activarHandler,
                queryHandler,
                cerrarHandler,
                NullLogger<EjecutarCierresCommandHandler>.Instance);

            return (cerrarHandler, activarHandler, ejecutarCierresHandler, context);
        }

        // ========================================================================
        // 1. (a) GANADOR CON RETENCIÓN SUFICIENTE: PAGO, COBRO, PERDEDORES LIBERADOS
        // ========================================================================
        [Fact]
        public async Task Test_A_GanadorConRetencionSuficiente_LiquidaPagoYCobro_LiberaPerdedores_SaldosConcilian()
        {
            var (cerrarHandler, _, _, context) = CrearContextoYHandlers();
            var ahora = DateTime.UtcNow;

            // Arrange: Vendedor = Usuario 1. Postor 2 oferta 15.000. Postor 3 oferta 20.000 (ganador).
            var subasta = new Subasta
            {
                Id = 100,
                Titulo = "Notebook Pro",
                Descripcion = "Computadora",
                PrecioBase = 10000m,
                IncrementoMinimo = 1000m,
                FechaInicio = ahora.AddDays(-2),
                FechaFin = ahora.AddMinutes(-5), // Vencida
                Estado = "ACTIVA",
                CategoriaId = 1,
                VendedorId = 1,
                Version = 3
            };
            context.Subastas.Add(subasta);

            // Pujas
            context.Pujas.Add(new Puja { Id = 201, SubastaId = 100, UsuarioId = 2, Monto = 15000m, FechaCreacion = ahora.AddHours(-2) });
            context.Pujas.Add(new Puja { Id = 202, SubastaId = 100, UsuarioId = 3, Monto = 20000m, FechaCreacion = ahora.AddHours(-1) });

            // Simular saldos previos y retenciones en billeteras
            var bVendedor = await context.Billeteras.FirstAsync(b => b.UsuarioId == 1);
            decimal saldoVendedorInicial = bVendedor.SaldoTotal;

            var bPerdedor = await context.Billeteras.FirstAsync(b => b.UsuarioId == 2);
            bPerdedor.SaldoRetenido = 15000m;
            bPerdedor.SaldoDisponible = 35000m;

            var bGanador = await context.Billeteras.FirstAsync(b => b.UsuarioId == 3);
            bGanador.SaldoRetenido = 20000m;
            bGanador.SaldoDisponible = 80000m;

            // Registrar movimientos previos de RETENCION en el ledger
            context.TransactionLedgers.Add(new TransactionLedger { BilleteraId = bPerdedor.Id, Tipo = TipoTransaccion.RETENCION, Monto = 15000m, Fecha = ahora.AddHours(-2), SubastaId = 100 });
            context.TransactionLedgers.Add(new TransactionLedger { BilleteraId = bGanador.Id, Tipo = TipoTransaccion.RETENCION, Monto = 20000m, Fecha = ahora.AddHours(-1), SubastaId = 100 });
            await context.SaveChangesAsync();

            // Act
            var resultado = await cerrarHandler.HandleAsync(new CerrarSubastaCommand { SubastaId = 100 });

            // Assert
            Assert.True(resultado.Exitoso);
            Assert.Equal("FINALIZADA", resultado.Estado);
            Assert.Equal(3, resultado.GanadorId);
            Assert.Equal(20000m, resultado.PrecioFinal);

            // Subasta en base
            var sDb = await context.Subastas.FirstAsync(s => s.Id == 100);
            Assert.Equal("FINALIZADA", sDb.Estado);
            Assert.Equal(3, sDb.GanadorId);

            // Billetera Comprador / Ganador (Usuario 3): DebitarRetencion(20000)
            var bGanadorFinal = await context.Billeteras.FirstAsync(b => b.UsuarioId == 3);
            Assert.Equal(0m, bGanadorFinal.SaldoRetenido);
            Assert.Equal(80000m, bGanadorFinal.SaldoTotal);
            Assert.Equal(80000m, bGanadorFinal.SaldoDisponible);

            // Billetera Vendedor (Usuario 1): AcreditarCobro(20000)
            var bVendedorFinal = await context.Billeteras.FirstAsync(b => b.UsuarioId == 1);
            Assert.Equal(saldoVendedorInicial + 20000m, bVendedorFinal.SaldoTotal);
            Assert.Equal(saldoVendedorInicial + 20000m, bVendedorFinal.SaldoDisponible);

            // Billetera Perdedor (Usuario 2): LiberarSaldo(15000)
            var bPerdedorFinal = await context.Billeteras.FirstAsync(b => b.UsuarioId == 2);
            Assert.Equal(0m, bPerdedorFinal.SaldoRetenido);
            Assert.Equal(50000m, bPerdedorFinal.SaldoDisponible);
            Assert.Equal(50000m, bPerdedorFinal.SaldoTotal);

            // Asientos en TransactionLedger
            var ledgers = await context.TransactionLedgers.Where(t => t.SubastaId == 100).ToListAsync();
            Assert.Single(ledgers, t => t.Tipo == TipoTransaccion.PAGO);
            Assert.Single(ledgers, t => t.Tipo == TipoTransaccion.COBRO);
            Assert.Single(ledgers, t => t.Tipo == TipoTransaccion.LIBERACION);

            var pago = ledgers.First(t => t.Tipo == TipoTransaccion.PAGO);
            var cobro = ledgers.First(t => t.Tipo == TipoTransaccion.COBRO);
            var lib = ledgers.First(t => t.Tipo == TipoTransaccion.LIBERACION);

            Assert.Equal(20000m, pago.Monto);
            Assert.Equal(bGanador.Id, pago.BilleteraId);
            Assert.Equal(20000m, cobro.Monto);
            Assert.Equal(bVendedor.Id, cobro.BilleteraId);
            Assert.Equal(15000m, lib.Monto);
            Assert.Equal(bPerdedor.Id, lib.BilleteraId);

            // Conciliación financiera: la masa monetaria total del sistema se mantiene íntegra
            decimal masaMonetariaFinal = bVendedorFinal.SaldoTotal + bPerdedorFinal.SaldoTotal + bGanadorFinal.SaldoTotal;
            decimal masaMonetariaEsperada = saldoVendedorInicial + 50000m + 100000m;
            Assert.Equal(masaMonetariaEsperada, masaMonetariaFinal);
        }

        // ========================================================================
        // 2. (b) COMPRADOR CON RETENIDO 500 Y PRECIO 1000: NO SE LIQUIDA, NO SE CREA DINERO
        // ========================================================================
        [Fact]
        public async Task Test_B_CompradorConRetenidoInsuficiente_NoLiquida_Rollback_SubastaSigueActiva_AuditoriaFallo()
        {
            var (cerrarHandler, _, _, context) = CrearContextoYHandlers();
            var ahora = DateTime.UtcNow;

            var subasta = new Subasta
            {
                Id = 101,
                Titulo = "Reloj de Lujo",
                Descripcion = "Joyas",
                PrecioBase = 500m,
                IncrementoMinimo = 100m,
                FechaInicio = ahora.AddDays(-2),
                FechaFin = ahora.AddMinutes(-5),
                Estado = "ACTIVA",
                CategoriaId = 1,
                VendedorId = 1,
                Version = 1
            };
            context.Subastas.Add(subasta);

            // Puja de 1000m de Usuario 2
            context.Pujas.Add(new Puja { Id = 203, SubastaId = 101, UsuarioId = 2, Monto = 1000m, FechaCreacion = ahora.AddHours(-1) });

            // Billetera Comprador tiene solo 500 retenidos (inconsistencia externa)
            var bComprador = await context.Billeteras.FirstAsync(b => b.UsuarioId == 2);
            bComprador.SaldoRetenido = 500m;
            bComprador.SaldoDisponible = 0m;
            bComprador.SaldoTotal = 500m;

            var bVendedor = await context.Billeteras.FirstAsync(b => b.UsuarioId == 1);
            decimal saldoVendedorInicial = bVendedor.SaldoTotal;

            await context.SaveChangesAsync();

            // Act & Assert: Debe rechazar la liquidación lanzando excepción
            await Assert.ThrowsAsync<DominioException>(() =>
                cerrarHandler.HandleAsync(new CerrarSubastaCommand { SubastaId = 101 })
            );

            // Verificar que la subasta NO quedó FINALIZADA
            var sDb = await context.Subastas.FirstAsync(s => s.Id == 101);
            Assert.Equal("ACTIVA", sDb.Estado);
            Assert.Null(sDb.GanadorId);
            Assert.Null(sDb.PrecioFinal);

            // Verificar que no se creó dinero ficticio ni se acreditó al vendedor
            var bVendedorFinal = await context.Billeteras.FirstAsync(b => b.UsuarioId == 1);
            Assert.Equal(saldoVendedorInicial, bVendedorFinal.SaldoTotal);

            // Comprador mantiene sus fondos intactos
            var bCompradorFinal = await context.Billeteras.FirstAsync(b => b.UsuarioId == 2);
            Assert.Equal(500m, bCompradorFinal.SaldoTotal);
            Assert.Equal(500m, bCompradorFinal.SaldoRetenido);

            // No existen asientos de PAGO ni COBRO
            var ledgers = await context.TransactionLedgers.Where(t => t.SubastaId == 101).ToListAsync();
            Assert.DoesNotContain(ledgers, t => t.Tipo == TipoTransaccion.PAGO);
            Assert.DoesNotContain(ledgers, t => t.Tipo == TipoTransaccion.COBRO);

            // Existe auditoría de fallo persistida
            var auditorias = await context.LogsAuditoria.Where(l => l.Accion == "FALLO_CIERRE_SUBASTA").ToListAsync();
            Assert.NotEmpty(auditorias);
            Assert.Contains("Saldo retenido insuficiente", auditorias.Last().Detalle);
        }

        // ========================================================================
        // 3. (c) DESIERTA: SUBASTA SIN PUJAS Y LIBERACIÓN DE RESIDUALES
        // ========================================================================
        [Fact]
        public async Task Test_C_SubastaSinPujas_PasaADesierta_GanadorNull()
        {
            var (cerrarHandler, _, _, context) = CrearContextoYHandlers();
            var ahora = DateTime.UtcNow;

            var subasta = new Subasta
            {
                Id = 102,
                Titulo = "Cuadro sin ofertas",
                Descripcion = "Arte",
                PrecioBase = 30000m,
                IncrementoMinimo = 1000m,
                FechaInicio = ahora.AddDays(-2),
                FechaFin = ahora.AddMinutes(-5),
                Estado = "ACTIVA",
                CategoriaId = 1,
                VendedorId = 1,
                Version = 1
            };
            context.Subastas.Add(subasta);
            await context.SaveChangesAsync();

            // Act
            var res = await cerrarHandler.HandleAsync(new CerrarSubastaCommand { SubastaId = 102 });

            // Assert
            Assert.True(res.Exitoso);
            Assert.Equal("DESIERTA", res.Estado);
            Assert.Null(res.GanadorId);
            Assert.Null(res.PrecioFinal);

            var sDb = await context.Subastas.FirstAsync(s => s.Id == 102);
            Assert.Equal("DESIERTA", sDb.Estado);
            Assert.Null(sDb.GanadorId);

            var auditoria = await context.LogsAuditoria.FirstOrDefaultAsync(l => l.Accion == "CAMBIO_ESTADO_SUBASTA" && l.Detalle.Contains("DESIERTA"));
            Assert.NotNull(auditoria);
        }

        // ========================================================================
        // 4. (d) DOS EJECUCIONES SEGUIDAS DEL CIERRE: UN SOLO PAGO/COBRO (IDEMPOTENCIA)
        // ========================================================================
        [Fact]
        public async Task Test_D_DosEjecucionesSeguidas_UnSoloPagoYCobro()
        {
            var (cerrarHandler, _, _, context) = CrearContextoYHandlers();
            var ahora = DateTime.UtcNow;

            var subasta = new Subasta
            {
                Id = 103,
                Titulo = "Consola de Videojuegos",
                Descripcion = "Gaming",
                PrecioBase = 10000m,
                IncrementoMinimo = 500m,
                FechaInicio = ahora.AddDays(-1),
                FechaFin = ahora.AddMinutes(-2),
                Estado = "ACTIVA",
                CategoriaId = 1,
                VendedorId = 1,
                Version = 1
            };
            context.Subastas.Add(subasta);

            context.Pujas.Add(new Puja { Id = 204, SubastaId = 103, UsuarioId = 2, Monto = 12000m, FechaCreacion = ahora.AddHours(-1) });

            var bComprador = await context.Billeteras.FirstAsync(b => b.UsuarioId == 2);
            bComprador.SaldoRetenido = 12000m;
            bComprador.SaldoDisponible = 38000m;

            var bVendedor = await context.Billeteras.FirstAsync(b => b.UsuarioId == 1);
            decimal saldoVendedorInicial = bVendedor.SaldoTotal;

            await context.SaveChangesAsync();

            // Primera ejecución
            var res1 = await cerrarHandler.HandleAsync(new CerrarSubastaCommand { SubastaId = 103 });
            Assert.True(res1.Exitoso);
            Assert.Equal("FINALIZADA", res1.Estado);

            // Segunda ejecución (inmediata)
            var res2 = await cerrarHandler.HandleAsync(new CerrarSubastaCommand { SubastaId = 103 });
            Assert.True(res2.Exitoso);
            Assert.Contains("ya se encuentra en estado 'FINALIZADA'", res2.Mensaje);

            // Comprobación de unicidad en ledgers
            var pagos = await context.TransactionLedgers.Where(t => t.SubastaId == 103 && t.Tipo == TipoTransaccion.PAGO).ToListAsync();
            var cobros = await context.TransactionLedgers.Where(t => t.SubastaId == 103 && t.Tipo == TipoTransaccion.COBRO).ToListAsync();

            Assert.Single(pagos);
            Assert.Single(cobros);

            // El saldo del vendedor no se duplicó
            var bVendedorFinal = await context.Billeteras.FirstAsync(b => b.UsuarioId == 1);
            Assert.Equal(saldoVendedorInicial + 12000m, bVendedorFinal.SaldoTotal);
        }

        // ========================================================================
        // 5. (e) CIERRE EXTENDIDO: TRAS ANTI-SNIPING NO SE CIERRA ANTES DE NUEVA FECHAFIN
        // ========================================================================
        [Fact]
        public async Task Test_E_CierreExtendido_TrasAntiSniping_NoSeCierraAntesDeNuevaFechaFin()
        {
            var (cerrarHandler, _, _, context) = CrearContextoYHandlers();
            var ahora = DateTime.UtcNow;

            // Subasta cuya fecha original era hace 10 segundos, pero fue extendida +2 minutos hacia el futuro
            var subasta = new Subasta
            {
                Id = 104,
                Titulo = "Guitarra Eléctrica",
                Descripcion = "Música",
                PrecioBase = 25000m,
                IncrementoMinimo = 1000m,
                FechaInicio = ahora.AddDays(-1),
                FechaFin = ahora.AddMinutes(2), // Extendida hacia el futuro!
                Estado = "ACTIVA",
                CategoriaId = 1,
                VendedorId = 1,
                Version = 2
            };
            context.Subastas.Add(subasta);
            context.Pujas.Add(new Puja { Id = 205, SubastaId = 104, UsuarioId = 3, Monto = 26000m, FechaCreacion = ahora.AddSeconds(-20) });
            await context.SaveChangesAsync();

            // Act
            var res = await cerrarHandler.HandleAsync(new CerrarSubastaCommand { SubastaId = 104 });

            // Assert: No se cierra
            Assert.False(res.Exitoso);
            Assert.Contains("todavía no ha alcanzado su fecha de finalización", res.Mensaje);

            var sDb = await context.Subastas.FirstAsync(s => s.Id == 104);
            Assert.Equal("ACTIVA", sDb.Estado);
            Assert.Null(sDb.GanadorId);
        }

        // ========================================================================
        // 6. (f) FALLO EN UNA SUBASTA NO IMPIDE CERRAR LA SIGUIENTE
        // ========================================================================
        [Fact]
        public async Task Test_F_FalloEnUnaSubasta_NoImpideCerrarLaSiguiente()
        {
            var (_, _, ejecutarCierresHandler, context) = CrearContextoYHandlers();
            var ahora = DateTime.UtcNow;

            // Subasta 105: Fallará porque el comprador tiene saldo insuficiente
            var s105 = new Subasta
            {
                Id = 105,
                Titulo = "Subasta Problemática",
                Descripcion = "Art",
                PrecioBase = 10000m,
                IncrementoMinimo = 500m,
                FechaInicio = ahora.AddDays(-2),
                FechaFin = ahora.AddMinutes(-10),
                Estado = "ACTIVA",
                CategoriaId = 1,
                VendedorId = 1,
                Version = 1
            };
            context.Subastas.Add(s105);
            context.Pujas.Add(new Puja { Id = 206, SubastaId = 105, UsuarioId = 2, Monto = 15000m, FechaCreacion = ahora.AddHours(-1) });

            var b2 = await context.Billeteras.FirstAsync(b => b.UsuarioId == 2);
            b2.SaldoRetenido = 2000m; // Menor a los 15000m requeridos!
            b2.SaldoDisponible = 0m;
            b2.SaldoTotal = 2000m;

            // Subasta 106: Válida con saldo suficiente
            var s106 = new Subasta
            {
                Id = 106,
                Titulo = "Subasta Válida Siguiente",
                Descripcion = "Tech",
                PrecioBase = 5000m,
                IncrementoMinimo = 500m,
                FechaInicio = ahora.AddDays(-2),
                FechaFin = ahora.AddMinutes(-5),
                Estado = "ACTIVA",
                CategoriaId = 1,
                VendedorId = 1,
                Version = 1
            };
            context.Subastas.Add(s106);
            context.Pujas.Add(new Puja { Id = 207, SubastaId = 106, UsuarioId = 3, Monto = 6000m, FechaCreacion = ahora.AddHours(-1) });

            var b3 = await context.Billeteras.FirstAsync(b => b.UsuarioId == 3);
            b3.SaldoRetenido = 6000m;
            b3.SaldoDisponible = 94000m;
            b3.SaldoTotal = 100000m;

            await context.SaveChangesAsync();

            // Act: Ejecutar el orquestador de cierres
            var resumen = await ejecutarCierresHandler.HandleAsync(new EjecutarCierresCommand());

            // Assert
            Assert.Equal(1, resumen.Fallidas);     // Subasta 105 falló
            Assert.Equal(1, resumen.Finalizadas);  // Subasta 106 finalizó correctamente!

            // Subasta 105 sigue ACTIVA
            var s105Db = await context.Subastas.FirstAsync(s => s.Id == 105);
            Assert.Equal("ACTIVA", s105Db.Estado);

            // Subasta 106 quedó FINALIZADA exitosamente
            var s106Db = await context.Subastas.FirstAsync(s => s.Id == 106);
            Assert.Equal("FINALIZADA", s106Db.Estado);
            Assert.Equal(3, s106Db.GanadorId);
            Assert.Equal(6000m, s106Db.PrecioFinal);
        }

        // ========================================================================
        // 7. B07: SIN CAMINO PÚBLICO PARA LIBERAR SIN RETENCIÓN NI MOVIMIENTOS DISTINTOS DE DEPOSITO; DEPÓSITOS <= 0 SE RECHAZAN
        // ========================================================================
        [Fact]
        public async Task Test_G_B07_InconsistenciaFinanciera_Y_SeguridadDeMovimientosPublicos()
        {
            var (_, _, _, context) = CrearContextoYHandlers();
            var scopeFactory = _serviceProvider.GetRequiredService<IServiceScopeFactory>();
            var billeteraRepo = new BilleteraRepository(context);
            var ledgerRepo = new Repository<TransactionLedger>(context);
            var unitOfWork = new UnitOfWork(context);
            var auditoria = new AuditoriaService(context, scopeFactory, NullLogger<AuditoriaService>.Instance);

            var cargarHandler = new CargarSaldoCommandHandler(billeteraRepo, ledgerRepo, unitOfWork, auditoria);

            // 1. Dominio: LiberarSaldo sin retención previa lanza DominioException
            var b = new Billetera { SaldoTotal = 1000m, SaldoDisponible = 1000m, SaldoRetenido = 0m };
            var exLib = Assert.Throws<DominioException>(() => b.LiberarSaldo(100m));
            Assert.Contains("No existe retención previa suficiente", exLib.Message);

            // 2. Dominio: DebitarRetencion sin retención previa lanza DominioException
            var exDeb = Assert.Throws<DominioException>(() => b.DebitarRetencion(100m));
            Assert.Contains("Saldo retenido insuficiente para debitar liquidación", exDeb.Message);

            // 3. Dominio: CargarSaldo <= 0 lanza DominioException
            Assert.Throws<DominioException>(() => b.CargarSaldo(0m));
            Assert.Throws<DominioException>(() => b.CargarSaldo(-50m));

            // 4. Application: CargarSaldoCommand <= 0 lanza ReglaNegocioException
            await Assert.ThrowsAsync<ReglaNegocioException>(() =>
                cargarHandler.HandleAsync(new CargarSaldoCommand { UsuarioId = 1, Monto = 0m })
            );
            await Assert.ThrowsAsync<ReglaNegocioException>(() =>
                cargarHandler.HandleAsync(new CargarSaldoCommand { UsuarioId = 1, Monto = -100m })
            );

            // 5. Application: El único camino de recarga genera estrictamente DEPOSITO
            await cargarHandler.HandleAsync(new CargarSaldoCommand { UsuarioId = 1, Monto = 2500m });
            var bDb = await billeteraRepo.ObtenerPorUsuarioIdAsync(1);
            Assert.NotNull(bDb);

            var ultimosMovimientos = await ledgerRepo.BuscarAsync(l => l.BilleteraId == bDb.Id);
            var ultimoDeposito = ultimosMovimientos.OrderByDescending(l => l.Id).First();
            Assert.Equal(TipoTransaccion.DEPOSITO, ultimoDeposito.Tipo);
            Assert.Equal(2500m, ultimoDeposito.Monto);
        }
    }
}
