using Microsoft.EntityFrameworkCore;
using PROYECTO_SUBASTA.Entities;
using PROYECTO_SUBASTA.Infraestructure;

namespace PROYECTO_SUBASTA.Repositories
{
    public class CategoriaRepository : ICategoriaRepository
    {
        private readonly SubastaDbContext _context;

        // Implementamos el principio de inversión de dependencias inyectando el contexto de base de datos para desacoplar la lógica de persistencia.
        public CategoriaRepository(SubastaDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Categoria>> ObtenerTodasAsync()
        {
            // Ejecutamos una consulta asíncrona sobre el conjunto de datos para abstraer la recuperación masiva de entidades de dominio.
            return await _context.Categorias.ToListAsync();
        }

        public async Task<Categoria?> ObtenerPorIdAsync(int id)
        {
            // Consultamos la persistencia buscando una coincidencia por clave primaria de manera optimizada y directa.
            return await _context.Categorias.FindAsync(id);
        }

        public async Task CrearAsync(Categoria categoria)
        {
            // Añadimos una nueva entidad al contexto de seguimiento para preparar su inserción dentro de la unidad de trabajo.
            await _context.Categorias.AddAsync(categoria);
        }

        public async Task GuardarCambiosAsync()
        {
            // Consolidamos físicamente las operaciones pendientes en el motor de base de datos de forma transaccional.
            await _context.SaveChangesAsync();
        }
    }
}