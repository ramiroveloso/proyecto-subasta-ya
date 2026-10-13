using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PROYECTO_SUBASTA.Application.DTOs;
using PROYECTO_SUBASTA.Application.UseCases;

namespace PROYECTO_SUBASTA.Application.Queries.Auditorias
{
    public class ObtenerLogsAuditoriaQuery
    {
        public int Limite { get; set; } = 100;
    }

    public class ObtenerLogsAuditoriaQueryHandler
    {
        private readonly IAuditoriaService _auditoriaService;

        public ObtenerLogsAuditoriaQueryHandler(IAuditoriaService auditoriaService)
        {
            _auditoriaService = auditoriaService ?? throw new ArgumentNullException(nameof(auditoriaService));
        }

        public async Task<IEnumerable<LogAuditoriaDto>> HandleAsync(ObtenerLogsAuditoriaQuery query, CancellationToken cancellationToken = default)
        {
            var limite = (query != null && query.Limite > 0) ? query.Limite : 100;
            var logs = await _auditoriaService.ObtenerLogsAsync(limite);

            return logs.Select(l => new LogAuditoriaDto
            {
                Id = l.Id,
                UsuarioId = l.UsuarioId,
                Accion = l.Accion,
                Detalle = l.Detalle,
                FechaRegistro = l.FechaRegistro
            }).ToList();
        }
    }
}
