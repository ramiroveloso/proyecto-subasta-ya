using System;
using System.Threading;
using System.Threading.Tasks;
using PROYECTO_SUBASTA.Application.DTOs;
using PROYECTO_SUBASTA.Application.Exceptions;
using PROYECTO_SUBASTA.Application.Mappers;
using PROYECTO_SUBASTA.Application.Repositories;
using PROYECTO_SUBASTA.Domain.Entities;

namespace PROYECTO_SUBASTA.Application.Commands.Subastas
{
    public class CrearSubastaCommandHandler
    {
        private readonly ISubastaRepository _subastaRepository;
        private readonly ICategoriaRepository _categoriaRepository;
        private readonly IUnitOfWork _unitOfWork;

        public CrearSubastaCommandHandler(
            ISubastaRepository subastaRepository,
            ICategoriaRepository categoriaRepository,
            IUnitOfWork unitOfWork)
        {
            _subastaRepository = subastaRepository ?? throw new ArgumentNullException(nameof(subastaRepository));
            _categoriaRepository = categoriaRepository ?? throw new ArgumentNullException(nameof(categoriaRepository));
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        }

        public async Task<SubastaResponseDto> HandleAsync(CrearSubastaCommand command, CancellationToken cancellationToken = default)
        {
            if (command == null)
                throw new ReglaNegocioException("Los datos de la subasta son obligatorios.");

            if (string.IsNullOrWhiteSpace(command.Titulo))
                throw new ReglaNegocioException("El título de la subasta es obligatorio.");

            if (command.PrecioBase <= 0)
                throw new ReglaNegocioException("El precio base de la subasta debe ser mayor a cero.");

            if (command.IncrementoMinimo <= 0)
                throw new ReglaNegocioException("El incremento mínimo debe ser mayor a cero.");

            if (command.FechaInicio >= command.FechaFin)
                throw new ReglaNegocioException("La fecha de inicio debe ser anterior a la de finalización.");

            var ahoraUtc = DateTime.UtcNow;
            if (command.FechaFin <= ahoraUtc)
                throw new ReglaNegocioException("La fecha de finalización debe ser futura.");

            if (command.CategoriaId <= 0)
                throw new ReglaNegocioException("Debe especificar una categoría válida.");

            var categoria = await _categoriaRepository.ObtenerPorIdAsync(command.CategoriaId);
            if (categoria == null)
                throw new ReglaNegocioException($"La categoría con ID {command.CategoriaId} no existe.");

            if (string.IsNullOrWhiteSpace(command.UrlImagen) ||
                !Uri.TryCreate(command.UrlImagen, UriKind.Absolute, out var uriResult) ||
                (uriResult.Scheme != Uri.UriSchemeHttp && uriResult.Scheme != Uri.UriSchemeHttps))
            {
                throw new ReglaNegocioException("La URL de la imagen debe ser una URL absoluta con protocolo http o https.");
            }

            string estadoInicial = command.FechaInicio <= ahoraUtc && command.FechaFin > ahoraUtc ? "ACTIVA" : "PROGRAMADA";

            var subasta = new Subasta
            {
                Titulo = command.Titulo.Trim(),
                Descripcion = command.Descripcion?.Trim() ?? string.Empty,
                UrlImagen = command.UrlImagen.Trim(),
                PrecioBase = command.PrecioBase,
                IncrementoMinimo = command.IncrementoMinimo,
                FechaInicio = command.FechaInicio,
                FechaFin = command.FechaFin,
                CategoriaId = command.CategoriaId,
                Categoria = categoria,
                VendedorId = command.VendedorId,
                Estado = estadoInicial,
                Version = 1
            };

            await _subastaRepository.CrearAsync(subasta);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return SubastaMapper.MapearADto(subasta);
        }
    }
}
