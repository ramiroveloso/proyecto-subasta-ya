using System;
using System.Threading;
using System.Threading.Tasks;
using PROYECTO_SUBASTA.Application.Constants;
using PROYECTO_SUBASTA.Application.DTOs;
using PROYECTO_SUBASTA.Application.Exceptions;
using PROYECTO_SUBASTA.Application.Mappers;
using PROYECTO_SUBASTA.Application.Repositories;
using PROYECTO_SUBASTA.Application.UseCases;
using PROYECTO_SUBASTA.Domain.Constants;
using PROYECTO_SUBASTA.Domain.Entities;

namespace PROYECTO_SUBASTA.Application.Commands.Subastas
{
    public class CrearSubastaCommandHandler
    {
        private readonly ISubastaRepository _subastaRepository;
        private readonly ICategoriaRepository _categoriaRepository;
        private readonly IRepository<Usuario> _usuarioRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IAuditoriaService _auditoriaService;

        public CrearSubastaCommandHandler(
            ISubastaRepository subastaRepository,
            ICategoriaRepository categoriaRepository,
            IRepository<Usuario> usuarioRepository,
            IUnitOfWork unitOfWork,
            IAuditoriaService auditoriaService)
        {
            _subastaRepository = subastaRepository ?? throw new ArgumentNullException(nameof(subastaRepository));
            _categoriaRepository = categoriaRepository ?? throw new ArgumentNullException(nameof(categoriaRepository));
            _usuarioRepository = usuarioRepository ?? throw new ArgumentNullException(nameof(usuarioRepository));
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _auditoriaService = auditoriaService ?? throw new ArgumentNullException(nameof(auditoriaService));
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
                throw new ReglaNegocioException("La fecha de finalización debe ser posterior a la fecha actual en UTC.");

            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            try
            {
                var categoria = await _categoriaRepository.ObtenerPorIdAsync(command.CategoriaId);
                if (categoria == null)
                    throw new RecursoNoEncontradoException($"No se encontró la categoría con ID {command.CategoriaId}.");

                var vendedor = await _usuarioRepository.GetByIdAsync(command.VendedorId);
                if (vendedor == null)
                    throw new RecursoNoEncontradoException($"No se encontró el vendedor con ID {command.VendedorId}.");

                string estadoInicial = command.FechaInicio <= ahoraUtc && command.FechaFin > ahoraUtc ? EstadoSubasta.ACTIVA : EstadoSubasta.PROGRAMADA;

                var subasta = new Subasta
                {
                    Titulo = command.Titulo.Trim(),
                    Descripcion = command.Descripcion?.Trim() ?? string.Empty,
                    UrlImagen = command.UrlImagen?.Trim() ?? string.Empty,
                    PrecioBase = command.PrecioBase,
                    IncrementoMinimo = command.IncrementoMinimo,
                    FechaInicio = command.FechaInicio,
                    FechaFin = command.FechaFin,
                    CategoriaId = command.CategoriaId,
                    VendedorId = command.VendedorId,
                    Estado = estadoInicial,
                    Version = 1
                };

                await _subastaRepository.CrearAsync(subasta);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                await _auditoriaService.RegistrarAsync(
                    AuditoriaAcciones.SUBASTA_CREADA,
                    $"Subasta #{subasta.Id} '{subasta.Titulo}' creada exitosamente por Vendedor #{subasta.VendedorId} con estado {subasta.Estado}.",
                    subasta.VendedorId
                );

                await _unitOfWork.CommitTransactionAsync(cancellationToken);

                subasta.Categoria = categoria;
                return SubastaMapper.MapearADto(subasta);
            }
            catch
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                throw;
            }
        }
    }
}
