using Microsoft.EntityFrameworkCore;
using PROYECTO_SUBASTA.Entities;
using PROYECTO_SUBASTA.Infraestructure;

namespace PROYECTO_SUBASTA.Repositories
{
    public class PujaRepository : IPujaRepository
    {
        private readonly SubastaDbContext _context;

        public PujaRepository(SubastaDbContext context)
        {
            _context = context;
        }

        public async Task CrearAsync(Puja puja)
        {
            await _context.Pujas.AddAsync(puja);
        }

        public async Task GuardarCambiosAsync()
        {
            await _context.SaveChangesAsync();
        }

        public async Task<IEnumerable<Puja>> ObtenerPorSubastaIdAsync(int subastaId)
        {
            // Devuelve las pujas de una subasta ordenadas de mayor a menor monto
            return await _context.Pujas
                .Where(p => p.SubastaId == subastaId)
                .OrderByDescending(p => p.Monto)
                .ToListAsync();
        }
    }
}