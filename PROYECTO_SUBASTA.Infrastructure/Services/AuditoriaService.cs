using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PROYECTO_SUBASTA.Application.UseCases;
using PROYECTO_SUBASTA.Domain.Entities;
using PROYECTO_SUBASTA.Infrastructure.Data;

namespace PROYECTO_SUBASTA.Infrastructure.Services
{
    public class AuditoriaService : IAuditoriaService
    {
        private readonly SubastaDbContext _context;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<AuditoriaService> _logger;

        public AuditoriaService(
            SubastaDbContext context,
            IServiceScopeFactory scopeFactory,
            ILogger<AuditoriaService> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Registra un evento de auditoría dentro del contexto de trabajo activo.
        /// Si hay una transacción activa, se asienta en el ChangeTracker y se confirma únicamente
        /// con el Commit de la Unidad de Trabajo. Si la transacción hace rollback, el log se descarta.
        /// </summary>
        public async Task RegistrarAsync(string accion, string detalle, int? usuarioId = null)
        {
            try
            {
                var log = new LogAuditoria
                {
                    Accion = accion,
                    Detalle = detalle,
                    UsuarioId = usuarioId,
                    FechaRegistro = DateTime.UtcNow
                };

                _context.LogsAuditoria.Add(log);

                // Si NO hay una transacción activa, se persiste inmediatamente.
                // Si HAY una transacción activa, se guardará y confirmará junto con la transacción principal.
                if (_context.Database.CurrentTransaction == null)
                {
                    await _context.SaveChangesAsync();
                }

                _logger.LogInformation("[AUDITORIA-TRANSACCIONAL] {Accion} (UsuarioId: {UsuarioId}): {Detalle}", accion, usuarioId, detalle);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al registrar evento de auditoría transaccional: {Accion}", accion);
            }
        }

        /// <summary>
        /// Registra un evento de auditoría en un scope aislado independiente de la transacción en curso.
        /// Uso exclusivo para fallos de seguridad, rechazos por concurrencia o intentos no autorizados.
        /// </summary>
        public async Task RegistrarFalloAsync(string accion, string detalle, int? usuarioId = null)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var isolatedContext = scope.ServiceProvider.GetRequiredService<SubastaDbContext>();

                var log = new LogAuditoria
                {
                    Accion = accion,
                    Detalle = detalle,
                    UsuarioId = usuarioId,
                    FechaRegistro = DateTime.UtcNow
                };

                isolatedContext.LogsAuditoria.Add(log);
                await isolatedContext.SaveChangesAsync();
                _logger.LogWarning("[AUDITORIA-FALLO/AISLADO] {Accion} (UsuarioId: {UsuarioId}): {Detalle}", accion, usuarioId, detalle);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al registrar evento de auditoría aislado/fallo: {Accion}", accion);
            }
        }

        public async Task<IEnumerable<LogAuditoria>> ObtenerLogsAsync(int limite = 100)
        {
            return await _context.LogsAuditoria
                .AsNoTracking()
                .OrderByDescending(l => l.FechaRegistro)
                .Take(limite)
                .ToListAsync();
        }
    }
}
