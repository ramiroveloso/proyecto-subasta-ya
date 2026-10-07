using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PROYECTO_SUBASTA.Application.UseCases;
using PROYECTO_SUBASTA.Domain.Entities;
using PROYECTO_SUBASTA.Infrastructure.Data;

namespace PROYECTO_SUBASTA.Infrastructure.Services
{
    public class AdjudicacionService : IAdjudicacionService
    {
        private readonly SubastaDbContext _context;
        private readonly IAuditoriaService _auditoriaService;
        private readonly ILogger<AdjudicacionService> _logger;

        public AdjudicacionService(
            SubastaDbContext context,
            IAuditoriaService auditoriaService,
            ILogger<AdjudicacionService> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _auditoriaService = auditoriaService ?? throw new ArgumentNullException(nameof(auditoriaService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<AdjudicacionResultado> ProcesarSubastasVencidasAsync(CancellationToken cancellationToken = default)
        {
            var resultado = new AdjudicacionResultado();
            var ahoraUtc = DateTime.UtcNow;

            // 1. Activar subastas PROGRAMADAS cuya FechaInicio ya llegó y FechaFin es futura
            var subastasPorActivar = await _context.Subastas
                .Where(s => s.Estado == "PROGRAMADA" && s.FechaInicio <= ahoraUtc && s.FechaFin > ahoraUtc)
                .ToListAsync(cancellationToken);

            foreach (var s in subastasPorActivar)
            {
                var estadoPrevio = s.Estado;
                s.Estado = "ACTIVA";
                s.Version += 1;
                resultado.Activadas++;

                await _auditoriaService.RegistrarAsync(
                    "CAMBIO_ESTADO_SUBASTA",
                    $"Subasta #{s.Id} ('{s.Titulo}') cambió de {estadoPrevio} a ACTIVA al iniciar su período.",
                    null
                );
            }

            if (subastasPorActivar.Any())
            {
                await _context.SaveChangesAsync(cancellationToken);
            }

            // 2. Obtener los identificadores de subastas que vencieron y requieren cierre
            var subastasVencidasIds = await _context.Subastas
                .Where(s => (s.Estado == "ACTIVA" || s.Estado == "PROGRAMADA") && s.FechaFin <= ahoraUtc)
                .Select(s => s.Id)
                .ToListAsync(cancellationToken);

            // 3. Procesar cada subasta vencida en una transacción aislada e independiente
            foreach (var subastaId in subastasVencidasIds)
            {
                await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
                try
                {
                    var subasta = await _context.Subastas
                        .Include(s => s.Pujas)
                        .FirstOrDefaultAsync(s => s.Id == subastaId, cancellationToken);

                    if (subasta == null || (subasta.Estado != "ACTIVA" && subasta.Estado != "PROGRAMADA") || subasta.FechaFin > ahoraUtc)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        continue;
                    }

                    var estadoAnterior = subasta.Estado;
                    var tienePujas = subasta.Pujas != null && subasta.Pujas.Any();

                    if (tienePujas)
                    {
                        // Con ganador: adjudicar y liquidar fondos formalmente
                        var mayorPuja = subasta.Pujas!.OrderByDescending(p => p.Monto).First();
                        var ganadorId = mayorPuja.UsuarioId;
                        var precioFinal = mayorPuja.Monto;
                        var vendedorId = subasta.VendedorId;

                        subasta.Finalizar(ganadorId, precioFinal);
                        resultado.Finalizadas++;

                        var billeteraComprador = await _context.Billeteras
                            .FirstOrDefaultAsync(b => b.UsuarioId == ganadorId, cancellationToken);
                        var billeteraVendedor = await _context.Billeteras
                            .FirstOrDefaultAsync(b => b.UsuarioId == vendedorId, cancellationToken);

                        var yaLiquidada = await _context.TransactionLedgers
                            .AnyAsync(t => t.SubastaId == subasta.Id && t.Tipo == TipoTransaccion.PAGO, cancellationToken);

                        if (!yaLiquidada)
                        {
                            if (billeteraComprador != null)
                            {
                                if (billeteraComprador.SaldoRetenido >= precioFinal)
                                {
                                    billeteraComprador.DebitarRetencion(precioFinal);
                                }
                                else
                                {
                                    // Resguardo de consistencia para datos históricos incompletos
                                    var saldoADebitarRetenido = Math.Min(billeteraComprador.SaldoRetenido, precioFinal);
                                    billeteraComprador.SaldoRetenido -= saldoADebitarRetenido;
                                    billeteraComprador.SaldoTotal = Math.Max(0, billeteraComprador.SaldoTotal - precioFinal);
                                    billeteraComprador.SaldoDisponible = Math.Max(0, billeteraComprador.SaldoTotal - billeteraComprador.SaldoRetenido);
                                    billeteraComprador.Version++;
                                }

                                _context.TransactionLedgers.Add(new TransactionLedger
                                {
                                    BilleteraId = billeteraComprador.Id,
                                    Tipo = TipoTransaccion.PAGO,
                                    Monto = precioFinal,
                                    Fecha = ahoraUtc,
                                    SubastaId = subasta.Id
                                });
                            }

                            if (billeteraVendedor != null)
                            {
                                billeteraVendedor.AcreditarCobro(precioFinal);

                                _context.TransactionLedgers.Add(new TransactionLedger
                                {
                                    BilleteraId = billeteraVendedor.Id,
                                    Tipo = TipoTransaccion.COBRO,
                                    Monto = precioFinal,
                                    Fecha = ahoraUtc,
                                    SubastaId = subasta.Id
                                });
                            }
                        }

                        // Liberación formal de garantías para todos los postores perdedores
                        var ledgersSubasta = await _context.TransactionLedgers
                            .Where(t => t.SubastaId == subasta.Id)
                            .ToListAsync(cancellationToken);

                        var billeterasPostoresIds = ledgersSubasta
                            .Select(l => l.BilleteraId)
                            .Distinct()
                            .Where(bId => billeteraComprador == null || bId != billeteraComprador.Id)
                            .ToList();

                        foreach (var bId in billeterasPostoresIds)
                        {
                            var retenciones = ledgersSubasta.Where(l => l.BilleteraId == bId && l.Tipo == TipoTransaccion.RETENCION).Sum(l => l.Monto);
                            var liberaciones = ledgersSubasta.Where(l => l.BilleteraId == bId && l.Tipo == TipoTransaccion.LIBERACION).Sum(l => l.Monto);
                            var saldoPendienteLiberar = retenciones - liberaciones;

                            if (saldoPendienteLiberar > 0)
                            {
                                var billeteraPerdedor = await _context.Billeteras.FindAsync(new object[] { bId }, cancellationToken);
                                if (billeteraPerdedor != null)
                                {
                                    var montoLiberar = Math.Min(billeteraPerdedor.SaldoRetenido, saldoPendienteLiberar);
                                    if (montoLiberar > 0)
                                    {
                                        billeteraPerdedor.LiberarSaldo(montoLiberar);
                                        _context.TransactionLedgers.Add(new TransactionLedger
                                        {
                                            BilleteraId = billeteraPerdedor.Id,
                                            Tipo = TipoTransaccion.LIBERACION,
                                            Monto = montoLiberar,
                                            Fecha = ahoraUtc,
                                            SubastaId = subasta.Id
                                        });
                                    }
                                }
                            }
                        }

                        await _auditoriaService.RegistrarAsync(
                            "CAMBIO_ESTADO_SUBASTA",
                            $"Subasta #{subasta.Id} ('{subasta.Titulo}') cambió de {estadoAnterior} a FINALIZADA tras vencer el tiempo. Ganador: Usuario #{ganadorId} con puja de ${precioFinal:N2}.",
                            ganadorId
                        );

                        await _auditoriaService.RegistrarAsync(
                            "VENTA_REGISTRADA",
                            $"Liquidación final de Subasta #{subasta.Id} ('{subasta.Titulo}'): Venta concretada por ${precioFinal:N2}. Saldo transferido de Comprador #{ganadorId} a Vendedor #{vendedorId}.",
                            vendedorId
                        );

                        _logger.LogInformation("Subasta #{SubastaId} finalizada y liquidada para Ganador #{GanadorId} por ${Monto:N2}.", subasta.Id, ganadorId, precioFinal);
                    }
                    else
                    {
                        // Sin ofertas: se declara DESIERTA
                        subasta.DeclararDesierta();
                        resultado.Desiertas++;

                        // Liberación preventiva de cualquier saldo retenido si hubiera quedado registrado
                        var ledgersSubasta = await _context.TransactionLedgers
                            .Where(t => t.SubastaId == subasta.Id)
                            .ToListAsync(cancellationToken);

                        var billeterasIds = ledgersSubasta.Select(l => l.BilleteraId).Distinct().ToList();
                        foreach (var bId in billeterasIds)
                        {
                            var retenciones = ledgersSubasta.Where(l => l.BilleteraId == bId && l.Tipo == TipoTransaccion.RETENCION).Sum(l => l.Monto);
                            var liberaciones = ledgersSubasta.Where(l => l.BilleteraId == bId && l.Tipo == TipoTransaccion.LIBERACION).Sum(l => l.Monto);
                            var saldoPendiente = retenciones - liberaciones;

                            if (saldoPendiente > 0)
                            {
                                var b = await _context.Billeteras.FindAsync(new object[] { bId }, cancellationToken);
                                if (b != null)
                                {
                                    var montoLiberar = Math.Min(b.SaldoRetenido, saldoPendiente);
                                    if (montoLiberar > 0)
                                    {
                                        b.LiberarSaldo(montoLiberar);
                                        _context.TransactionLedgers.Add(new TransactionLedger
                                        {
                                            BilleteraId = b.Id,
                                            Tipo = TipoTransaccion.LIBERACION,
                                            Monto = montoLiberar,
                                            Fecha = ahoraUtc,
                                            SubastaId = subasta.Id
                                        });
                                    }
                                }
                            }
                        }

                        await _auditoriaService.RegistrarAsync(
                            "CAMBIO_ESTADO_SUBASTA",
                            $"Subasta #{subasta.Id} ('{subasta.Titulo}') pasó de {estadoAnterior} a DESIERTA al vencer el tiempo sin ofertas registradas.",
                            null
                        );

                        _logger.LogInformation("Subasta #{SubastaId} declarada DESIERTA (sin ofertas).", subasta.Id);
                    }

                    await _context.SaveChangesAsync(cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    _logger.LogError(ex, "Error al procesar el cierre aislado de la subasta #{SubastaId}.", subastaId);
                    await _auditoriaService.RegistrarFalloAsync(
                        "FALLO_CIERRE_SUBASTA",
                        $"Error en el cierre transaccional de Subasta #{subastaId}: {ex.Message}",
                        null
                    );
                }
            }

            return resultado;
        }
    }
}
