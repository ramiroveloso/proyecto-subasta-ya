using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PROYECTO_SUBASTA.Domain.Entities;

namespace PROYECTO_SUBASTA.Application.Repositories
{
    public interface ISubastaRepository : IRepository<Subasta>
    {
        // Declaramos la firma para consultar de manera asíncrona todas las subastas que se encuentren activas en el sistema.
        Task<IEnumerable<Subasta>> ObtenerActivasAsync();

        // Consulta filtrada, ordenada y paginada ejecutada directamente a nivel IQueryable en base de datos
        Task<(IEnumerable<Subasta> Items, int TotalCount)> ObtenerFiltradasAsync(
            int? categoriaId,
            string? estado,
            decimal? precioMin,
            decimal? precioMax,
            string? ordenarPor,
            string? orden,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken = default);

        // Consultas optimizadas para el módulo de actividades del usuario
        Task<IEnumerable<Subasta>> ObtenerPorUsuarioOfertanteAsync(int usuarioId, CancellationToken cancellationToken = default);
        Task<IEnumerable<Subasta>> ObtenerPorVendedorAsync(int vendedorId, CancellationToken cancellationToken = default);

        // Establecemos el contrato para recuperar una subasta en particular a través de su identificador único.
        Task<Subasta?> ObtenerPorIdAsync(int id);

        // Definimos la operación de contrato para registrar una nueva subasta dentro del contexto de persistencia.
        Task CrearAsync(Subasta subasta);

        // Exponemos la firma para modificar o actualizar los datos de una subasta existente.
        Task ActualizarAsync(Subasta subasta);

        // Definimos el contrato para consolidar y guardar físicamente los cambios transaccionales en la base de datos.
        Task GuardarCambiosAsync();
        // Se actualiza el valor de la version para usar en caso de concurrencia 
        Task ActualizarConConcurrenciaAsync(Subasta subasta, int versionCliente);

        // Obtiene los IDs de las subastas cuyo tiempo ya venció y requieren cierre
        Task<IEnumerable<int>> ObtenerVencidasParaCerrarIdsAsync(DateTime ahoraUtc);

        // Obtiene las subastas en estado PROGRAMADA cuya fecha de inicio ya fue alcanzada
        Task<IEnumerable<Subasta>> ObtenerProgramadasParaActivarAsync(DateTime ahoraUtc);
    }
}