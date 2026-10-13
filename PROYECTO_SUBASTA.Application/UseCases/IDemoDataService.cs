using System.Threading;
using System.Threading.Tasks;

namespace PROYECTO_SUBASTA.Application.UseCases
{
    public interface IDemoDataService
    {
        Task ReubicarFechasDemoSiCorrespondeAsync(CancellationToken cancellationToken = default);
    }
}
