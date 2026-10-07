using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PROYECTO_SUBASTA.Application.Commands.Subastas;
using PROYECTO_SUBASTA.Application.DTOs;
using PROYECTO_SUBASTA.Application.Exceptions;
using PROYECTO_SUBASTA.Application.Repositories;
using PROYECTO_SUBASTA.Domain.Entities;

namespace PROYECTO_SUBASTA.Application.UseCases
{
    public class SubastaUseCases
    {
        private readonly ISubastaRepository _subastaRepository;
        private readonly IAuditoriaService _auditoriaService;
        private readonly IAdjudicacionService _adjudicacionService;
        private readonly RegistrarPujaCommandHandler _registrarPujaHandler;
        private readonly CrearSubastaCommandHandler _crearSubastaHandler;

        public SubastaUseCases(
            ISubastaRepository subastaRepository,
            IAuditoriaService auditoriaService,
            IAdjudicacionService adjudicacionService,
            RegistrarPujaCommandHandler registrarPujaHandler,
            CrearSubastaCommandHandler crearSubastaHandler)
        {
            _subastaRepository = subastaRepository ?? throw new ArgumentNullException(nameof(subastaRepository));
            _auditoriaService = auditoriaService ?? throw new ArgumentNullException(nameof(auditoriaService));
            _adjudicacionService = adjudicacionService ?? throw new ArgumentNullException(nameof(adjudicacionService));
            _registrarPujaHandler = registrarPujaHandler ?? throw new ArgumentNullException(nameof(registrarPujaHandler));
            _crearSubastaHandler = crearSubastaHandler ?? throw new ArgumentNullException(nameof(crearSubastaHandler));
        }

        public async Task<IEnumerable<Subasta>> ObtenerActivasAsync()
        {
            await _adjudicacionService.ProcesarSubastasVencidasAsync();
            return await _subastaRepository.ObtenerActivasAsync();
        }

        public async Task<IEnumerable<Subasta>> ObtenerActivasPaginadasAsync(int pageNumber, int pageSize)
        {
            var subastasActivas = await ObtenerActivasAsync();
            return subastasActivas
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize);
        }

        public async Task<Subasta?> ObtenerPorIdAsync(int id)
        {
            await _adjudicacionService.ProcesarSubastasVencidasAsync();
            return await _subastaRepository.ObtenerPorIdAsync(id);
        }

        public async Task<Subasta> CrearAsync(CrearSubastaDto dto)
        {
            return await _crearSubastaHandler.HandleAsync(dto);
        }

        public async Task<Subasta> CrearAsync(Subasta subasta)
        {
            if (subasta == null) throw new ArgumentNullException(nameof(subasta));

            var dto = new CrearSubastaDto
            {
                Titulo = subasta.Titulo,
                Descripcion = subasta.Descripcion,
                UrlImagen = subasta.UrlImagen,
                PrecioBase = subasta.PrecioBase,
                IncrementoMinimo = subasta.IncrementoMinimo,
                FechaInicio = subasta.FechaInicio,
                FechaFin = subasta.FechaFin,
                CategoriaId = subasta.CategoriaId,
                VendedorId = subasta.VendedorId
            };

            return await _crearSubastaHandler.HandleAsync(dto);
        }

        public async Task<RegistrarPujaResponseDto> RegistrarPujaAsync(int subastaId, int usuarioId, decimal montoPuja, int versionCliente)
        {
            return await _registrarPujaHandler.HandleAsync(new RegistrarPujaCommand
            {
                SubastaId = subastaId,
                UsuarioId = usuarioId,
                Monto = montoPuja,
                VersionCliente = versionCliente
            });
        }
    }
}