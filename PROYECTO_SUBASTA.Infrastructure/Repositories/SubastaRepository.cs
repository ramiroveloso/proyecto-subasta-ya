using Microsoft.EntityFrameworkCore;
using PROYECTO_SUBASTA.Application.Repositories;
using PROYECTO_SUBASTA.Domain.Constants;
using PROYECTO_SUBASTA.Domain.Entities;
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
        public Task ActualizarConConcurrenciaAsync(Subasta subasta, int versionCliente)
        {
            // 1. Establecemos la versión que mandó el cliente como el valor original que EF espera encontrar
            _context.Entry(subasta).Property(s => s.Version).OriginalValue = (uint)versionCliente;

            // 2. Incrementamos explícitamente el valor actual en memoria
            subasta.Version = (uint)(versionCliente + 1);

            // 3. Marcamos la entidad como modificada (la persistencia queda delegada al Unit of Work)
            _context.Entry(subasta).State = EntityState.Modified;
            return Task.CompletedTask;
        }

        public async Task<List<Subasta>> ObtenerPorActivarAsync(DateTime ahora, CancellationToken cancellationToken = default)
        {
            return await _context.Subastas
                .Where(s => s.Estado == EstadoSubasta.PROGRAMADA && s.FechaInicio <= ahora && s.FechaFin > ahora)
                .ToListAsync(cancellationToken);
        }

        public async Task<List<int>> ObtenerIdsVencidasAsync(DateTime ahora, CancellationToken cancellationToken = default)
        {
            return await _context.Subastas
                .Where(s => (s.Estado == EstadoSubasta.ACTIVA || s.Estado == EstadoSubasta.PROGRAMADA) && s.FechaFin <= ahora)
                .Select(s => s.Id)
                .ToListAsync(cancellationToken);
        }

        public async Task<(List<Subasta> Items, int Total)> BuscarAsync(
            int? categoriaId,
            string? estado,
            decimal? precioMin,
            decimal? precioMax,
            string? orden,
            int pagina,
            int tamanoPagina,
            CancellationToken cancellationToken = default)
        {
            IQueryable<Subasta> query = _context.Subastas.AsNoTracking();

            if (categoriaId.HasValue && categoriaId.Value > 0)
            {
                query = query.Where(s => s.CategoriaId == categoriaId.Value);
            }

            if (!string.IsNullOrWhiteSpace(estado))
            {
                var estadoUpper = estado.Trim().ToUpperInvariant();
                query = query.Where(s => s.Estado == estadoUpper);
            }

            if (precioMin.HasValue)
            {
                var pMin = (double)precioMin.Value;
                query = query.Where(s => (s.Pujas.Select(p => (double?)p.Monto).Max() ?? (double)s.PrecioBase) >= pMin);
            }

            if (precioMax.HasValue)
            {
                var pMax = (double)precioMax.Value;
                query = query.Where(s => (s.Pujas.Select(p => (double?)p.Monto).Max() ?? (double)s.PrecioBase) <= pMax);
            }

            var ordenNorm = orden?.Trim().ToLowerInvariant();
            query = ordenNorm switch
            {
                "cierre" or "tiempo" => query.OrderBy(s => s.FechaFin),
                "cierre_desc" => query.OrderByDescending(s => s.FechaFin),
                "precio" => query.OrderBy(s => s.Pujas.Select(p => (double?)p.Monto).Max() ?? (double)s.PrecioBase),
                "precio_desc" => query.OrderByDescending(s => s.Pujas.Select(p => (double?)p.Monto).Max() ?? (double)s.PrecioBase),
                "pujas" or "puja" => query.OrderByDescending(s => s.Pujas.Count),
                _ => query.OrderByDescending(s => s.Id)
            };

            var total = await query.CountAsync(cancellationToken);

            var page = pagina > 0 ? pagina : 1;
            var size = Math.Clamp(tamanoPagina > 0 ? tamanoPagina : 20, 1, 100);
            var skip = (page - 1) * size;

            var items = await query
                .Skip(skip)
                .Take(size)
                .Include(s => s.Categoria)
                .Include(s => s.Pujas)
                .ToListAsync(cancellationToken);

            return (items, total);
        }

        public async Task<List<Subasta>> ObtenerPorParticipanteAsync(int usuarioId, CancellationToken cancellationToken = default)
        {
            return await _context.Subastas
                .AsNoTracking()
                .Include(s => s.Categoria)
                .Include(s => s.Pujas)
                .Where(s => s.Pujas.Any(p => p.UsuarioId == usuarioId))
                .OrderByDescending(s => s.FechaFin)
                .ToListAsync(cancellationToken);
        }

        public async Task<List<Subasta>> ObtenerPorVendedorAsync(int usuarioId, CancellationToken cancellationToken = default)
        {
            return await _context.Subastas
                .AsNoTracking()
                .Include(s => s.Categoria)
                .Include(s => s.Pujas)
                .Where(s => s.VendedorId == usuarioId)
                .OrderByDescending(s => s.FechaInicio)
                .ToListAsync(cancellationToken);
        }
    }
}