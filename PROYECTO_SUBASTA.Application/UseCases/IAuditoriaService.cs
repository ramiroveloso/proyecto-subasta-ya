using System.Collections.Generic;
using System.Threading.Tasks;
using PROYECTO_SUBASTA.Domain.Entities;

namespace PROYECTO_SUBASTA.Application.UseCases
{
    public interface IAuditoriaService
    {
        Task RegistrarAsync(string accion, string detalle, int? usuarioId = null);
        Task<IEnumerable<LogAuditoria>> ObtenerLogsAsync(int limite = 100);
    }
}
