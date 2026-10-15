using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PROYECTO_SUBASTA.Application.Constants;
using PROYECTO_SUBASTA.Application.DTOs;
using PROYECTO_SUBASTA.Application.Repositories;
using PROYECTO_SUBASTA.Application.UseCases;
using PROYECTO_SUBASTA.Domain.Constants;
using PROYECTO_SUBASTA.Domain.Entities;
using PROYECTO_SUBASTA.Domain.Exceptions;

namespace PROYECTO_SUBASTA.Application.Commands.Subastas
{
    public class CerrarSubastasVencidasCommandHandler
    {
        private readonly ISubastaRepository _subastaRepository;
        private readonly IBilleteraRepository _billeteraRepository;
        private readonly ILedgerRepository _ledgerRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IAuditoriaService _auditoriaService;
        private readonly ILogger<CerrarSubastasVencidasCommandHandler> _logger;

        public CerrarSubastasVencidasCommandHandler(
            ISubastaRepository subastaRepository,
            IBilleteraRepository billeteraRepository,
            ILedgerRepository ledgerRepository,
            IUnitOfWork unitOfWork,
            IAuditoriaService auditoriaService,
            ILogger<CerrarSubastasVencidasCommandHandler>? logger = null)
        {
            _subastaRepository = subastaRepository ?? throw new ArgumentNullException(nameof(subastaRepository));
            _billeteraRepository = billeteraRepository ?? throw new ArgumentNullException(nameof(billeteraRepository));
            _ledgerRepository = ledgerRepository ?? throw new ArgumentNullException(nameof(ledgerRepository));
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _auditoriaService = auditoriaService ?? throw new ArgumentNullException(nameof(auditoriaService));
            _logger = logger ?? NullLogger<CerrarSubastasVencidasCommandHandler>.Instance;
        }

