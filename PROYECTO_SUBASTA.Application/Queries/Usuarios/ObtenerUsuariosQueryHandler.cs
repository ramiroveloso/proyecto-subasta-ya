using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PROYECTO_SUBASTA.Application.DTOs;
using PROYECTO_SUBASTA.Application.Repositories;
using PROYECTO_SUBASTA.Domain.Entities;

namespace PROYECTO_SUBASTA.Application.Queries.Usuarios
{
    public class ObtenerUsuariosQuery
    {
    }

    public class ObtenerUsuariosQueryHandler
    {
        private readonly IRepository<Usuario> _usuarioRepository;

        public ObtenerUsuariosQueryHandler(IRepository<Usuario> usuarioRepository)
        {
            _usuarioRepository = usuarioRepository ?? throw new ArgumentNullException(nameof(usuarioRepository));
        }

        public async Task<IEnumerable<UsuarioResponseDto>> HandleAsync(ObtenerUsuariosQuery query, CancellationToken cancellationToken = default)
        {
            var usuarios = await _usuarioRepository.GetAllAsync();
            return usuarios.Select(u => new UsuarioResponseDto
            {
                Id = u.Id,
                Nombre = u.Nombre,
                Email = u.Email,
                FechaRegistro = u.FechaRegistro
            }).ToList();
        }
    }

    public class ObtenerUsuarioPorIdQuery
    {
        public int Id { get; set; }
    }

    public class ObtenerUsuarioPorIdQueryHandler
    {
        private readonly IRepository<Usuario> _usuarioRepository;

        public ObtenerUsuarioPorIdQueryHandler(IRepository<Usuario> usuarioRepository)
        {
            _usuarioRepository = usuarioRepository ?? throw new ArgumentNullException(nameof(usuarioRepository));
        }

        public async Task<UsuarioResponseDto?> HandleAsync(ObtenerUsuarioPorIdQuery query, CancellationToken cancellationToken = default)
        {
            if (query == null || query.Id <= 0)
                return null;

            var usuario = await _usuarioRepository.GetByIdAsync(query.Id);
            if (usuario == null)
                return null;

            return new UsuarioResponseDto
            {
                Id = usuario.Id,
                Nombre = usuario.Nombre,
                Email = usuario.Email,
                FechaRegistro = usuario.FechaRegistro
            };
        }
    }
}
