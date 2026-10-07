using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PROYECTO_SUBASTA.Application.DTOs;
using PROYECTO_SUBASTA.Application.Exceptions;
using PROYECTO_SUBASTA.Application.Repositories;
using PROYECTO_SUBASTA.Application.UseCases;
using PROYECTO_SUBASTA.Domain.Entities;

namespace PROYECTO_SUBASTA.Application.Commands.Subastas
{
    public class RegistrarPujaCommandHandler
    {
        private readonly ISubastaRepository _subastaRepository;
        private readonly IBilleteraRepository _billeteraRepository;
        private readonly IRepository<TransactionLedger> _ledgerRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IAuditoriaService _auditoriaService;

        public RegistrarPujaCommandHandler(
            ISubastaRepository subastaRepository,
            IBilleteraRepository billeteraRepository,
            IRepository<TransactionLedger> ledgerRepository,
            IUnitOfWork unitOfWork,
            IAuditoriaService auditoriaService)
        {
            _subastaRepository = subastaRepository ?? throw new ArgumentNullException(nameof(subastaRepository));
            _billeteraRepository = billeteraRepository ?? throw new ArgumentNullException(nameof(billeteraRepository));
            _ledgerRepository = ledgerRepository ?? throw new ArgumentNullException(nameof(ledgerRepository));
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _auditoriaService = auditoriaService ?? throw new ArgumentNullException(nameof(auditoriaService));
        }

        public async Task<RegistrarPujaResponseDto> HandleAsync(RegistrarPujaCommand command, CancellationToken cancellationToken = default)
        {
            if (command == null)
                throw new ArgumentNullException(nameof(command));

            // 1. Iniciar la transacción atómica gestionada por Unit of Work
            await _unitOfWork.BeginTransactionAsync(cancellationToken);

            try
            {
                // 2. Cargar la subasta con su historial de pujas
                var subasta = await _subastaRepository.ObtenerPorIdAsync(command.SubastaId);
                if (subasta == null)
                {
                    throw new KeyNotFoundException($"No se encontró la subasta con ID {command.SubastaId}.");
                }

                // Exigir versión positiva para garantizar OCC
                if (command.VersionCliente <= 0)
                {
                    throw new ReglaNegocioException("El número de versión de la subasta es obligatorio para garantizar el control de concurrencia optimista.");
                }

                // Identificar el postor líder previo (si existe)
                var liderAnterior = subasta.ObtenerPujaLider();

                // 3. Cargar la billetera del nuevo postor
                var billeteraNuevoPostor = await _billeteraRepository.ObtenerPorUsuarioIdAsync(command.UsuarioId);
                if (billeteraNuevoPostor == null)
                {
                    throw new ReglaNegocioException($"El usuario #{command.UsuarioId} no posee una billetera activa.");
                }

                // 4. Si el postor líder anterior era otro usuario, cargar su billetera para liberar la garantía previa
                Billetera? billeteraLiderAnterior = null;
                if (liderAnterior != null && liderAnterior.UsuarioId != command.UsuarioId)
                {
                    billeteraLiderAnterior = await _billeteraRepository.ObtenerPorUsuarioIdAsync(liderAnterior.UsuarioId);
                }

                // 5. Aplicar reglas de negocio enriquecidas en la entidad de dominio
                var ahoraUtc = DateTime.UtcNow;
                var (nuevaPuja, antiSnipingActivado) = subasta.RegistrarPuja(
                    command.UsuarioId,
                    command.Monto,
                    ahoraUtc,
                    billeteraNuevoPostor.SaldoDisponible
                );

                // 6. Retener garantía en la billetera del nuevo postor y asentar en el libro mayor
                billeteraNuevoPostor.RetenerSaldo(command.Monto);
                _billeteraRepository.Update(billeteraNuevoPostor);

                await _ledgerRepository.AddAsync(new TransactionLedger
                {
                    BilleteraId = billeteraNuevoPostor.Id,
                    Tipo = TipoTransaccion.RETENCION,
                    Monto = command.Monto,
                    Fecha = ahoraUtc,
                    SubastaId = subasta.Id
                });

                // 7. Liberar garantía retenida del postor anterior superado
                if (billeteraLiderAnterior != null && liderAnterior != null)
                {
                    billeteraLiderAnterior.LiberarSaldo(liderAnterior.Monto);
                    _billeteraRepository.Update(billeteraLiderAnterior);

                    await _ledgerRepository.AddAsync(new TransactionLedger
                    {
                        BilleteraId = billeteraLiderAnterior.Id,
                        Tipo = TipoTransaccion.LIBERACION,
                        Monto = liderAnterior.Monto,
                        Fecha = ahoraUtc,
                        SubastaId = subasta.Id
                    });
                }

                // 8. Actualizar la subasta con Concurrencia Optimista (OCC)
                await _subastaRepository.ActualizarConConcurrenciaAsync(subasta, command.VersionCliente);

                // 9. Registrar evento de auditoría dentro de la misma transacción
                await _auditoriaService.RegistrarAsync(
                    "PUJA_REGISTRADA",
                    $"Puja de ${command.Monto:N2} registrada con éxito por Usuario #{command.UsuarioId} en Subasta #{subasta.Id}. Garantía Escrow retenida: ${command.Monto:N2}. Anti-Sniping: {(antiSnipingActivado ? "ACTIVADO (+60s)" : "NO")}.",
                    command.UsuarioId
                );

                // 10. Confirmación atómica de todos los cambios
                await _unitOfWork.CommitTransactionAsync(cancellationToken);

                return new RegistrarPujaResponseDto
                {
                    Mensaje = antiSnipingActivado
                        ? "Puja registrada con éxito. ¡Regla Anti-Sniping activada (+60s)!"
                        : "Puja registrada con éxito y saldo retenido en Escrow.",
                    Version = subasta.Version,
                    FechaFin = subasta.FechaFin,
                    AntiSnipingActivado = antiSnipingActivado,
                    Puja = new PujaItemDto
                    {
                        Id = nuevaPuja.Id,
                        SubastaId = nuevaPuja.SubastaId,
                        UsuarioId = nuevaPuja.UsuarioId,
                        Monto = nuevaPuja.Monto,
                        FechaCreacion = nuevaPuja.FechaCreacion,
                        PostorAnonimo = $"Postor #{(nuevaPuja.UsuarioId * 33 + 100):X}"
                    }
                };
            }
            catch
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                throw;
            }
        }
    }
}
