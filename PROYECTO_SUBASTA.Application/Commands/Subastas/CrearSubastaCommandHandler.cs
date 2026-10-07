using System;
using System.Threading;
using System.Threading.Tasks;
using PROYECTO_SUBASTA.Application.DTOs;
using PROYECTO_SUBASTA.Application.Exceptions;
using PROYECTO_SUBASTA.Application.Repositories;
using PROYECTO_SUBASTA.Domain.Entities;

namespace PROYECTO_SUBASTA.Application.Commands.Subastas
{
    public class CrearSubastaCommandHandler
    {
        private readonly ISubastaRepository _subastaRepository;
        private readonly IUnitOfWork _unitOfWork;

        public CrearSubastaCommandHandler(ISubastaRepository subastaRepository, IUnitOfWork unitOfWork)
        {
            _subastaRepository = subastaRepository ?? throw new ArgumentNullException(nameof(subastaRepository));
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        }

        public async Task<Subasta> HandleAsync(CrearSubastaDto dto, CancellationToken cancellationToken = default)
        {
            if (dto == null)
                throw new ReglaNegocioException("Los datos de la subasta son obligatorios.");

            if (dto.PrecioBase <= 0)
                throw new ReglaNegocioException("El precio base de la subasta debe ser mayor a cero.");

            if (dto.IncrementoMinimo <= 0)
                throw new ReglaNegocioException("El incremento mínimo debe ser mayor a cero.");

            if (dto.FechaInicio >= dto.FechaFin)
                throw new ReglaNegocioException("La fecha de inicio debe ser anterior a la de finalización.");

            var ahoraUtc = DateTime.UtcNow;
            string estadoInicial = dto.FechaInicio <= ahoraUtc && dto.FechaFin > ahoraUtc ? "ACTIVA" : "PROGRAMADA";

            var subasta = new Subasta
            {
                Titulo = dto.Titulo.Trim(),
                Descripcion = dto.Descripcion?.Trim() ?? string.Empty,
                UrlImagen = dto.UrlImagen?.Trim() ?? string.Empty,
                PrecioBase = dto.PrecioBase,
                IncrementoMinimo = dto.IncrementoMinimo,
                FechaInicio = dto.FechaInicio,
                FechaFin = dto.FechaFin,
                CategoriaId = dto.CategoriaId,
                VendedorId = dto.VendedorId,
                Estado = estadoInicial,
                Version = 1
            };

            await _subastaRepository.CrearAsync(subasta);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return subasta;
        }
    }
}
