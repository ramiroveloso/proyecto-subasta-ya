using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using PROYECTO_SUBASTA.Domain.Entities;
using PROYECTO_SUBASTA.Application.Repositories;

namespace PROYECTO_SUBASTA.Application.UseCases
{
    // Centraliza la lógica de negocio y validaciones estrictas para la gestión y publicación de subastas.
    public class SubastaUseCases
    {
        private readonly ISubastaRepository _subastaRepository;

        // Inyectamos el repositorio de subastas cumpliendo con el principio de inversión de dependencias.
        public SubastaUseCases(ISubastaRepository subastaRepository)
        {
            _subastaRepository = subastaRepository;
        }

        // Recupera el catálogo de subastas activas aplicando las reglas de filtrado correspondientes.
        public async Task<IEnumerable<Subasta>> ObtenerActivasAsync()
        {
            return await _subastaRepository.ObtenerActivasAsync();
        }

        // Busca una subasta específica por su identificador único asegurando la trazabilidad del recurso.
        public async Task<Subasta?> ObtenerPorIdAsync(int id)
        {
            return await _subastaRepository.ObtenerPorIdAsync(id);
        }

        // Orquesta la creación de una nueva subasta aplicando validaciones de negocio críticas.
        public async Task<Subasta> CrearAsync(Subasta subasta)
        {
            // Validamos que el precio base sea estrictamente mayor a cero para garantizar la viabilidad económica de la puja.
            if (subasta.PrecioBase <= 0)
            {
                throw new ArgumentException("El precio base de la subasta debe ser mayor a cero.");
            }

            // Validamos que el incremento mínimo esté configurado correctamente para evitar pujas inválidas.
            if (subasta.IncrementoMinimo <= 0)
            {
                throw new ArgumentException("El incremento mínimo debe ser mayor a cero.");
            }

            // Verificamos la coherencia cronológica de la subasta (la fecha de inicio debe ser anterior a la fecha de cierre).
            if (subasta.FechaInicio >= subasta.FechaFin)
            {
                throw new ArgumentException("La fecha de inicio debe ser anterior a la fecha de finalización de la subasta.");
            }

            await _subastaRepository.CrearAsync(subasta);
            await _subastaRepository.GuardarCambiosAsync();

            return subasta;
        }
        // Registra una puja en una subasta específica aplicando reglas de validación de negocio y control de concurrencia optimista.
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

            // Incrementamos la versión para disparar la concurrencia optimista en EF Core
            subasta.Version += 1;

            await _subastaRepository.GuardarCambiosAsync();
        }
    }
}