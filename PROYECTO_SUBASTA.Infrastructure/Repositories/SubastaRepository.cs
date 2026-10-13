using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PROYECTO_SUBASTA.Application.Repositories;
using PROYECTO_SUBASTA.Domain.Entities;
using PROYECTO_SUBASTA.Infrastructure.Data;

namespace PROYECTO_SUBASTA.Infrastructure.Repositories
{
    public class SubastaRepository : Repository<Subasta>, ISubastaRepository
    {
        public SubastaRepository(SubastaDbContext context) : base(context)
        {
        }

        public async Task<IEnumerable<Subasta>> ObtenerActivasAsync()
        {
            return await _context.Subastas
                .Include(s => s.Categoria)
                .Include(s => s.Pujas)
                .Where(s => s.Estado == "ACTIVA")
                .ToListAsync();
        }

        public async Task<(IEnumerable<Subasta> Items, int TotalCount)> ObtenerFiltradasAsync(
            int? categoriaId,
            string? estado,
            decimal? precioMin,
            decimal? precioMax,
            string? ordenarPor,
            string? orden,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            var query = _context.Subastas
                .AsNoTracking()
                .Include(s => s.Categoria)
                .Include(s => s.Pujas)
                .AsQueryable();

            if (categoriaId.HasValue)
            {
                query = query.Where(s => s.CategoriaId == categoriaId.Value);
            }

            if (!string.IsNullOrWhiteSpace(estado))
            {
                var est = estado.Trim().ToUpperInvariant();
                query = query.Where(s => s.Estado == est);
            }

            if (precioMin.HasValue)
            {
                query = query.Where(s => s.PrecioBase >= precioMin.Value);
            }

            if (precioMax.HasValue)
            {
                query = query.Where(s => s.PrecioBase <= precioMax.Value);
            }

            var ordenAsc = string.Equals(orden?.Trim(), "asc", StringComparison.OrdinalIgnoreCase);
            var sortKey = (ordenarPor ?? string.Empty).Trim().ToLowerInvariant();

            query = sortKey switch
            {
                "precio" => ordenAsc ? query.OrderBy(s => s.PrecioBase) : query.OrderByDescending(s => s.PrecioBase),
                "titulo" => ordenAsc ? query.OrderBy(s => s.Titulo) : query.OrderByDescending(s => s.Titulo),
                "pujas" => ordenAsc ? query.OrderBy(s => s.Pujas.Count) : query.OrderByDescending(s => s.Pujas.Count),
                "cierre" => ordenAsc ? query.OrderBy(s => s.FechaFin) : query.OrderByDescending(s => s.FechaFin),
                _ => ordenAsc ? query.OrderBy(s => s.FechaFin) : query.OrderBy(s => s.FechaFin)
            };

            var totalCount = await query.CountAsync(cancellationToken);

            var items = await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            return (items, totalCount);
        }

        public async Task<IEnumerable<Subasta>> ObtenerPorUsuarioOfertanteAsync(int usuarioId, CancellationToken cancellationToken = default)
        {
            return await _context.Subastas
                .AsNoTracking()
                .Include(s => s.Categoria)
                .Include(s => s.Pujas)
                .Where(s => s.Pujas.Any(p => p.UsuarioId == usuarioId))
                .ToListAsync(cancellationToken);
        }

        public async Task<IEnumerable<Subasta>> ObtenerPorVendedorAsync(int vendedorId, CancellationToken cancellationToken = default)
        {
            return await _context.Subastas
                .AsNoTracking()
                .Include(s => s.Categoria)
                .Include(s => s.Pujas)
                .Where(s => s.VendedorId == vendedorId)
                .ToListAsync(cancellationToken);
        }

        public async Task<Subasta?> ObtenerPorIdAsync(int id)
        {
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

        public async Task ActualizarConConcurrenciaAsync(Subasta subasta, int versionCliente)
        {
            if (versionCliente > 0)
            {
                _context.Entry(subasta).Property(s => s.Version).OriginalValue = (uint)versionCliente;
            }

            _context.Entry(subasta).State = EntityState.Modified;
            await Task.CompletedTask;
        }

        public async Task<IEnumerable<int>> ObtenerVencidasParaCerrarIdsAsync(DateTime ahoraUtc)
        {
            return await _context.Subastas
                .Where(s => (s.Estado == "ACTIVA" || s.Estado == "PROGRAMADA") && s.FechaFin <= ahoraUtc)
                .Select(s => s.Id)
                .ToListAsync();
        }

        public async Task<IEnumerable<Subasta>> ObtenerProgramadasParaActivarAsync(DateTime ahoraUtc)
        {
            return await _context.Subastas
                .Where(s => s.Estado == "PROGRAMADA" && s.FechaInicio <= ahoraUtc && s.FechaFin > ahoraUtc)
                .ToListAsync();
        }
    }
}