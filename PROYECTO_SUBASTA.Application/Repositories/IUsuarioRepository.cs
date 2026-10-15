using System.Threading;
using System.Threading.Tasks;
using PROYECTO_SUBASTA.Domain.Entities;

namespace PROYECTO_SUBASTA.Application.Repositories
{
    public interface IUsuarioRepository : IRepository<Usuario>
    {
        Task<Usuario?> ObtenerPorEmailONombreAsync(string identificador, CancellationToken cancellationToken = default);
    }
}
