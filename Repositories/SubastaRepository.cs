using Microsoft.EntityFrameworkCore;
using PROYECTO_SUBASTA.Entities;
using PROYECTO_SUBASTA.Infraestructure;

namespace PROYECTO_SUBASTA.Repositories
{
    public class SubastaRepository : ISubastaRepository
    {
        private readonly SubastaDbContext _context;

        // Inyectamos el contexto de base de datos para desacoplar el acceso a los datos de la lógica de negocio.
        public SubastaRepository(SubastaDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Subasta>> ObtenerActivasAsync()
        {
            // Consultamos las subastas activas incluyendo de forma ansiosa (Eager Loading) sus categorías asociadas.
            return await _context.Subastas
                .Include(s => s.Categoria)
                .ToListAsync();
        }

        public async Task<Subasta?> ObtenerPorIdAsync(int id)
        {
            // Recuperamos una subasta específica aplicando cargas relacionadas de categoría y pujas para mantener la integridad del agregado.
            return await _context.Subastas
                .Include(s => s.Categoria)
                .Include(s => s.Pujas)
                .FirstOrDefaultAsync(s => s.Id == id);
        }

        public async Task CrearAsync(Subasta subasta)
        {
            // Registramos una nueva subasta en el contexto de seguimiento de Entity Framework.
            await _context.Subastas.AddAsync(subasta);
        }

        public async Task ActualizarAsync(Subasta subasta)
        {
            // Actualizamos el estado de la entidad subasta dentro del contexto de persistencia.
            _context.Subastas.Update(subasta);

            // Retornamos una tarea completada para mantener la firma asíncrona requerida por la interfaz.
            await Task.CompletedTask;
        }

        public async Task GuardarCambiosAsync()
        {
            // Persistimos físicamente los cambios acumulados en el motor de base de datos de manera transaccional.
            await _context.SaveChangesAsync();
        }
    }
}