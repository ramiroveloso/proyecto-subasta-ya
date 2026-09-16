using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PROYECTO_SUBASTA.Application.DTOs;
using PROYECTO_SUBASTA.Application.Exceptions;
using PROYECTO_SUBASTA.Application.Repositories;
using PROYECTO_SUBASTA.Domain.Entities;

namespace PROYECTO_SUBASTA.Application.UseCases
{
    public class SubastaUseCases
    {
        private readonly ISubastaRepository _subastaRepository;

        public SubastaUseCases(ISubastaRepository subastaRepository)
        {
            _subastaRepository = subastaRepository;
        }

        public async Task<IEnumerable<Subasta>> ObtenerActivasAsync()
        {
            return await _subastaRepository.ObtenerActivasAsync();
        }
        // Recupera el catálogo de subastas activas de forma paginada para optimizar recursos, de forma paginada
        public async Task<IEnumerable<Subasta>> ObtenerActivasPaginadasAsync(int pageNumber, int pageSize)
        {
            var subastasActivas = await _subastaRepository.ObtenerActivasAsync();

            return subastasActivas
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize);
        }

        public async Task<Subasta?> ObtenerPorIdAsync(int id)
        {
            return await _subastaRepository.ObtenerPorIdAsync(id);
        }

        // Firma requerida por los controladores
        public async Task<Subasta> CrearAsync(Subasta subasta)
        {
            if (subasta.PrecioBase <= 0)
            {
                throw new ArgumentException("El precio base de la subasta debe ser mayor a cero.");
            }

            if (subasta.IncrementoMinimo <= 0)
            {
                throw new ArgumentException("El incremento mínimo debe ser mayor a cero.");
            }

            if (subasta.FechaInicio >= subasta.FechaFin)
            {
                throw new ArgumentException("La fecha de inicio debe ser anterior a la de finalización.");
            }

            await _subastaRepository.CrearAsync(subasta);
            await _subastaRepository.GuardarCambiosAsync();

            return subasta;
        }

        // Firma basada en DTO
        public async Task<Subasta> CrearAsync(CrearSubastaDto dto)
        {
            if (dto == null)
            {
                throw new ReglaNegocioException("Los datos de la subasta son obligatorios.");
            }

            var subasta = new Subasta
            {
                Titulo = dto.Titulo.Trim(),
                Descripcion = dto.Descripcion.Trim(),
                UrlImagen = dto.UrlImagen.Trim(),
                PrecioBase = dto.PrecioBase,
                IncrementoMinimo = dto.IncrementoMinimo,
                FechaInicio = dto.FechaInicio,
                FechaFin = dto.FechaFin,
                Estado = "ACTIVA",
                CategoriaId = dto.CategoriaId,
                VendedorId = dto.VendedorId,
                Version = 1
            };

            return await CrearAsync(subasta);
        }

        public async Task RegistrarPujaAsync(int subastaId, int usuarioId, decimal montoPuja, int versionCliente)
        {
            var subasta = await _subastaRepository.ObtenerPorIdAsync(subastaId);
            if (subasta == null)
            {
                throw new KeyNotFoundException("La subasta especificada no existe.");
            }

            if (subasta.Estado != "ACTIVA")
            {
                throw new InvalidOperationException("No se pueden realizar pujas en una subasta que no está activa.");
            }

            decimal pujaMaximaActual = (subasta.Pujas != null && subasta.Pujas.Count > 0)
                ? subasta.Pujas.Max(p => p.Monto)
                : subasta.PrecioBase;

            if (montoPuja <= pujaMaximaActual)
            {
                throw new ArgumentException($"La puja debe superar la oferta actual de ${pujaMaximaActual}.");
            }

            if ((montoPuja - pujaMaximaActual) < subasta.IncrementoMinimo && subasta.Pujas != null && subasta.Pujas.Count > 0)
            {
                throw new ArgumentException($"El incremento mínimo requerido es de ${subasta.IncrementoMinimo}.");
            }

            var nuevaPuja = new Puja
            {
                SubastaId = subastaId,
                UsuarioId = usuarioId,
                Monto = montoPuja,
                FechaCreacion = DateTime.UtcNow
            };

            if (subasta.Pujas == null) subasta.Pujas = new List<Puja>();
            subasta.Pujas.Add(nuevaPuja);

            if (versionCliente > 0)
            {
                await _subastaRepository.ActualizarConConcurrenciaAsync(subasta, versionCliente);
            }
            else
            {
                subasta.Version += 1;
                await _subastaRepository.GuardarCambiosAsync();
            }
        }
    }
}