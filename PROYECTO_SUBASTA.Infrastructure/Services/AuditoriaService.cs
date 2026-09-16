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
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<AuditoriaService> _logger;

        public AuditoriaService(IServiceScopeFactory scopeFactory, ILogger<AuditoriaService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        public async Task RegistrarAsync(string accion, string detalle, int? usuarioId = null)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<SubastaDbContext>();

                var log = new LogAuditoria
                {
                    Accion = accion,
                    Detalle = detalle,
                    UsuarioId = usuarioId,
                    FechaRegistro = DateTime.UtcNow
                };

                context.LogsAuditoria.Add(log);
                await context.SaveChangesAsync();
                _logger.LogInformation("[AUDITORIA] {Accion} (UsuarioId: {UsuarioId}): {Detalle}", accion, usuarioId, detalle);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al registrar evento de auditoría: {Accion}", accion);
            }
        }

        public async Task<IEnumerable<LogAuditoria>> ObtenerLogsAsync(int limite = 100)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<SubastaDbContext>();

            return await context.LogsAuditoria
                .AsNoTracking()
                .OrderByDescending(l => l.FechaRegistro)
                .Take(limite)
                .ToListAsync();
        }
    }
}
