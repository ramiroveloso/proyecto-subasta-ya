using Microsoft.EntityFrameworkCore;
using PROYECTO_SUBASTA.Domain.Entities;
using PROYECTO_SUBASTA.Application.Repositories;
using PROYECTO_SUBASTA.Infrastructure.Data;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PROYECTO_SUBASTA.Infrastructure.Repositories
{
    public class SubastaRepository : Repository<Subasta>, ISubastaRepository
    {
        public SubastaRepository(SubastaDbContext context) : base(context)
        {
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
            // Recuperamos una subasta específica aplicando cargas relacionadas de categoría y pujas.
            return await _context.Subastas
                .Include(s => s.Categoria)
                .Include(s => s.Pujas)
                .FirstOrDefaultAsync(s => s.Id == id);
        }

        public async Task CrearAsync(Subasta subasta)
        {
            await AddAsync(subasta);
        }

        public async Task ActualizarAsync(Subasta subasta)
        {
            Update(subasta);
            await Task.CompletedTask;
        }

        public async Task GuardarCambiosAsync()
        {
            await SaveChangesAsync();
        }
    }
}