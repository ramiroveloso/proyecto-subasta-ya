using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PROYECTO_SUBASTA.Application.DTOs;
using PROYECTO_SUBASTA.Application.Exceptions;
using PROYECTO_SUBASTA.Application.Repositories;
using PROYECTO_SUBASTA.Domain.Constants;
using PROYECTO_SUBASTA.Domain.Entities;

namespace PROYECTO_SUBASTA.Application.Queries.Usuarios
{
    public class ObtenerActividadesUsuarioQueryHandler
    {
        private readonly IRepository<Usuario> _usuarioRepository;
        private readonly ISubastaRepository _subastaRepository;

        public ObtenerActividadesUsuarioQueryHandler(
            IRepository<Usuario> usuarioRepository,
            ISubastaRepository subastaRepository)
        {
            _usuarioRepository = usuarioRepository;
            _subastaRepository = subastaRepository;
        }

        public async Task<ActividadesUsuarioDto> HandleAsync(ObtenerActividadesUsuarioQuery query, CancellationToken cancellationToken = default)
        {
            if (query == null || query.UsuarioId <= 0)
                throw new ReglaNegocioException("El identificador de usuario debe ser mayor a cero.");

            var usuario = await _usuarioRepository.GetByIdAsync(query.UsuarioId);
            if (usuario == null)
            {
                throw new RecursoNoEncontradoException($"No se encontró el usuario con ID {query.UsuarioId}.");
            }

            var subastasParticipante = await _subastaRepository.ObtenerPorParticipanteAsync(query.UsuarioId, cancellationToken);
            var subastasVendedor = await _subastaRepository.ObtenerPorVendedorAsync(query.UsuarioId, cancellationToken);

            var participaciones = subastasParticipante.Select(sub =>
            {
                var pujasUsuario = sub.Pujas?.Where(p => p.UsuarioId == query.UsuarioId).ToList() ?? new List<Puja>();
                var miMejorOferta = pujasUsuario.Any() ? pujasUsuario.Max(p => p.Monto) : 0m;
                var ofertaLiderActual = (sub.Pujas != null && sub.Pujas.Any()) ? sub.Pujas.Max(p => p.Monto) : sub.PrecioBase;
                var esLider = sub.Pujas != null && sub.Pujas.Any() && miMejorOferta == ofertaLiderActual;

                return new ParticipacionSubastaDto
                {
                    SubastaId = sub.Id,
                    Titulo = sub.Titulo,
                    UrlImagen = sub.UrlImagen,
                    CategoriaNombre = sub.Categoria?.Nombre ?? string.Empty,
                    Estado = sub.Estado,
                    FechaFin = sub.FechaFin,
                    MiMejorOferta = miMejorOferta,
                    OfertaLiderActual = ofertaLiderActual,
                    EsLider = esLider,
                    GanadorId = sub.GanadorId,
                    PrecioFinal = sub.PrecioFinal,
                    CantidadMisPujas = pujasUsuario.Count,
                    CantidadPujasTotal = sub.Pujas?.Count ?? 0
                };
            }).ToList();

            var publicaciones = subastasVendedor.Select(sub =>
            {
                var ofertaLiderActual = (sub.Pujas != null && sub.Pujas.Any()) ? sub.Pujas.Max(p => p.Monto) : sub.PrecioBase;

                return new PublicacionSubastaDto
                {
                    SubastaId = sub.Id,
                    Titulo = sub.Titulo,
                    UrlImagen = sub.UrlImagen,
                    CategoriaNombre = sub.Categoria?.Nombre ?? string.Empty,
                    Estado = sub.Estado,
                    PrecioBase = sub.PrecioBase,
                    OfertaLiderActual = ofertaLiderActual,
                    CantidadPujas = sub.Pujas?.Count ?? 0,
                    GanadorId = sub.GanadorId,
                    PrecioFinal = sub.PrecioFinal,
                    FechaFin = sub.FechaFin
                };
            }).ToList();

            var metricas = new MetricasActividadDto
            {
                TotalPujas = participaciones.Sum(p => p.CantidadMisPujas),
                SubastasGanadas = participaciones.Count(p => p.GanadorId == query.UsuarioId && p.Estado == EstadoSubasta.FINALIZADA),
                SubastasPublicadas = publicaciones.Count,
                SubastasVendidas = publicaciones.Count(p => p.Estado == EstadoSubasta.FINALIZADA && p.GanadorId.HasValue)
            };

            return new ActividadesUsuarioDto
            {
                Participaciones = participaciones,
                Publicaciones = publicaciones,
                Metricas = metricas
            };
        }
    }
}
