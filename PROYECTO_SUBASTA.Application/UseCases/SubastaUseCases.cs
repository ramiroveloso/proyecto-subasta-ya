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
        private readonly IAuditoriaService _auditoriaService;
        private readonly IAdjudicacionService _adjudicacionService;

        public SubastaUseCases(
            ISubastaRepository subastaRepository,
            IAuditoriaService auditoriaService,
            IAdjudicacionService adjudicacionService)
        {
            _subastaRepository = subastaRepository;
            _auditoriaService = auditoriaService;
            _adjudicacionService = adjudicacionService;
        }

        public async Task<IEnumerable<Subasta>> ObtenerActivasAsync()
        {
            // Sincroniza y procesa subastas vencidas antes de listar
            await _adjudicacionService.ProcesarSubastasVencidasAsync();

            var subastas = (await _subastaRepository.ObtenerActivasAsync()).ToList();
            return subastas;
        }

        // Recupera el catálogo de subastas de forma paginada para optimizar recursos
        public async Task<IEnumerable<Subasta>> ObtenerActivasPaginadasAsync(int pageNumber, int pageSize)
        {
            var subastasActivas = await ObtenerActivasAsync();

            return subastasActivas
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize);
        }

        public async Task<Subasta?> ObtenerPorIdAsync(int id)
        {
            // Sincroniza y procesa subastas vencidas antes de obtener por ID
            await _adjudicacionService.ProcesarSubastasVencidasAsync();

            return await _subastaRepository.ObtenerPorIdAsync(id);
        }

        // Firma requerida por los controladores
        public async Task<Subasta> CrearAsync(Subasta subasta)
        {
            // Garantizar que las fechas se guarden en UTC en la Base de Datos
            subasta.FechaInicio = subasta.FechaInicio.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(subasta.FechaInicio, DateTimeKind.Utc)
                : subasta.FechaInicio.ToUniversalTime();

            subasta.FechaFin = subasta.FechaFin.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(subasta.FechaFin, DateTimeKind.Utc)
                : subasta.FechaFin.ToUniversalTime();

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

            // Sincronizar estado inicial
            var ahoraUtc = DateTime.UtcNow;
            if (subasta.FechaInicio > ahoraUtc)
            {
                subasta.Estado = "PROGRAMADA";
            }
            else if (subasta.FechaFin <= ahoraUtc)
            {
                subasta.Estado = (subasta.Pujas != null && subasta.Pujas.Any()) ? "FINALIZADA" : "DESIERTA";
            }
            else
            {
                subasta.Estado = "ACTIVA";
            }

            if (subasta.Version == 0) subasta.Version = 1;

            await _subastaRepository.CrearAsync(subasta);
            await _subastaRepository.GuardarCambiosAsync();

            await _auditoriaService.RegistrarAsync(
                "CREACION_SUBASTA",
                $"Subasta #{subasta.Id} ('{subasta.Titulo}') creada exitosamente con estado inicial '{subasta.Estado}'. Base: ${subasta.PrecioBase:N2}, Cierre: {subasta.FechaFin:yyyy-MM-dd HH:mm:ss} UTC.",
                subasta.VendedorId
            );

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
                CategoriaId = dto.CategoriaId,
                VendedorId = dto.VendedorId,
                Version = 1
            };

            return await CrearAsync(subasta);
        }

        public async Task<(Subasta Subasta, Puja Puja, uint SubastaVersion, bool AntiSnipingActivado)> RegistrarPujaAsync(int subastaId, int usuarioId, decimal montoPuja, int versionCliente)
        {
            var subasta = await _subastaRepository.ObtenerPorIdAsync(subastaId);
            if (subasta == null)
            {
                await _auditoriaService.RegistrarAsync(
                    "PUJA_RECHAZADA",
                    $"Intento de puja de ${montoPuja:N2} por Usuario #{usuarioId} en Subasta #{subastaId} rechazado: La subasta especificada no existe.",
                    usuarioId
                );
                throw new KeyNotFoundException("La subasta especificada no existe.");
            }

            if (subasta.Estado != "ACTIVA")
            {
                await _auditoriaService.RegistrarAsync(
                    "PUJA_RECHAZADA",
                    $"Intento de puja de ${montoPuja:N2} por Usuario #{usuarioId} en Subasta #{subastaId} rechazado: La subasta no se encuentra en estado ACTIVA (Estado actual: '{subasta.Estado}').",
                    usuarioId
                );
                throw new InvalidOperationException("No se pueden realizar pujas en una subasta que no está activa.");
            }

            if (subasta.Version == 0)
            {
                subasta.Version = (uint)(subasta.Pujas != null && subasta.Pujas.Count > 0 ? subasta.Pujas.Count + 1 : 1);
            }

            decimal pujaMaximaActual = (subasta.Pujas != null && subasta.Pujas.Count > 0)
                ? subasta.Pujas.Max(p => p.Monto)
                : subasta.PrecioBase;

            if (montoPuja <= pujaMaximaActual)
            {
                await _auditoriaService.RegistrarAsync(
                    "PUJA_RECHAZADA",
                    $"Intento de puja de ${montoPuja:N2} por Usuario #{usuarioId} en Subasta #{subastaId} rechazado: El monto ofertado (${montoPuja:N2}) no supera la puja líder actual de ${pujaMaximaActual:N2}.",
                    usuarioId
                );
                throw new ArgumentException($"La puja debe superar la oferta actual de ${pujaMaximaActual}.");
            }

            if ((montoPuja - pujaMaximaActual) < subasta.IncrementoMinimo && subasta.Pujas != null && subasta.Pujas.Count > 0)
            {
                await _auditoriaService.RegistrarAsync(
                    "PUJA_RECHAZADA",
                    $"Intento de puja de ${montoPuja:N2} por Usuario #{usuarioId} en Subasta #{subastaId} rechazado: El incremento (${montoPuja - pujaMaximaActual:N2}) es inferior al mínimo requerido de ${subasta.IncrementoMinimo:N2}.",
                    usuarioId
                );
                throw new ArgumentException($"El incremento mínimo requerido es de ${subasta.IncrementoMinimo}.");
            }

            // ========================================================================
            // REGLA ANTI-SNIPING (EXTENSIÓN DE TIEMPO)
            // Si la oferta ingresa en el último minuto (< 60s), se extiende el cierre +60s
            // ========================================================================
            var ahoraUtc = DateTime.UtcNow;
            var tiempoRestante = subasta.FechaFin - ahoraUtc;
            bool antiSnipingActivado = false;

            if (tiempoRestante > TimeSpan.Zero && tiempoRestante <= TimeSpan.FromSeconds(60))
            {
                var fechaFinAnterior = subasta.FechaFin;
                subasta.FechaFin = subasta.FechaFin.AddSeconds(60);
                antiSnipingActivado = true;

                await _auditoriaService.RegistrarAsync(
                    "EXTENSION_ANTI_SNIPING",
                    $"Regla Anti-Sniping gatillada en Subasta #{subasta.Id} ('{subasta.Titulo}'). Oferta de última hora de Usuario #{usuarioId} (${montoPuja:N2}) extendió la fecha de finalización de {fechaFinAnterior:yyyy-MM-dd HH:mm:ss} UTC a {subasta.FechaFin:yyyy-MM-dd HH:mm:ss} UTC (+60s).",
                    usuarioId
                );
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

            try
            {
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
            catch (Exception ex) when (ex.GetType().Name.Contains("Concurrency") || ex is ConcurrenciaException)
            {
                await _auditoriaService.RegistrarAsync(
                    "PUJA_RECHAZADA_CONCURRENCIA",
                    $"Intento de puja de ${montoPuja:N2} por Usuario #{usuarioId} en Subasta #{subastaId} rechazado por colisión de concurrencia optimista (versión del cliente {versionCliente} desactualizada).",
                    usuarioId
                );
                throw;
            }

            return (subasta, nuevaPuja, subasta.Version, antiSnipingActivado);
        }
    }
}