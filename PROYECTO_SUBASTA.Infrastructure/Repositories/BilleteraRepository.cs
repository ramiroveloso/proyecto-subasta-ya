using Microsoft.EntityFrameworkCore;
using PROYECTO_SUBASTA.Domain.Entities;
using PROYECTO_SUBASTA.Infrastructure.Data;
using System.Threading.Tasks;
using PROYECTO_SUBASTA.Application.Repositories;

namespace PROYECTO_SUBASTA.Infrastructure.Repositories
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