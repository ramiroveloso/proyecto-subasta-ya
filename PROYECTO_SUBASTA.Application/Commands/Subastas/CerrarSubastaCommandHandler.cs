using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PROYECTO_SUBASTA.Application.Repositories;
using PROYECTO_SUBASTA.Application.UseCases;
using PROYECTO_SUBASTA.Domain.Entities;
using PROYECTO_SUBASTA.Domain.Exceptions;

namespace PROYECTO_SUBASTA.Application.Commands.Subastas
{
    public class CerrarSubastaCommandHandler
    {
        private readonly ISubastaRepository _subastaRepository;
        private readonly IBilleteraRepository _billeteraRepository;
        private readonly IRepository<TransactionLedger> _ledgerRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IAuditoriaService _auditoriaService;
        private readonly ILogger<CerrarSubastaCommandHandler> _logger;

        public CerrarSubastaCommandHandler(
            ISubastaRepository subastaRepository,
            IBilleteraRepository billeteraRepository,
            IRepository<TransactionLedger> ledgerRepository,
            IUnitOfWork unitOfWork,
            IAuditoriaService auditoriaService,
            ILogger<CerrarSubastaCommandHandler> logger)
        {
            _subastaRepository = subastaRepository ?? throw new ArgumentNullException(nameof(subastaRepository));
            _billeteraRepository = billeteraRepository ?? throw new ArgumentNullException(nameof(billeteraRepository));
            _ledgerRepository = ledgerRepository ?? throw new ArgumentNullException(nameof(ledgerRepository));
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _auditoriaService = auditoriaService ?? throw new ArgumentNullException(nameof(auditoriaService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<CerrarSubastaResultadoDto> HandleAsync(CerrarSubastaCommand command, CancellationToken cancellationToken = default)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));

            await _unitOfWork.BeginTransactionAsync(cancellationToken);

            try
            {
                // 1. Recargar la subasta dentro de la transacción para evaluar el estado y FechaFin actuales
                var subasta = await _subastaRepository.ObtenerPorIdAsync(command.SubastaId);
                if (subasta == null)
                {
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    _unitOfWork.DescartarCambiosPendientes();
                    return new CerrarSubastaResultadoDto
                    {
                        SubastaId = command.SubastaId,
                        Exitoso = false,
                        Mensaje = $"No se encontró la subasta con ID {command.SubastaId}."
                    };
                }

                var ahoraUtc = DateTime.UtcNow;

                // 2. Regla de idempotencia y estado: si ya no es ACTIVA (o PROGRAMADA), no hacer nada
                if (subasta.Estado != "ACTIVA" && subasta.Estado != "PROGRAMADA")
                {
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    _unitOfWork.DescartarCambiosPendientes();
                    return new CerrarSubastaResultadoDto
                    {
                        SubastaId = subasta.Id,
                        Estado = subasta.Estado,
                        Exitoso = true,
                        Mensaje = $"La subasta ya se encuentra en estado '{subasta.Estado}'. Operación idempotente omitida."
                    };
                }

                // 3. Regla temporal: cerrar únicamente si ahoraUtc >= FechaFin (respeta extensiones por anti-sniping)
                if (ahoraUtc < subasta.FechaFin)
                {
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    _unitOfWork.DescartarCambiosPendientes();
                    return new CerrarSubastaResultadoDto
                    {
                        SubastaId = subasta.Id,
                        Estado = subasta.Estado,
                        Exitoso = false,
                        Mensaje = "La subasta todavía no ha alcanzado su fecha de finalización (posible extensión anti-sniping activa)."
                    };
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
                    var billeteraVendedor = await _billeteraRepository.ObtenerPorUsuarioIdAsync(vendedorId);

                    // 4. Liquidación estricta: si el comprador no tiene SaldoRetenido suficiente, NO liquidar
                    if (billeteraComprador == null || billeteraComprador.SaldoRetenido < precioFinal)
                    {
                        await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                        _unitOfWork.DescartarCambiosPendientes();

                        var saldoActual = billeteraComprador?.SaldoRetenido ?? 0m;
                        await _auditoriaService.RegistrarFalloAsync(
                            "FALLO_CIERRE_SUBASTA",
                            $"Saldo retenido insuficiente (${saldoActual:N2}) en la billetera de Usuario #{ganadorId} para cubrir el precio final (${precioFinal:N2}) de la subasta #{subasta.Id}. No se liquida; subasta no queda FINALIZADA para reintento.",
                            ganadorId
                        );

                        throw new DominioException($"Saldo retenido insuficiente (${saldoActual:N2}) del comprador #{ganadorId} para cubrir el precio final de ${precioFinal:N2}.");
                    }

                    // Transición de estado en entidad de dominio
                    subasta.Finalizar(ganadorId, precioFinal, ahoraUtc);

                    // Liquidación financiera formal a través de métodos de dominio
                    billeteraComprador.DebitarRetencion(precioFinal);
                    _billeteraRepository.Update(billeteraComprador);

                    if (billeteraVendedor != null)
                    {
                        billeteraVendedor.AcreditarCobro(precioFinal);
                        _billeteraRepository.Update(billeteraVendedor);
                    }

                    // Asientos contables de liquidación (PAGO y COBRO exactamente una vez)
                    var ledgersSubasta = (await _ledgerRepository.BuscarAsync(t => t.SubastaId == subasta.Id)).ToList();
                    var yaLiquidada = ledgersSubasta.Any(t => t.Tipo == TipoTransaccion.PAGO);

                    if (!yaLiquidada)
                    {
                        await _ledgerRepository.AddAsync(new TransactionLedger
                        {
                            BilleteraId = billeteraComprador.Id,
                            Tipo = TipoTransaccion.PAGO,
                            Monto = precioFinal,
                            Fecha = ahoraUtc,
                            SubastaId = subasta.Id
                        });

                        if (billeteraVendedor != null)
                        {
                            await _ledgerRepository.AddAsync(new TransactionLedger
                            {
                                BilleteraId = billeteraVendedor.Id,
                                Tipo = TipoTransaccion.COBRO,
                                Monto = precioFinal,
                                Fecha = ahoraUtc,
                                SubastaId = subasta.Id
                            });
                        }
                    }

                    // 5. Liberación de garantías para postores perdedores (RETENCION - LIBERACION - PAGO)
                    var todasBilleterasIds = ledgersSubasta
                        .Select(l => l.BilleteraId)
                        .Distinct()
                        .ToList();

                    foreach (var bId in todasBilleterasIds)
                    {
                        var retenciones = ledgersSubasta.Where(l => l.BilleteraId == bId && l.Tipo == TipoTransaccion.RETENCION).Sum(l => l.Monto);
                        var liberaciones = ledgersSubasta.Where(l => l.BilleteraId == bId && l.Tipo == TipoTransaccion.LIBERACION).Sum(l => l.Monto);
                        var pagos = ledgersSubasta.Where(l => l.BilleteraId == bId && l.Tipo == TipoTransaccion.PAGO).Sum(l => l.Monto);

                        // Si es el comprador, consideramos el pago que se acaba de registrar
                        if (bId == billeteraComprador.Id && !yaLiquidada)
                        {
                            pagos += precioFinal;
                        }

                        var saldoPendienteLiberar = retenciones - liberaciones - pagos;

                        if (saldoPendienteLiberar > 0)
                        {
                            var billeteraPostor = (bId == billeteraComprador.Id)
                                ? billeteraComprador
                                : await _billeteraRepository.GetByIdAsync(bId);

                            if (billeteraPostor != null)
                            {
                                var montoALiberar = Math.Min(billeteraPostor.SaldoRetenido, saldoPendienteLiberar);
                                if (montoALiberar > 0)
                                {
                                    billeteraPostor.LiberarSaldo(montoALiberar);
                                    _billeteraRepository.Update(billeteraPostor);

                                    await _ledgerRepository.AddAsync(new TransactionLedger
                                    {
                                        BilleteraId = billeteraPostor.Id,
                                        Tipo = TipoTransaccion.LIBERACION,
                                        Monto = montoALiberar,
                                        Fecha = ahoraUtc,
                                        SubastaId = subasta.Id
                                    });
                                }
                            }
                        }
                    }

                    // 6. Auditoría de CAMBIO_ESTADO_SUBASTA y VENTA_REGISTRADA dentro de la misma transacción
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

                    await _subastaRepository.ActualizarAsync(subasta);
                    await _unitOfWork.CommitTransactionAsync(cancellationToken);

                    _logger.LogInformation("Subasta #{SubastaId} finalizada exitosamente para Ganador #{GanadorId} por ${Monto:N2}.", subasta.Id, ganadorId, precioFinal);

                    return new CerrarSubastaResultadoDto
                    {
                        SubastaId = subasta.Id,
                        Estado = "FINALIZADA",
                        GanadorId = ganadorId,
                        PrecioFinal = precioFinal,
                        Exitoso = true,
                        Mensaje = "Subasta adjudicada y liquidada formalmente."
                    };
                }
                else
                {
                    // Sin ofertas: se declara DESIERTA en la entidad de dominio
                    subasta.DeclararDesierta(ahoraUtc);

                    // Liberación de cualquier retención residual si existiera en los asientos contables
                    var ledgersSubasta = (await _ledgerRepository.BuscarAsync(t => t.SubastaId == subasta.Id)).ToList();
                    var billeterasIds = ledgersSubasta.Select(l => l.BilleteraId).Distinct().ToList();

                    foreach (var bId in billeterasIds)
                    {
                        var retenciones = ledgersSubasta.Where(l => l.BilleteraId == bId && l.Tipo == TipoTransaccion.RETENCION).Sum(l => l.Monto);
                        var liberaciones = ledgersSubasta.Where(l => l.BilleteraId == bId && l.Tipo == TipoTransaccion.LIBERACION).Sum(l => l.Monto);
                        var pagos = ledgersSubasta.Where(l => l.BilleteraId == bId && l.Tipo == TipoTransaccion.PAGO).Sum(l => l.Monto);
                        var saldoPendiente = retenciones - liberaciones - pagos;

                        if (saldoPendiente > 0)
                        {
                            var b = await _billeteraRepository.GetByIdAsync(bId);
                            if (b != null)
                            {
                                var montoLiberar = Math.Min(b.SaldoRetenido, saldoPendiente);
                                if (montoLiberar > 0)
                                {
                                    b.LiberarSaldo(montoLiberar);
                                    _billeteraRepository.Update(b);

                                    await _ledgerRepository.AddAsync(new TransactionLedger
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

                    // Auditoría dentro de la misma transacción
                    await _auditoriaService.RegistrarAsync(
                        "CAMBIO_ESTADO_SUBASTA",
                        $"Subasta #{subasta.Id} ('{subasta.Titulo}') pasó de {estadoAnterior} a DESIERTA al vencer el período sin ofertas registradas.",
                        null
                    );

                    await _subastaRepository.ActualizarAsync(subasta);
                    await _unitOfWork.CommitTransactionAsync(cancellationToken);

                    _logger.LogInformation("Subasta #{SubastaId} declarada DESIERTA (sin ofertas).", subasta.Id);

                    return new CerrarSubastaResultadoDto
                    {
                        SubastaId = subasta.Id,
                        Estado = "DESIERTA",
                        GanadorId = null,
                        PrecioFinal = null,
                        Exitoso = true,
                        Mensaje = "Subasta declarada desierta."
                    };
                }
            }
            catch (Exception ex)
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                _unitOfWork.DescartarCambiosPendientes();

                _logger.LogError(ex, "Error al procesar el cierre transaccional de la subasta #{SubastaId}.", command.SubastaId);
                await _auditoriaService.RegistrarFalloAsync(
                    "FALLO_CIERRE_SUBASTA",
                    $"Error en el cierre transaccional de Subasta #{command.SubastaId}: {ex.Message}",
                    null
                );
                throw;
            }
        }
    }
}