        public async Task<CierreSubastasResultDto> HandleAsync(CerrarSubastasVencidasCommand? command = null, CancellationToken cancellationToken = default)
        {
            var resultado = new CierreSubastasResultDto();
            var ahoraUtc = command?.FechaReferenciaUtc ?? DateTime.UtcNow;

            // 1. Activar subastas PROGRAMADAS cuya FechaInicio ya llegó y FechaFin es futura
            var subastasPorActivar = await _subastaRepository.ObtenerPorActivarAsync(ahoraUtc, cancellationToken);
            foreach (var s in subastasPorActivar)
            {
                var estadoPrevio = s.Estado;
                s.Activar();
                resultado.Activadas++;

                await _auditoriaService.RegistrarAsync(
                    AuditoriaAcciones.CAMBIO_ESTADO_SUBASTA,
                    $"Subasta #{s.Id} ('{s.Titulo}') cambió de {estadoPrevio} a ACTIVA al iniciar su período.",
                    null
                );
            }

            if (subastasPorActivar.Count > 0)
            {
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }

            // 2. Obtener los identificadores de subastas que vencieron y requieren cierre
            var subastasVencidasIds = await _subastaRepository.ObtenerIdsVencidasAsync(ahoraUtc, cancellationToken);

            // 3. Procesar cada subasta vencida en una transacción aislada e independiente
            foreach (var subastaId in subastasVencidasIds)
            {
                await _unitOfWork.BeginTransactionAsync(cancellationToken);
                try
                {
                    var subasta = await _subastaRepository.ObtenerPorIdAsync(subastaId);
                    if (subasta == null || (subasta.Estado != EstadoSubasta.ACTIVA && subasta.Estado != EstadoSubasta.PROGRAMADA) || subasta.FechaFin > ahoraUtc)
                    {
                        await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                        _unitOfWork.DescartarCambios();
                        continue;
                    }

                    var estadoAnterior = subasta.Estado;
                    var tienePujas = subasta.Pujas != null && subasta.Pujas.Any();

                    if (tienePujas)
                    {
                        var mayorPuja = subasta.Pujas!.OrderByDescending(p => p.Monto).First();
                        var ganadorId = mayorPuja.UsuarioId;
                        var precioFinal = mayorPuja.Monto;
                        var vendedorId = subasta.VendedorId;

                        var billeteraComprador = await _billeteraRepository.ObtenerPorUsuarioIdAsync(ganadorId);
                        if (billeteraComprador == null)
                        {
                            throw new DominioException($"No se encontró la billetera del comprador ganador (Usuario #{ganadorId}) para liquidar la subasta #{subasta.Id}.");
                        }

                        var billeteraVendedor = await _billeteraRepository.ObtenerPorUsuarioIdAsync(vendedorId);
                        if (billeteraVendedor == null)
                        {
                            throw new DominioException($"No se encontró la billetera del vendedor (Usuario #{vendedorId}) para liquidar la subasta #{subasta.Id}.");
                        }

                        // Invariante financiero: calcula la garantía de ESA subasta con el ledger: (RETENCION - LIBERACION)
                        // y exige que sea >= precioFinal además de SaldoRetenido >= precioFinal
                        var ledgersSubasta = await _ledgerRepository.ObtenerPorSubastaAsync(subasta.Id, cancellationToken);
                        var retencionSubasta = ledgersSubasta
                            .Where(l => l.BilleteraId == billeteraComprador.Id && l.Tipo == TipoTransaccion.RETENCION)
                            .Sum(l => l.Monto);
                        var liberacionSubasta = ledgersSubasta
                            .Where(l => l.BilleteraId == billeteraComprador.Id && l.Tipo == TipoTransaccion.LIBERACION)
                            .Sum(l => l.Monto);
                        var garantiaSubastaComprador = retencionSubasta - liberacionSubasta;

                        if (billeteraComprador.SaldoRetenido < precioFinal || garantiaSubastaComprador < precioFinal)
                        {
                            throw new DominioException($"Saldo retenido o garantía específica insuficiente en la billetera del comprador #{ganadorId} (SaldoRetenido: ${billeteraComprador.SaldoRetenido:N2}, Garantía Subasta: ${garantiaSubastaComprador:N2}) para cubrir el precio final adjudicado (${precioFinal:N2}) de la subasta #{subasta.Id}.");
                        }

                        subasta.Finalizar(ganadorId, precioFinal);
                        resultado.Finalizadas++;

                        // Idempotencia: si ya existe PAGO para esta subasta, no volver a liquidar
                        var yaLiquidada = await _ledgerRepository.ExistePagoPorSubastaAsync(subasta.Id, cancellationToken);
                        if (!yaLiquidada)
                        {
                            billeteraComprador.DebitarRetencion(precioFinal);
                            await _ledgerRepository.RegistrarAsync(new TransactionLedger
                            {
                                BilleteraId = billeteraComprador.Id,
                                Tipo = TipoTransaccion.PAGO,
                                Monto = precioFinal,
                                Fecha = ahoraUtc,
                                SubastaId = subasta.Id
                            }, cancellationToken);

                            billeteraVendedor.AcreditarCobro(precioFinal);
                            await _ledgerRepository.RegistrarAsync(new TransactionLedger
                            {
                                BilleteraId = billeteraVendedor.Id,
                                Tipo = TipoTransaccion.COBRO,
                                Monto = precioFinal,
                                Fecha = ahoraUtc,
                                SubastaId = subasta.Id
                            }, cancellationToken);
                        }

                        // Liberación formal de garantías para postores perdedores (excluyendo la billetera del comprador ganador)
                        await LiberarGarantiasPostoresAsync(subasta.Id, billeteraComprador.Id, ahoraUtc, cancellationToken);

                        await _auditoriaService.RegistrarAsync(
                            AuditoriaAcciones.CAMBIO_ESTADO_SUBASTA,
                            $"Subasta #{subasta.Id} ('{subasta.Titulo}') cambió de {estadoAnterior} a FINALIZADA tras vencer el tiempo. Ganador: Usuario #{ganadorId} con puja de ${precioFinal:N2}.",
                            ganadorId
                        );

                        await _auditoriaService.RegistrarAsync(
                            AuditoriaAcciones.VENTA_REGISTRADA,
                            $"Liquidación final de Subasta #{subasta.Id} ('{subasta.Titulo}'): Venta concretada por ${precioFinal:N2}. Saldo transferido de Comprador #{ganadorId} a Vendedor #{vendedorId}.",
                            vendedorId
                        );
                    }
                    else
                    {
                        // Sin ofertas: se declara DESIERTA
                        subasta.DeclararDesierta();
                        resultado.Desiertas++;

                        // Liberación formal de garantías si hubiera quedado alguna retenida (sin postor ganador que excluir)
                        await LiberarGarantiasPostoresAsync(subasta.Id, null, ahoraUtc, cancellationToken);

                        await _auditoriaService.RegistrarAsync(
                            AuditoriaAcciones.CAMBIO_ESTADO_SUBASTA,
                            $"Subasta #{subasta.Id} ('{subasta.Titulo}') pasó de {estadoAnterior} a DESIERTA al vencer el tiempo sin ofertas registradas.",
                            null
                        );
                    }

                    await _unitOfWork.CommitTransactionAsync(cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error en el cierre transaccional de Subasta #{SubastaId}: {Message}", subastaId, ex.Message);

                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    _unitOfWork.DescartarCambios();

                    var existeReciente = await _auditoriaService.ExisteEventoRecienteAsync(
                        AuditoriaAcciones.FALLO_CIERRE_SUBASTA,
                        $"Subasta #{subastaId}",
                        TimeSpan.FromMinutes(10)
                    );

                    if (!existeReciente)
                    {
                        await _auditoriaService.RegistrarFalloAsync(
                            AuditoriaAcciones.FALLO_CIERRE_SUBASTA,
                            $"Error en el cierre transaccional de Subasta #{subastaId}: {ex.Message}",
                            null
                        );
                    }
                }
            }

            return resultado;
        }

        /// <summary>
        /// Libera formalmente las garantías retenidas de todos los postores perdedores en una subasta.
        /// </summary>
        private async Task LiberarGarantiasPostoresAsync(
            int subastaId,
            int? billeteraExcluidaId,
            DateTime ahoraUtc,
            CancellationToken cancellationToken)
        {
            var ledgersSubasta = await _ledgerRepository.ObtenerPorSubastaAsync(subastaId, cancellationToken);
            var billeterasPostoresIds = ledgersSubasta
                .Select(l => l.BilleteraId)
                .Distinct()
                .Where(bId => !billeteraExcluidaId.HasValue || bId != billeteraExcluidaId.Value)
                .ToList();

            foreach (var bId in billeterasPostoresIds)
            {
                var retenciones = ledgersSubasta
                    .Where(l => l.BilleteraId == bId && l.Tipo == TipoTransaccion.RETENCION)
                    .Sum(l => l.Monto);

                var liberaciones = ledgersSubasta
                    .Where(l => l.BilleteraId == bId && l.Tipo == TipoTransaccion.LIBERACION)
                    .Sum(l => l.Monto);

                var saldoPendienteLiberar = retenciones - liberaciones;
                if (saldoPendienteLiberar > 0)
                {
                    var billeteraPostor = await _billeteraRepository.GetByIdAsync(bId);
                    if (billeteraPostor != null)
                    {
                        var montoLiberar = Math.Min(billeteraPostor.SaldoRetenido, saldoPendienteLiberar);
                        if (montoLiberar > 0)
                        {
                            billeteraPostor.LiberarSaldo(montoLiberar);
                            await _ledgerRepository.RegistrarAsync(new TransactionLedger
                            {
                                BilleteraId = billeteraPostor.Id,
                                Tipo = TipoTransaccion.LIBERACION,
                                Monto = montoLiberar,
                                Fecha = ahoraUtc,
                                SubastaId = subastaId
                            }, cancellationToken);
                        }
                    }
                }
            }
        }
    }
}
