using System;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PROYECTO_SUBASTA.Domain.Entities;
using PROYECTO_SUBASTA.Infrastructure.Data;
using Xunit;

namespace PROYECTO_SUBASTA.Domain.Tests
{
    /// <summary>
    /// Pruebas que verifican el control de concurrencia optimista (OCC) en la entidad Billetera
    /// para TODOS sus escritores de negocio:
    /// 1. Carga de saldo (CargarSaldo)
    /// 2. Registro de puja - Retención de garantía (RetenerSaldo)
    /// 3. Registro de puja - Liberación de garantía de líder previo (LiberarSaldo)
    /// 4. Cierre de subasta - Débito de liquidación a comprador (DebitarRetencion)
    /// 5. Cierre de subasta - Acreditación de cobro a vendedor (AcreditarCobro)
    /// </summary>
    public class BilleteraConcurrenciaTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly DbContextOptions<SubastaDbContext> _options;

        public BilleteraConcurrenciaTests()
        {
            _connection = new SqliteConnection($"DataSource=billetera_occ_{Guid.NewGuid():N};Mode=Memory;Cache=Shared");
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

        // ========================================================================
        // 1. VERIFICACIÓN DE INCREMENTO DE VERSIÓN EN TODOS LOS ESCRITORES
        // ========================================================================

        [Fact]
        public void TodosLosEscritores_IncrementanVersionEnMemoria()
        {
            var b = new Billetera
            {
                Id = 1,
                UsuarioId = 1,
                SaldoTotal = 100000m,
                SaldoDisponible = 100000m,
                SaldoRetenido = 0m,
                Version = 1
            };

            // 1. Carga de saldo
            b.CargarSaldo(5000m);
            Assert.Equal(2, b.Version);

            // 2. Retención (Puja - nuevo líder)
            b.RetenerSaldo(20000m);
            Assert.Equal(3, b.Version);

            // 3. Liberación (Puja - postor anterior superado)
            b.LiberarSaldo(10000m);
            Assert.Equal(4, b.Version);

            // 4. Débito de retención (Cierre - liquidación de comprador)
            b.DebitarRetencion(10000m);
            Assert.Equal(5, b.Version);

            // 5. Acreditación de cobro (Cierre - liquidación a vendedor)
            b.AcreditarCobro(10000m);
            Assert.Equal(6, b.Version);
        }

        // ========================================================================
        // 2. COLISIÓN CONCURRENTE EN CARGA DE SALDO (CargarSaldo)
        // ========================================================================

        [Fact]
        public async Task CargarSaldo_ConVersionOriginalDesactualizada_LanzaDbUpdateConcurrencyException()
        {
            // Contexto 1 y Contexto 2 leen la misma billetera con Version = 1
            using var context1 = new SubastaDbContext(_options);
            using var context2 = new SubastaDbContext(_options);

            var b1 = await context1.Billeteras.FindAsync(1);
            var b2 = await context2.Billeteras.FindAsync(1);
            Assert.NotNull(b1);
            Assert.NotNull(b2);
            int versionInicial = b1.Version;

            // Operación 1: Carga saldo y persiste exitosamente (Version pasa a versionInicial + 1)
            b1.CargarSaldo(10000m);
            await context1.SaveChangesAsync();

            // Operación 2: Intenta persistir con la versión original que ya fue modificada en la base
            b2.CargarSaldo(5000m);

            // Assert: EF Core detecta que el valor en base ya no coincide con OriginalValue y lanza DbUpdateConcurrencyException
            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => context2.SaveChangesAsync());
        }

        // ========================================================================
        // 3. COLISIÓN CONCURRENTE EN PUJA - RETENCIÓN (RetenerSaldo)
        // ========================================================================

