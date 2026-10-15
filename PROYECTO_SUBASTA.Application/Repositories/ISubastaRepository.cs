using PROYECTO_SUBASTA.Domain.Entities;

namespace PROYECTO_SUBASTA.Application.Repositories
{
    public interface ISubastaRepository : IRepository<Subasta>
    {
        // Establecemos el contrato para recuperar una subasta en particular a través de su identificador único.
        Task<Subasta?> ObtenerPorIdAsync(int id);

        // Definimos la operación de contrato para registrar una nueva subasta dentro del contexto de persistencia.
        Task CrearAsync(Subasta subasta);
        //se actualiza el valor de la version para usar en caso de concurrencia 
        Task ActualizarConConcurrenciaAsync(Subasta subasta, int versionCliente);

        Task<List<Subasta>> ObtenerPorActivarAsync(DateTime ahora, CancellationToken cancellationToken = default);
        Task<List<int>> ObtenerIdsVencidasAsync(DateTime ahora, CancellationToken cancellationToken = default);

        Task<(List<Subasta> Items, int Total)> BuscarAsync(
            int? categoriaId,
            string? estado,
            decimal? precioMin,
            decimal? precioMax,
            string? orden,
            int pagina,
            int tamanoPagina,
            CancellationToken cancellationToken = default);

        Task<List<Subasta>> ObtenerPorParticipanteAsync(int usuarioId, CancellationToken cancellationToken = default);
        Task<List<Subasta>> ObtenerPorVendedorAsync(int usuarioId, CancellationToken cancellationToken = default);
    }
}