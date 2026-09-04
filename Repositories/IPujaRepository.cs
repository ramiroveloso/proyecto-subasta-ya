using PROYECTO_SUBASTA.Entities;

namespace PROYECTO_SUBASTA.Repositories
{
    public interface IPujaRepository
    {
        // Prepara la nueva puja para ser guardada en la base de datos
        Task CrearAsync(Puja puja);

        // Ejecuta la transacción final en la base de datos
        Task GuardarCambiosAsync();

        // (Opcional por ahora, pero vital luego) Para mostrar el historial de ofertas de una subasta
        Task<IEnumerable<Puja>> ObtenerPorSubastaIdAsync(int subastaId);
    }
}