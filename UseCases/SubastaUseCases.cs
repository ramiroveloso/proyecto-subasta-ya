using PROYECTO_SUBASTA.Entities;
using PROYECTO_SUBASTA.Repositories;

namespace PROYECTO_SUBASTA.UseCases
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
    }
}