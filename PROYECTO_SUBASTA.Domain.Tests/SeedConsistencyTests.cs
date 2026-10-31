using System.Linq;
using Xunit;
using PROYECTO_SUBASTA.Domain.Entities;
using PROYECTO_SUBASTA.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System;

namespace PROYECTO_SUBASTA.Domain.Tests
{
    public class SeedConsistencyTests
    {
        [Fact]
        public void DataSeed_BilleterasAndLedger_ConsistencyCheck()
        {
            // Arrange
            var options = new DbContextOptionsBuilder<SubastaDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            using (var context = new SubastaDbContext(options))
            {
                // Limpiar BD en memoria para tests aislados
                context.Database.EnsureDeleted();
                context.Database.EnsureCreated();

                var billeteras = context.Billeteras.Include(b => b.Movimientos).ToList();

                // 1. Verificar que no haya billeteras con Total ≠ Retenido + Disponible
                foreach (var billetera in billeteras)
                {
                    Assert.Equal(billetera.SaldoTotal, billetera.SaldoRetenido + billetera.SaldoDisponible);

                    // 2. Fórmulas de consistencia con el Ledger
                    // Total = Σ(DEPOSITO + COBRO − PAGO)
                    var movimientos = billetera.Movimientos;

                    var depositos = movimientos.Where(m => m.Tipo == TipoTransaccion.DEPOSITO).Sum(m => m.Monto);
                    var cobros = movimientos.Where(m => m.Tipo == TipoTransaccion.COBRO).Sum(m => m.Monto);
                    var pagos = movimientos.Where(m => m.Tipo == TipoTransaccion.PAGO).Sum(m => m.Monto);

                    var totalEsperado = depositos + cobros - pagos;
                    Assert.Equal(billetera.SaldoTotal, totalEsperado);

                    // Retenido = Σ(RETENCION − LIBERACION − PAGO)
                    var subastasInvolucradas = movimientos.Where(m => m.SubastaId != null).Select(m => m.SubastaId.GetValueOrDefault()).Distinct();

                    decimal retenidoTotalCalculado = 0;
                    foreach(var sId in subastasInvolucradas)
                    {
                        var m = movimientos.Where(x => x.SubastaId == sId);
                        var r = m.Where(x => x.Tipo == TipoTransaccion.RETENCION).Sum(x => x.Monto);
                        var l = m.Where(x => x.Tipo == TipoTransaccion.LIBERACION).Sum(x => x.Monto);
                        var p = m.Where(x => x.Tipo == TipoTransaccion.PAGO).Sum(x => x.Monto);

                        var retPendiente = r - l - p;
                        retenidoTotalCalculado += retPendiente;
                    }

                    Assert.Equal(billetera.SaldoRetenido, retenidoTotalCalculado);
                }
            }
        }
    }
}
