using System.Threading.Tasks;
using PROYECTO_SUBASTA.Entities;

namespace PROYECTO_SUBASTA.UseCases
{
    public interface IBilleteraService
    {
        Task<Billetera?> ObtenerBilleteraPorUsuarioAsync(int usuarioId);
        Task<bool> CargarSaldoAsync(int usuarioId, decimal monto);
    }
}