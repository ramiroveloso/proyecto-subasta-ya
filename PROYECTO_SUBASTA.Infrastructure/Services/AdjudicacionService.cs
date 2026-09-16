using System;
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
            _context = context;
            _auditoriaService = auditoriaService;
            _logger = logger;
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
                    $"Subasta #{s.Id} ('{s.Titulo}') cambió de {estadoPrevio} a ACTIVA ejecutado por el Worker al iniciar su período.",
                    null
                );
            }

            // 2. Subastas que vencieron y no están en estado final (ACTIVA o PROGRAMADA que ya venció)
            var subastasVencidas = await _context.Subastas
                .Include(s => s.Pujas)
                .Where(s => (s.Estado == "ACTIVA" || s.Estado == "PROGRAMADA") && s.FechaFin <= ahoraUtc)
                .ToListAsync(cancellationToken);

            foreach (var subasta in subastasVencidas)
            {
                var estadoAnterior = subasta.Estado;
                var tienePujas = subasta.Pujas != null && subasta.Pujas.Any();

                if (tienePujas)
                {
                    // Con ganador: Marca la subasta como FINALIZADA, transfiere el saldo retenido de la billetera del comprador a la del vendedor (liquidación final) y registra la venta.
                    var mayorPuja = subasta.Pujas!.OrderByDescending(p => p.Monto).First();
                    var ganadorId = mayorPuja.UsuarioId;
                    var precioFinal = mayorPuja.Monto;
                    var vendedorId = subasta.VendedorId;

                    subasta.Estado = "FINALIZADA";
                    subasta.GanadorId = ganadorId;
                    subasta.PrecioFinal = precioFinal;
                    subasta.Version += 1;
                    resultado.Finalizadas++;

                    // Liquidación final en las billeteras
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
                            var saldoADebitarRetenido = Math.Min(billeteraComprador.SaldoRetenido, precioFinal);
                            billeteraComprador.SaldoRetenido -= saldoADebitarRetenido;
                            var remanente = precioFinal - saldoADebitarRetenido;
                            if (remanente > 0)
                            {
                                billeteraComprador.SaldoDisponible = Math.Max(0, billeteraComprador.SaldoDisponible - remanente);
                            }
                            billeteraComprador.SaldoTotal = Math.Max(0, billeteraComprador.SaldoTotal - precioFinal);
                            billeteraComprador.Version += 1;

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
                            billeteraVendedor.SaldoTotal += precioFinal;
                            billeteraVendedor.SaldoDisponible += precioFinal;
                            billeteraVendedor.Version += 1;

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

                    // Auditoría: Cambio de estado ejecutado por el Worker
                    await _auditoriaService.RegistrarAsync(
                        "CAMBIO_ESTADO_SUBASTA",
                        $"Subasta #{subasta.Id} ('{subasta.Titulo}') cambió de {estadoAnterior} a FINALIZADA ejecutado por el Worker tras vencer el tiempo. Ganador: Usuario #{ganadorId} con puja de ${precioFinal:N2}.",
                        ganadorId
                    );

                    // Auditoría: Venta registrada y liquidación final
                    await _auditoriaService.RegistrarAsync(
                        "VENTA_REGISTRADA",
                        $"Liquidación final de Subasta #{subasta.Id} ('{subasta.Titulo}'): Venta concretada por ${precioFinal:N2}. Saldo retenido transferido de Comprador #{ganadorId} a Billetera de Vendedor #{vendedorId}.",
                        vendedorId
                    );

                    _logger.LogInformation("Subasta #{SubastaId} finalizada y adjudicada a Usuario #{GanadorId} por ${Monto:N2}.", subasta.Id, ganadorId, precioFinal);
                }
                else
                {
                    // Sin ofertas: Si vence sin ninguna puja registrada, pasa a estado DESIERTA.
                    subasta.Estado = "DESIERTA";
                    subasta.GanadorId = null;
                    subasta.PrecioFinal = null;
                    subasta.Version += 1;
                    resultado.Desiertas++;

                    await _auditoriaService.RegistrarAsync(
                        "CAMBIO_ESTADO_SUBASTA",
                        $"Subasta #{subasta.Id} ('{subasta.Titulo}') pasó de {estadoAnterior} a DESIERTA ejecutado por el Worker al vencer el tiempo sin ninguna puja registrada.",
                        null
                    );

                    _logger.LogInformation("Subasta #{SubastaId} declarada DESIERTA por el Worker (sin ofertas).", subasta.Id);
                }
            }

            if (resultado.Activadas > 0 || resultado.Finalizadas > 0 || resultado.Desiertas > 0)
            {
                await _context.SaveChangesAsync(cancellationToken);
            }

            return resultado;
        }
    }
}
