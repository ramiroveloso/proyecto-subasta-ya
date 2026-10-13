using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PROYECTO_SUBASTA.Application.Commands.Subastas;
using PROYECTO_SUBASTA.Application.Configuration;
using PROYECTO_SUBASTA.Application.DTOs;
using PROYECTO_SUBASTA.Application.Exceptions;
using PROYECTO_SUBASTA.Application.Repositories;
using PROYECTO_SUBASTA.Application.UseCases;
using PROYECTO_SUBASTA.Domain.Entities;
using PROYECTO_SUBASTA.Domain.Exceptions;

namespace PROYECTO_SUBASTA.Application.Commands.Subastas
{
    public class RegistrarPujaCommandHandler
    {
        private readonly ISubastaRepository _subastaRepository;
        private readonly IBilleteraRepository _billeteraRepository;
        private readonly IRepository<TransactionLedger> _ledgerRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IAuditoriaService _auditoriaService;
        private readonly IOptions<AntiSnipingOptions>? _antiSnipingOptions;

        public RegistrarPujaCommandHandler(
            ISubastaRepository subastaRepository,
            IBilleteraRepository billeteraRepository,
            IRepository<TransactionLedger> ledgerRepository,
            IUnitOfWork unitOfWork,
            IAuditoriaService auditoriaService,
            IOptions<AntiSnipingOptions>? antiSnipingOptions = null)
        {
            _subastaRepository = subastaRepository ?? throw new ArgumentNullException(nameof(subastaRepository));
            _billeteraRepository = billeteraRepository ?? throw new ArgumentNullException(nameof(billeteraRepository));
            _ledgerRepository = ledgerRepository ?? throw new ArgumentNullException(nameof(ledgerRepository));
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _auditoriaService = auditoriaService ?? throw new ArgumentNullException(nameof(auditoriaService));
            _antiSnipingOptions = antiSnipingOptions;
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

                // 3. Validación preventiva de Concurrencia Optimista (OCC)
                // Si el cliente especificó una versión previa y difiere de la versión cargada, se aborta antes de alterar cualquier entidad.
                if (command.VersionCliente > 0 && command.VersionCliente != (int)subasta.Version)
                {
                    throw new ConcurrenciaException("El recurso fue modificado por otra operación; reintenta.");
                }

                var versionCliente = command.VersionCliente > 0 ? command.VersionCliente : (int)subasta.Version;

                // Identificar el postor líder previo (si existe)
                var liderAnterior = subasta.ObtenerPujaLider();
                bool esMismoPostor = liderAnterior != null && liderAnterior.UsuarioId == command.UsuarioId;

                // 4. Cargar la billetera del nuevo postor
                var billeteraNuevoPostor = await _billeteraRepository.ObtenerPorUsuarioIdAsync(command.UsuarioId);
                if (billeteraNuevoPostor == null)
                {
                    throw new ReglaNegocioException($"El usuario #{command.UsuarioId} no posee una billetera activa.");
                }

                // 5. Cargar la billetera del líder anterior si correspondiera liberar su retención
                Billetera? billeteraLiderAnterior = null;
                if (liderAnterior != null && !esMismoPostor)
                {
                    billeteraLiderAnterior = await _billeteraRepository.ObtenerPorUsuarioIdAsync(liderAnterior.UsuarioId);
                }

                // 6. Aplicar reglas de dominio ricas con Anti-Sniping parametrizado
                var ahoraUtc = DateTime.UtcNow;
                var umbralSegundos = _antiSnipingOptions?.Value?.UmbralSegundos ?? 60;
                var extensionSegundos = _antiSnipingOptions?.Value?.ExtensionSegundos ?? 120;

                var (nuevaPuja, antiSnipingActivado) = subasta.RegistrarPuja(
                    command.UsuarioId,
                    command.Monto,
                    ahoraUtc,
                    billeteraNuevoPostor.SaldoDisponible,
                    antiSnipingUmbralSegundos: umbralSegundos,
                    antiSnipingExtensionSegundos: extensionSegundos
                );

                // 7. Retener garantía en la billetera del postor y asentar en el libro mayor
                decimal montoARetener = (esMismoPostor && liderAnterior != null)
                    ? command.Monto - liderAnterior.Monto
                    : command.Monto;

                if (montoARetener > 0)
                {
                    billeteraNuevoPostor.RetenerSaldo(montoARetener);
                    _billeteraRepository.Update(billeteraNuevoPostor);

                    await _ledgerRepository.AddAsync(new TransactionLedger
                    {
                        BilleteraId = billeteraNuevoPostor.Id,
                        Tipo = TipoTransaccion.RETENCION,
                        Monto = montoARetener,
                        Fecha = ahoraUtc,
                        SubastaId = subasta.Id
                    });
                }

                // 8. Liberar garantía retenida del postor anterior superado
                if (billeteraLiderAnterior != null && liderAnterior != null && !esMismoPostor)
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

                // 9. Configurar token de concurrencia optimista en el repositorio (sin SaveChanges prematuro)
                await _subastaRepository.ActualizarConConcurrenciaAsync(subasta, versionCliente);

                // 10. Registrar auditoría transaccional de éxito dentro de la misma unidad
                await _auditoriaService.RegistrarAsync(
                    "PUJA_REGISTRADA",
                    $"Puja de ${command.Monto:N2} registrada con éxito por Usuario #{command.UsuarioId} en Subasta #{subasta.Id}. Garantía Escrow retenida: ${montoARetener:N2}.",
                    command.UsuarioId
                );

                // Si se activó Anti-Sniping, se asienta como evento de auditoría diferenciado
                if (antiSnipingActivado)
                {
                    await _auditoriaService.RegistrarAsync(
                        "EXTENSION_ANTI_SNIPING",
                        $"Regla Anti-Sniping activada en Subasta #{subasta.Id}. Fecha de fin extendida en {extensionSegundos}s hasta {subasta.FechaFin:yyyy-MM-dd HH:mm:ss} UTC.",
                        command.UsuarioId
                    );
                }

                // 11. Confirmación atómica única de toda la unidad de negocio
                await _unitOfWork.CommitTransactionAsync(cancellationToken);

                return new RegistrarPujaResponseDto
                {
                    Mensaje = antiSnipingActivado
                        ? $"Puja registrada con éxito. ¡Regla Anti-Sniping activada (+{extensionSegundos}s)!"
                        : "Puja registrada con éxito y saldo retenido en Escrow.",
                    Version = subasta.Version,
                    FechaFin = subasta.FechaFin,
                    AntiSnipingActivado = antiSnipingActivado,
                    LiderId = command.UsuarioId,
                    Lider = $"Postor #{(command.UsuarioId * 33 + 100):X}",
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
            catch (Exception ex)
            {
                // Reversión total transaccional ante cualquier fallo
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);

                // Clasificación de motivo para auditoría en scope aislado
                string motivo = ex switch
                {
                    DominioException domEx when domEx.Message.Contains("saldo", StringComparison.OrdinalIgnoreCase) || domEx.Message.Contains("Fondos", StringComparison.OrdinalIgnoreCase) => "SALDO_INSUFICIENTE",
                    DominioException domEx when domEx.Message.Contains("incremento", StringComparison.OrdinalIgnoreCase) || domEx.Message.Contains("monto", StringComparison.OrdinalIgnoreCase) => "INCREMENTO_INVALIDO",
                    DominioException domEx when domEx.Message.Contains("finalizado", StringComparison.OrdinalIgnoreCase) || domEx.Message.Contains("vencida", StringComparison.OrdinalIgnoreCase) => "SUBASTA_VENCIDA",
                    DominioException domEx when domEx.Message.Contains("activa", StringComparison.OrdinalIgnoreCase) => "SUBASTA_NO_ACTIVA",
                    DominioException domEx when domEx.Message.Contains("vendedor", StringComparison.OrdinalIgnoreCase) => "VENDEDOR_NO_PUEDE_PUJAR",
                    ConcurrenciaException or DbUpdateConcurrencyException => "CONCURRENCIA",
                    _ => "ERROR_VALIDACION"
                };

                try
                {
                    await _auditoriaService.RegistrarFalloAsync(
                        "PUJA_RECHAZADA",
                        $"Puja rechazada en Subasta #{command.SubastaId} para Usuario #{command.UsuarioId}. Motivo: {motivo}. Detalle: {ex.Message}",
                        command.UsuarioId
                    );
                }
                catch
                {
                    // La auditoría defensiva no oculta la excepción primaria de negocio
                }

                throw;
            }
        }
    }
}
