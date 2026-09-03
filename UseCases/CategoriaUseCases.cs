using PROYECTO_SUBASTA.Entities;
using PROYECTO_SUBASTA.Repositories;

namespace PROYECTO_SUBASTA.UseCases
{
    // Encapsula las reglas de negocio y operaciones aplicativas correspondientes al dominio de categorías.
    public class CategoriaUseCases
    {
        private readonly ICategoriaRepository _categoriaRepository;

        // Inyectamos el repositorio abstracto para mantener el caso de uso desacoplado de la persistencia física.
        public CategoriaUseCases(ICategoriaRepository categoriaRepository)
        {
            _categoriaRepository = categoriaRepository;
        }

        // Orquesta la recuperación de todas las categorías disponibles aplicando las políticas de la aplicación.
        public async Task<IEnumerable<Categoria>> ObtenerTodasAsync()
        {
            return await _categoriaRepository.ObtenerTodasAsync();
        }

        // Ejecuta la lógica para registrar una nueva categoría validando reglas previas si fuera necesario.
        public async Task<Categoria> CrearAsync(Categoria categoria)
        {
            // Validamos que el nombre de la categoría no esté vacío para proteger la integridad del dominio.
            if (string.IsNullOrWhiteSpace(categoria.Nombre))
            {
                throw new ArgumentException("El nombre de la categoría es obligatorio.");
            }

            await _categoriaRepository.CrearAsync(categoria);
            await _categoriaRepository.GuardarCambiosAsync();

            return categoria;
        }
    }
}