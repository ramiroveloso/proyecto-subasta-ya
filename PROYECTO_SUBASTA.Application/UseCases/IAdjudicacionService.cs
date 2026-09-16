using System.Threading;
using System.Threading.Tasks;

namespace PROYECTO_SUBASTA.Application.UseCases
{
    public class AdjudicacionResultado
    {
        public int Finalizadas { get; set; }
        public int Desiertas { get; set; }
        public int Activadas { get; set; }
    }

    public interface IAdjudicacionService
    {
        Task<AdjudicacionResultado> ProcesarSubastasVencidasAsync(CancellationToken cancellationToken = default);
    }
}
