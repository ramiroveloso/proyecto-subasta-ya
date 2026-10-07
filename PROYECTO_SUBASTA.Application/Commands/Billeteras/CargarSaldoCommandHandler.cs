using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PROYECTO_SUBASTA.Application.Exceptions;
using PROYECTO_SUBASTA.Application.Repositories;
using PROYECTO_SUBASTA.Application.UseCases;
using PROYECTO_SUBASTA.Domain.Entities;

namespace PROYECTO_SUBASTA.Application.Commands.Billeteras
{
    public class CargarSaldoCommand
    {
        public int UsuarioId { get; set; }
        public decimal Monto { get; set; }
    }

    public class CargarSaldoCommandHandler
    {
        private readonly IBilleteraRepository _billeteraRepository;
        private readonly IRepository<TransactionLedger> _ledgerRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IAuditoriaService _auditoriaService;

        public CargarSaldoCommandHandler(
            IBilleteraRepository billeteraRepository,
            IRepository<TransactionLedger> ledgerRepository,
            IUnitOfWork unitOfWork,
            IAuditoriaService auditoriaService)
        {
            _billeteraRepository = billeteraRepository ?? throw new ArgumentNullException(nameof(billeteraRepository));
            _ledgerRepository = ledgerRepository ?? throw new ArgumentNullException(nameof(ledgerRepository));
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _auditoriaService = auditoriaService ?? throw new ArgumentNullException(nameof(auditoriaService));
        }

        public async Task<bool> HandleAsync(CargarSaldoCommand command, CancellationToken cancellationToken = default)
        {
            if (command == null || command.Monto <= 0)
                throw new ReglaNegocioException("El monto a cargar debe ser mayor a cero.");

            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            try
            {
                var billetera = await _billeteraRepository.ObtenerPorUsuarioIdAsync(command.UsuarioId);
                if (billetera == null)
                    throw new KeyNotFoundException($"No se encontró la billetera para el usuario #{command.UsuarioId}.");

                billetera.CargarSaldo(command.Monto);
                _billeteraRepository.Update(billetera);

                var transaccion = new TransactionLedger
                {
                    BilleteraId = billetera.Id,
                    Monto = command.Monto,
                    Tipo = TipoTransaccion.DEPOSITO,
                    Fecha = DateTime.UtcNow
                };
                await _ledgerRepository.AddAsync(transaccion);

                await _auditoriaService.RegistrarAsync(
                    "ACREDITACION_MANUAL_SALDO",
                    $"Acreditación de saldo de ${command.Monto:N2} en la billetera de Usuario #{command.UsuarioId}. Saldo resultante: ${billetera.SaldoTotal:N2}.",
                    command.UsuarioId
                );

                await _unitOfWork.CommitTransactionAsync(cancellationToken);
                return true;
            }
            catch
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                throw;
            }
        }
    }
}
