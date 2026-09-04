using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PROYECTO_SUBASTA.Entities;
using PROYECTO_SUBASTA.Repositories;

namespace PROYECTO_SUBASTA.UseCases
{
    public class BilleteraService : IBilleteraService
    {
        private readonly IBilleteraRepository _billeteraRepository;
        private readonly IRepository<TransactionLedger> _ledgerRepository;

        // Inyectamos las abstracciones de persistencia promoviendo el desacoplamiento de capas (Inversion of Control).
        public BilleteraService(
            IBilleteraRepository billeteraRepository,
            IRepository<TransactionLedger> ledgerRepository)
        {
            _billeteraRepository = billeteraRepository;
            _ledgerRepository = ledgerRepository;
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
                // Captura el fallo si el saldo disponible es insuficiente
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

            // Asentamos la contrapartida contable de liberación en la auditoría
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