using System;
using System.Threading;
using System.Threading.Tasks;
using PROYECTO_SUBASTA.Application.DTOs;
using PROYECTO_SUBASTA.Application.Repositories;

namespace PROYECTO_SUBASTA.Application.Queries.Categorias
{
    public class ObtenerCategoriaPorIdQuery
    {
        public int Id { get; set; }
    }

    public class ObtenerCategoriaPorIdQueryHandler
    {
        private readonly ICategoriaRepository _categoriaRepository;

        public ObtenerCategoriaPorIdQueryHandler(ICategoriaRepository categoriaRepository)
        {
            _categoriaRepository = categoriaRepository ?? throw new ArgumentNullException(nameof(categoriaRepository));
        }

        public async Task<CategoriaResponseDto?> HandleAsync(ObtenerCategoriaPorIdQuery query, CancellationToken cancellationToken = default)
        {
            if (query == null || query.Id <= 0)
                return null;

            var categoria = await _categoriaRepository.ObtenerPorIdAsync(query.Id);
            if (categoria == null)
                return null;

            return new CategoriaResponseDto
            {
                Id = categoria.Id,
                Nombre = categoria.Nombre,
                UrlIcono = categoria.UrlIcono
            };
        }
    }
}
