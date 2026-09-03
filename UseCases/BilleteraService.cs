using System;
using System.Threading.Tasks;
using PROYECTO_SUBASTA.Entities;
using PROYECTO_SUBASTA.Repositories;

namespace PROYECTO_SUBASTA.UseCases
{
    public class BilleteraService : IBilleteraService
    {
        private readonly IBilleteraRepository _billeteraRepository;
        private readonly IRepository<TransactionLedger> _ledgerRepository;

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

            // Actualizar saldos
            billetera.SaldoTotal += monto;
            billetera.SaldoDisponible += monto;
            _billeteraRepository.Update(billetera);

            // Registrar movimiento en el Ledger
            var transaccion = new TransactionLedger
            {
                BilleteraId = billetera.Id,
                Monto = monto,
                Tipo = TipoTransaccion.DEPOSITO, // 1: Depósito / Carga de saldo
                Fecha = DateTime.UtcNow
            };
            await _ledgerRepository.AddAsync(transaccion);

            await _billeteraRepository.SaveChangesAsync();
            return true;
        }
    }
}
