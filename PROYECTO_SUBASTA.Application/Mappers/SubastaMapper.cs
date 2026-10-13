using System.Linq;
using PROYECTO_SUBASTA.Application.DTOs;
using PROYECTO_SUBASTA.Domain.Entities;

namespace PROYECTO_SUBASTA.Application.Mappers
{
    /// <summary>
    /// Mapper centralizado para transformar entidades Subasta a DTOs de salida de la capa de aplicación.
    /// Evita duplicación y exposición de entidades de dominio en controladores o consultas.
    /// </summary>
    public static class SubastaMapper
    {
        public static SubastaResponseDto MapearADto(Subasta subasta)
        {
            if (subasta == null)
            {
                return null!;
            }

            return new SubastaResponseDto
            {
                Id = subasta.Id,
                Titulo = subasta.Titulo,
                Descripcion = subasta.Descripcion,
                UrlImagen = subasta.UrlImagen,
                PrecioBase = subasta.PrecioBase,
                IncrementoMinimo = subasta.IncrementoMinimo,
                FechaInicio = subasta.FechaInicio,
                FechaFin = subasta.FechaFin,
                Estado = subasta.Estado,
                CategoriaId = subasta.CategoriaId,
                CategoriaNombre = subasta.Categoria?.Nombre ?? string.Empty,
                VendedorId = subasta.VendedorId,
                GanadorId = subasta.GanadorId,
                PrecioFinal = subasta.PrecioFinal,
                Version = subasta.Version,
                Pujas = subasta.Pujas?.Select(p => new PujaItemDto
                {
                    Id = p.Id,
                    SubastaId = p.SubastaId,
                    UsuarioId = p.UsuarioId,
                    Monto = p.Monto,
                    FechaCreacion = p.FechaCreacion,
                    PostorAnonimo = $"Postor #{(p.UsuarioId * 33 + 100):X}"
                }).OrderByDescending(p => p.Monto).ToList() ?? new()
            };
        }
    }
}