        [Fact]
        public async Task RetenerSaldo_ConVersionOriginalDesactualizada_LanzaDbUpdateConcurrencyException()
        {
            using var context1 = new SubastaDbContext(_options);
            using var context2 = new SubastaDbContext(_options);

            var b1 = await context1.Billeteras.FindAsync(3); // Usuario 3 con fondos
            var b2 = await context2.Billeteras.FindAsync(3);
            Assert.NotNull(b1);
            Assert.NotNull(b2);

            // Operación 1: Retiene saldo en puja A
            b1.RetenerSaldo(20000m);
            await context1.SaveChangesAsync();

            // Operación 2: Puja B concurrente sobre la misma billetera partiendo de la versión previa
            b2.RetenerSaldo(30000m);

            // Assert: Colisión OCC detectada
            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => context2.SaveChangesAsync());
        }

        // ========================================================================
        // 4. COLISIÓN CONCURRENTE EN PUJA - LIBERACIÓN (LiberarSaldo)
        // ========================================================================

        [Fact]
        public async Task LiberarSaldo_ConVersionOriginalDesactualizada_LanzaDbUpdateConcurrencyException()
        {
            // Preparar saldo retenido en la billetera
            using (var setupContext = new SubastaDbContext(_options))
            {
                var bSetup = await setupContext.Billeteras.FindAsync(3);
                bSetup!.SaldoRetenido = 50000m;
                bSetup.SaldoDisponible = 150000m;
                await setupContext.SaveChangesAsync();
            }

            using var context1 = new SubastaDbContext(_options);
            using var context2 = new SubastaDbContext(_options);

            var b1 = await context1.Billeteras.FindAsync(3);
            var b2 = await context2.Billeteras.FindAsync(3);

            // Operación 1: Modifica la billetera y eleva versión
            b1!.CargarSaldo(1000m);
            await context1.SaveChangesAsync();

            // Operación 2: Intenta liberar retención con la versión obsoleta
            b2!.LiberarSaldo(20000m);

            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => context2.SaveChangesAsync());
        }

        // ========================================================================
        // 5. COLISIÓN CONCURRENTE EN CIERRE - DÉBITO DE RETENCIÓN (DebitarRetencion)
        // ========================================================================

        [Fact]
        public async Task DebitarRetencion_ConVersionOriginalDesactualizada_LanzaDbUpdateConcurrencyException()
        {
            // Preparar saldo retenido
            using (var setupContext = new SubastaDbContext(_options))
            {
                var bSetup = await setupContext.Billeteras.FindAsync(2);
                bSetup!.SaldoRetenido = 45000m;
                bSetup.SaldoDisponible = 105000m;
                await setupContext.SaveChangesAsync();
            }

            using var context1 = new SubastaDbContext(_options);
            using var context2 = new SubastaDbContext(_options);

            var b1 = await context1.Billeteras.FindAsync(2);
            var b2 = await context2.Billeteras.FindAsync(2);

            // Operación 1: Modifica concurrentemente la billetera
            b1!.CargarSaldo(5000m);
            await context1.SaveChangesAsync();

            // Operación 2: El proceso de cierre intenta debitar la garantía con la versión desactualizada
            b2!.DebitarRetencion(25000m);

            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => context2.SaveChangesAsync());
        }

        // ========================================================================
        // 6. COLISIÓN CONCURRENTE EN CIERRE - ACREDITACIÓN DE COBRO (AcreditarCobro)
        // ========================================================================

        [Fact]
        public async Task AcreditarCobro_ConVersionOriginalDesactualizada_LanzaDbUpdateConcurrencyException()
        {
            using var context1 = new SubastaDbContext(_options);
            using var context2 = new SubastaDbContext(_options);

            var b1 = await context1.Billeteras.FindAsync(1);
            var b2 = await context2.Billeteras.FindAsync(1);

            // Operación 1: Modifica concurrentemente la billetera del vendedor
            b1!.CargarSaldo(2000m);
            await context1.SaveChangesAsync();

            // Operación 2: El proceso de liquidación intenta acreditar cobro con versión desactualizada
            b2!.AcreditarCobro(25000m);

            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => context2.SaveChangesAsync());
        }
    }
}
