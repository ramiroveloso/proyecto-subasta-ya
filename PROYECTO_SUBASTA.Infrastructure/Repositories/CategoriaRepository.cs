using Microsoft.EntityFrameworkCore;
using PROYECTO_SUBASTA.Domain.Entities;
using PROYECTO_SUBASTA.Application.Repositories;
using PROYECTO_SUBASTA.Infrastructure.Data;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PROYECTO_SUBASTA.Infrastructure.Repositories
{
    public class CategoriaRepository : Repository<Categoria>, ICategoriaRepository
    {
        public CategoriaRepository(SubastaDbContext context) : base(context) { }

        public async Task<IEnumerable<Categoria>> ObtenerTodasAsync()
        {
            return await GetAllAsync();
        }

        public async Task<Categoria?> ObtenerPorIdAsync(int id)
        {
            return await GetByIdAsync(id);
        }

        public async Task CrearAsync(Categoria categoria)
        {
            await AddAsync(categoria);
        }

        public async Task GuardarCambiosAsync()
        {
            await SaveChangesAsync();
        }
    }
}