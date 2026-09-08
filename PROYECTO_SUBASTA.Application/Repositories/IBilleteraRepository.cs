using System.Threading.Tasks;
using PROYECTO_SUBASTA.Domain.Entities;

namespace PROYECTO_SUBASTA.Application.Repositories
{
    public interface IBilleteraRepository : IRepository<Billetera>
    {
        Task<Billetera?> ObtenerPorUsuarioIdAsync(int usuarioId);
    }
}
