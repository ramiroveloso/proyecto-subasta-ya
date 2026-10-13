using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PROYECTO_SUBASTA.Application.DTOs;
using PROYECTO_SUBASTA.Application.Repositories;

namespace PROYECTO_SUBASTA.Application.Queries.Categorias
{
    public class ObtenerCategoriasQuery
    {
    }

    public class ObtenerCategoriasQueryHandler
    {
        private readonly ICategoriaRepository _categoriaRepository;

        public ObtenerCategoriasQueryHandler(ICategoriaRepository categoriaRepository)
        {
            _categoriaRepository = categoriaRepository ?? throw new ArgumentNullException(nameof(categoriaRepository));
        }

        public async Task<IEnumerable<CategoriaResponseDto>> HandleAsync(ObtenerCategoriasQuery query, CancellationToken cancellationToken = default)
        {
            var categorias = await _categoriaRepository.ObtenerTodasAsync();
            return categorias.Select(c => new CategoriaResponseDto
            {
                Id = c.Id,
                Nombre = c.Nombre,
                UrlIcono = c.UrlIcono
            }).ToList();
        }
    }
}
