using System.Collections.Generic;
using System.Threading.Tasks;
using PROYECTO_SUBASTA.Domain.Entities;

namespace PROYECTO_SUBASTA.Application.UseCases
{
    public interface IAuditoriaService
    {
        /// <summary>
        /// Registra un evento de auditoría dentro del contexto transaccional activo.
        /// Si hay una transacción en curso en la unidad de trabajo, el log se confirma
        /// únicamente cuando la transacción de negocio se comitea exitosamente.
        /// </summary>
        Task RegistrarAsync(string accion, string detalle, int? usuarioId = null);

        /// <summary>
        /// Registra un evento de auditoría en un scope aislado e independiente de la transacción actual.
        /// Diseñado exclusivamente para registrar fallos de seguridad o rechazos por concurrencia.
        /// </summary>
        Task RegistrarFalloAsync(string accion, string detalle, int? usuarioId = null);

        Task<IEnumerable<LogAuditoria>> ObtenerLogsAsync(int limite = 100);
    }
}
