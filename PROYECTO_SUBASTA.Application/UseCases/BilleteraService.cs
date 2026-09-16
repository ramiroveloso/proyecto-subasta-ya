using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PROYECTO_SUBASTA.Domain.Entities;
using PROYECTO_SUBASTA.Application.Repositories;

namespace PROYECTO_SUBASTA.Application.UseCases
{
    public class BilleteraService : IBilleteraService
    {
        private readonly IBilleteraRepository _billeteraRepository;
        private readonly IRepository<TransactionLedger> _ledgerRepository;
        private readonly IAuditoriaService _auditoriaService;

        public BilleteraService(
            IBilleteraRepository billeteraRepository,
            IRepository<TransactionLedger> ledgerRepository,
            IAuditoriaService auditoriaService)
        {
            _billeteraRepository = billeteraRepository;
            _ledgerRepository = ledgerRepository;
            _auditoriaService = auditoriaService;
        }

        public async Task<Billetera?> ObtenerBilleteraPorUsuarioAsync(int usuarioId)
        {
            return await _billeteraRepository.ObtenerPorUsuarioIdAsync(usuarioId);
        }

        public async Task<bool> CargarSaldoAsync(int usuarioId, decimal monto)
        {
            if (monto <= 0) return false;

            var billetera = await _billeteraRepository.ObtenerPorUsuarioIdAsync(usuarioId);
            if (billetera == null) return false;

            // Transición de estado: se incrementa la liquidez total y disponible
            billetera.SaldoTotal += monto;
            billetera.SaldoDisponible += monto;
            _billeteraRepository.Update(billetera);

            // Registro en el libro diario para trazabilidad e intangibilidad financiera
            var transaccion = new TransactionLedger
            {
                BilleteraId = billetera.Id,
                Monto = monto,
                Tipo = TipoTransaccion.DEPOSITO,
                Fecha = DateTime.UtcNow
            };
            await _ledgerRepository.AddAsync(transaccion);

            await _billeteraRepository.SaveChangesAsync();

            // Auditoría obligatoria de acreditación manual de saldo
            await _auditoriaService.RegistrarAsync(
                "ACREDITACION_MANUAL_SALDO",
                $"Acreditación manual de saldo de ${monto:N2} en la billetera de Usuario #{usuarioId}. Saldo total resultante: ${billetera.SaldoTotal:N2}.",
                usuarioId
            );

            return true;
        }

        public async Task<bool> RetenerSaldoAsync(int usuarioId, decimal monto, int subastaId)
        {
            if (monto <= 0) return false;

            var billetera = await _billeteraRepository.ObtenerPorUsuarioIdAsync(usuarioId);
            if (billetera == null) return false;

            try
            {
                // Delegamos la validación del dominio a la entidad Billetera (Rich Domain Model)
                billetera.RetenerSaldo(monto);
                _billeteraRepository.Update(billetera);

                // Registramos el evento de retención especificando el identificador de la subasta
                var transaccion = new TransactionLedger
                {
                    BilleteraId = billetera.Id,
                    Monto = monto,
                    Tipo = TipoTransaccion.RETENCION,
                    SubastaId = subastaId,
                    Fecha = DateTime.UtcNow
                };
                await _ledgerRepository.AddAsync(transaccion);

                await _billeteraRepository.SaveChangesAsync();
                return true;
            }
            catch (InvalidOperationException)
            {
                await _auditoriaService.RegistrarAsync(
                    "RETENCION_RECHAZADA",
                    $"Intento de retención de garantía de ${monto:N2} para Subasta #{subastaId} rechazado: Saldo disponible insuficiente (${billetera.SaldoDisponible:N2}) en billetera de Usuario #{usuarioId}.",
                    usuarioId
                );
                return false;
            }
        }

        public async Task<bool> LiberarSaldoAsync(int usuarioId, decimal monto, int subastaId)
        {
            if (monto <= 0) return false;

            var billetera = await _billeteraRepository.ObtenerPorUsuarioIdAsync(usuarioId);
            if (billetera == null) return false;

            // Restablecemos el saldo congelado al estado disponible dentro del agregado
            billetera.LiberarSaldo(monto);
            _billeteraRepository.Update(billetera);

            // Asentamos la contrapartida contable de liberación en el ledger
            var transaccion = new TransactionLedger
            {
                BilleteraId = billetera.Id,
                Monto = monto,
                Tipo = TipoTransaccion.LIBERACION,
                SubastaId = subastaId,
                Fecha = DateTime.UtcNow
            };
            await _ledgerRepository.AddAsync(transaccion);

            await _billeteraRepository.SaveChangesAsync();
            return true;
        }

        public async Task<IEnumerable<TransactionLedger>> ObtenerMovimientosAsync(int usuarioId)
        {
            var billetera = await _billeteraRepository.ObtenerPorUsuarioIdAsync(usuarioId);
            if (billetera == null) return Enumerable.Empty<TransactionLedger>();

            var todosLosMovimientos = await _ledgerRepository.GetAllAsync();

            // Filtramos los registros del ledger pertenecientes a la billetera y los ordenamos cronológicamente
            return todosLosMovimientos
                .Where(m => m.BilleteraId == billetera.Id)
                .OrderByDescending(m => m.Fecha);
        }
    }
}