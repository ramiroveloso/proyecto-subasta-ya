using PROYECTO_SUBASTA.Entities;

namespace PROYECTO_SUBASTA.Repositories
{
    // Definimos el contrato de persistencia para el módulo de subastas, aislando la lógica de negocio de los detalles de base de datos.
    public interface ISubastaRepository
    {
        // Declaramos la firma para consultar de manera asíncrona todas las subastas que se encuentren activas en el sistema.
        Task<IEnumerable<Subasta>> ObtenerActivasAsync();

        // Establecemos el contrato para recuperar una subasta en particular a través de su identificador único.
        Task<Subasta?> ObtenerPorIdAsync(int id);

        // Definimos la operación de contrato para registrar una nueva subasta dentro del contexto de persistencia.
        Task CrearAsync(Subasta subasta);

        // Exponemos la firma para modificar o actualizar los datos de una subasta existente.
        Task ActualizarAsync(Subasta subasta);

        // Definimos el contrato para consolidar y guardar físicamente los cambios transaccionales en la base de datos.
        Task GuardarCambiosAsync();
    }
}