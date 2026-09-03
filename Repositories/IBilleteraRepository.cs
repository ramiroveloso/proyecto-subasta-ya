using PROYECTO_SUBASTA.Entities;
using System.Threading.Tasks;

namespace PROYECTO_SUBASTA.Repositories
{
    public interface IBilleteraRepository : IRepository<Billetera>
    {
        Task<Billetera?> ObtenerPorUsuarioIdAsync(int usuarioId);
    }
}
