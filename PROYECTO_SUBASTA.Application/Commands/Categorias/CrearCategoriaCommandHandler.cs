using System;
using System.Threading;
using System.Threading.Tasks;
using PROYECTO_SUBASTA.Application.DTOs;
using PROYECTO_SUBASTA.Application.Exceptions;
using PROYECTO_SUBASTA.Application.Repositories;
using PROYECTO_SUBASTA.Domain.Entities;

namespace PROYECTO_SUBASTA.Application.Commands.Categorias
{
    public class CrearCategoriaCommandHandler
    {
        private readonly ICategoriaRepository _categoriaRepository;

        public CrearCategoriaCommandHandler(ICategoriaRepository categoriaRepository)
        {
            _categoriaRepository = categoriaRepository ?? throw new ArgumentNullException(nameof(categoriaRepository));
        }

        public async Task<CategoriaResponseDto> HandleAsync(CrearCategoriaCommand command, CancellationToken cancellationToken = default)
        {
            if (command == null)
            {
                throw new ReglaNegocioException("Los datos de la categoría son obligatorios.");
            }

            if (string.IsNullOrWhiteSpace(command.Nombre))
            {
                throw new ReglaNegocioException("El nombre de la categoría es obligatorio.");
            }

            var categoria = new Categoria
            {
                Nombre = command.Nombre.Trim(),
                UrlIcono = command.UrlIcono?.Trim() ?? string.Empty
            };

            await _categoriaRepository.CrearAsync(categoria);
            await _categoriaRepository.GuardarCambiosAsync();

            return new CategoriaResponseDto
            {
                Id = categoria.Id,
                Nombre = categoria.Nombre,
                UrlIcono = categoria.UrlIcono
            };
        }
    }
}
