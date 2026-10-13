using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PROYECTO_SUBASTA.Application.DTOs;
using PROYECTO_SUBASTA.Application.Repositories;
using PROYECTO_SUBASTA.Domain.Entities;

namespace PROYECTO_SUBASTA.Application.Queries.Usuarios
{
    public class ObtenerActividadesUsuarioQuery
    {
        public int UsuarioId { get; set; }
    }

    public class ObtenerActividadesUsuarioQueryHandler
    {
        private readonly IRepository<Usuario> _usuarioRepository;
        private readonly ISubastaRepository _subastaRepository;

        public ObtenerActividadesUsuarioQueryHandler(
            IRepository<Usuario> usuarioRepository,
            ISubastaRepository subastaRepository)
        {
            _usuarioRepository = usuarioRepository ?? throw new ArgumentNullException(nameof(usuarioRepository));
            _subastaRepository = subastaRepository ?? throw new ArgumentNullException(nameof(subastaRepository));
        }

        public async Task<UsuarioActividadesResponseDto?> HandleAsync(ObtenerActividadesUsuarioQuery query, CancellationToken cancellationToken = default)
        {
            if (query == null || query.UsuarioId <= 0)
                return null;

            var usuario = await _usuarioRepository.GetByIdAsync(query.UsuarioId);
            if (usuario == null)
                return null;

            // 1. Participaciones (subastas donde el usuario pujó)
            var subastasParticipadas = await _subastaRepository.ObtenerPorUsuarioOfertanteAsync(query.UsuarioId, cancellationToken);
            var participaciones = new List<ParticipacionSubastaDto>();
            int totalPujasUsuario = 0;
            int subastasGanadas = 0;
            int subastasEnLiderazgo = 0;

            foreach (var sub in subastasParticipadas)
            {
                var misPujas = sub.Pujas?.Where(p => p.UsuarioId == query.UsuarioId).ToList() ?? new();
                if (misPujas.Count == 0) continue;

                totalPujasUsuario += misPujas.Count;
                var miMayorOferta = misPujas.Max(p => p.Monto);
                var ofertaLiderActual = (sub.Pujas != null && sub.Pujas.Count > 0) ? sub.Pujas.Max(p => p.Monto) : sub.PrecioBase;
                var esLider = (miMayorOferta == ofertaLiderActual);
                var haFinalizado = (sub.Estado == "FINALIZADA" || sub.Estado == "DESIERTA");

                string estadoParticipacion;
                if (haFinalizado)
                {
                    if (sub.GanadorId == query.UsuarioId || (esLider && sub.Estado == "FINALIZADA"))
                    {
                        estadoParticipacion = "GANADA";
                        subastasGanadas++;
                    }
                    else
                    {
                        estadoParticipacion = "PERDIDA";
                    }
                }
                else
                {
                    if (esLider)
                    {
                        estadoParticipacion = "LIDER";
                        subastasEnLiderazgo++;
                    }
                    else
                    {
                        estadoParticipacion = "SUPERADO";
                    }
                }

                participaciones.Add(new ParticipacionSubastaDto
                {
                    SubastaId = sub.Id,
                    Titulo = sub.Titulo,
                    UrlImagen = sub.UrlImagen,
                    CategoriaNombre = sub.Categoria?.Nombre ?? string.Empty,
                    MiMayorOferta = miMayorOferta,
                    OfertaLiderActual = ofertaLiderActual,
                    CantidadOfertasUsuario = misPujas.Count,
                    TotalOfertasSubasta = sub.Pujas?.Count ?? 0,
                    EstadoParticipacion = estadoParticipacion,
                    EstadoSubasta = sub.Estado,
                    FechaFin = sub.FechaFin
                });
            }

            // 2. Publicaciones (subastas donde el usuario es vendedor)
            var subastasPublicadas = await _subastaRepository.ObtenerPorVendedorAsync(query.UsuarioId, cancellationToken);
            var publicaciones = new List<PublicacionSubastaDto>();

            foreach (var sub in subastasPublicadas)
            {
                var mejorOferta = (sub.Pujas != null && sub.Pujas.Count > 0) ? sub.Pujas.Max(p => p.Monto) : (decimal?)null;
                publicaciones.Add(new PublicacionSubastaDto
                {
                    SubastaId = sub.Id,
                    Titulo = sub.Titulo,
                    UrlImagen = sub.UrlImagen,
                    CategoriaNombre = sub.Categoria?.Nombre ?? string.Empty,
                    PrecioBase = sub.PrecioBase,
                    MejorOferta = mejorOferta,
                    CantidadPujas = sub.Pujas?.Count ?? 0,
                    GanadorId = sub.GanadorId,
                    GanadorNombre = sub.GanadorId.HasValue ? $"Postor #{(sub.GanadorId.Value * 33 + 100):X}" : null,
                    Estado = sub.Estado,
                    FechaFin = sub.FechaFin
                });
            }

            var metricas = new MetricasActividadDto
            {
                TotalPujasRealizadas = totalPujasUsuario,
                TotalSubastasCreadas = publicaciones.Count,
                SubastasGanadas = subastasGanadas,
                SubastasEnLiderazgo = subastasEnLiderazgo
            };

            return new UsuarioActividadesResponseDto
            {
                UsuarioId = query.UsuarioId,
                Participaciones = participaciones,
                Publicaciones = publicaciones,
                Metricas = metricas
            };
        }
    }
}
