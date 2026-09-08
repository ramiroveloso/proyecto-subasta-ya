using System.Collections.Generic;
using System.Threading.Tasks;
using PROYECTO_SUBASTA.Entities;

namespace PROYECTO_SUBASTA.UseCases
{
    // Establece el contrato de aplicación para orquestar la gestión financiera y garantizar la consistencia transaccional.
    public interface IBilleteraService
    {
        Task<Billetera?> ObtenerBilleteraPorUsuarioAsync(int usuarioId);
        Task<bool> CargarSaldoAsync(int usuarioId, decimal monto);

        // Firma para congelar fondos temporalmente cuando un usuario realiza una oferta válida en una subasta.
        Task<bool> RetenerSaldoAsync(int usuarioId, decimal monto, int subastaId);

        // Firma para desbloquear y retornar fondos al saldo disponible cuando la oferta de un usuario es superada.
        Task<bool> LiberarSaldoAsync(int usuarioId, decimal monto, int subastaId);

        // Firma para recuperar el libro diario contable (audit trail) de un usuario específico.
        Task<IEnumerable<TransactionLedger>> ObtenerMovimientosAsync(int usuarioId);
    }
}