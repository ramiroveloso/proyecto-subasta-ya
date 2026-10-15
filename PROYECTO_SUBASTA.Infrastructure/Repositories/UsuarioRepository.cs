using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PROYECTO_SUBASTA.Application.Repositories;
using PROYECTO_SUBASTA.Domain.Entities;
using PROYECTO_SUBASTA.Infrastructure.Data;

namespace PROYECTO_SUBASTA.Infrastructure.Repositories
{
    public class UsuarioRepository : Repository<Usuario>, IUsuarioRepository
    {
        public UsuarioRepository(SubastaDbContext context) : base(context)
        {
        }

        public async Task<Usuario?> ObtenerPorEmailONombreAsync(string identificador, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(identificador))
                return null;

            var input = identificador.Trim();
            var inputLower = input.ToLower();

            return await _context.Usuarios
                .FirstOrDefaultAsync(u => u.Email.ToLower() == inputLower || u.Nombre.ToLower() == inputLower, cancellationToken);
        }
    }
}
