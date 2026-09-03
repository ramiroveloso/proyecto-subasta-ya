using Microsoft.EntityFrameworkCore;
using PROYECTO_SUBASTA.Entities;
using PROYECTO_SUBASTA.Infraestructure;
using System.Threading.Tasks;

namespace PROYECTO_SUBASTA.Repositories
{
    public class BilleteraRepository : Repository<Billetera>, IBilleteraRepository
    {
        public BilleteraRepository(SubastaDbContext context) : base(context) { }

        public async Task<Billetera?> ObtenerPorUsuarioIdAsync(int usuarioId)
        {
            return await _context.Billeteras
                .FirstOrDefaultAsync(b => b.UsuarioId == usuarioId);
        }
    }
}
