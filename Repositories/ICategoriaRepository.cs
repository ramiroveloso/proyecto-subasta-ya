using PROYECTO_SUBASTA.Entities;

namespace PROYECTO_SUBASTA.Repositories
{
    // Definimos el contrato de persistencia para el dominio de categorías, desacoplando las reglas de negocio de los detalles de infraestructura.
    public interface ICategoriaRepository
    {
        // Declaramos la firma asíncrona para recuperar la colección completa de categorías disponibles en el sistema.
        Task<IEnumerable<Categoria>> ObtenerTodasAsync();

        // Establecemos el contrato para buscar una categoría específica de manera unívoca mediante su identificador.
        Task<Categoria?> ObtenerPorIdAsync(int id);

        // Definimos la operación de contrato para registrar una nueva categoría dentro del flujo de persistencia.
        Task CrearAsync(Categoria categoria);

        // Exponemos la firma para consolidar y guardar los cambios transaccionales en el motor de base de datos.
        Task GuardarCambiosAsync();
    }
}