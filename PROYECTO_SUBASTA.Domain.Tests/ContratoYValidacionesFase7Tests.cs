using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PROYECTO_SUBASTA.Application.Commands.Subastas;
using PROYECTO_SUBASTA.Application.DTOs;
using PROYECTO_SUBASTA.Application.Exceptions;
using PROYECTO_SUBASTA.Application.Queries.Subastas;
using PROYECTO_SUBASTA.Application.Queries.Usuarios;
using PROYECTO_SUBASTA.Domain.Entities;
using PROYECTO_SUBASTA.Infrastructure.Data;
using PROYECTO_SUBASTA.Infrastructure.Repositories;
using Xunit;

namespace PROYECTO_SUBASTA.Domain.Tests
{
    public class ContratoYValidacionesFase7Tests : IDisposable
    {
        private readonly string _connectionString;
        private readonly SqliteConnection _masterConnection;
        private readonly DbContextOptions<SubastaDbContext> _options;

        public ContratoYValidacionesFase7Tests()
        {
            _connectionString = $"DataSource=test_fase7_{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
            _masterConnection = new SqliteConnection(_connectionString);
            _masterConnection.Open();

            _options = new DbContextOptionsBuilder<SubastaDbContext>()
                .UseSqlite(_connectionString)
                .Options;

            using var context = new SubastaDbContext(_options);
            context.Database.EnsureCreated();
        }

        public void Dispose()
        {
            _masterConnection.Dispose();
        }

        // ========================================================================
        // 1. VALIDACIONES B01 (DataAnnotations + Reglas de Dominio)
        // ========================================================================

        [Theory]
        [InlineData("", "obligatorio")]
        [InlineData("   ", "obligatorio")]
        [InlineData("ab", "caracteres")]
        public void CrearSubastaDto_TituloInvalido_FallaValidacion(string tituloInvalido, string errorEsperadoParcial)
        {
            var dto = CrearDtoValido();
            dto.Titulo = tituloInvalido;

            var context = new ValidationContext(dto);
            var results = new List<ValidationResult>();
            var esValido = Validator.TryValidateObject(dto, context, results, true);

            Assert.False(esValido);
            Assert.Contains(results, r => r.ErrorMessage != null && r.ErrorMessage.Contains(errorEsperadoParcial, StringComparison.OrdinalIgnoreCase));
        }

        [Theory]
        [InlineData("ftp://servidor.com/img.png")]
        [InlineData("imagen_local.jpg")]
        [InlineData("/assets/reloj.png")]
        [InlineData("not-a-url")]
        public void CrearSubastaDto_UrlImagenNoHttpHttps_FallaValidacion(string urlInvalida)
        {
            var dto = CrearDtoValido();
            dto.UrlImagen = urlInvalida;

            var context = new ValidationContext(dto);
            var results = new List<ValidationResult>();
            var esValido = Validator.TryValidateObject(dto, context, results, true);

            Assert.False(esValido);
            Assert.Contains(results, r => r.MemberNames.Contains(nameof(CrearSubastaDto.UrlImagen)));
        }

        [Fact]
        public void CrearSubastaDto_FechaFinAnteriorOIgualAFechaInicio_FallaValidacion()
        {
            var dto = CrearDtoValido();
            dto.FechaInicio = DateTime.UtcNow.AddDays(2);
            dto.FechaFin = DateTime.UtcNow.AddDays(1); // FechaFin < FechaInicio

            var context = new ValidationContext(dto);
            var results = new List<ValidationResult>();
            var esValido = Validator.TryValidateObject(dto, context, results, true);

            Assert.False(esValido);
            Assert.Contains(results, r => r.ErrorMessage != null && r.ErrorMessage.Contains("posterior", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void CrearSubastaDto_FechaFinPasada_FallaValidacion()
        {
            var dto = CrearDtoValido();
            dto.FechaInicio = DateTime.UtcNow.AddDays(-5);
            dto.FechaFin = DateTime.UtcNow.AddDays(-1); // FechaFin ya vencida

            var context = new ValidationContext(dto);
            var results = new List<ValidationResult>();
            var esValido = Validator.TryValidateObject(dto, context, results, true);

            Assert.False(esValido);
            Assert.Contains(results, r => r.ErrorMessage != null && r.ErrorMessage.Contains("futura", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public async Task CrearSubastaCommandHandler_CategoriaInexistente_LanzaReglaNegocioException()
        {
            using var context = new SubastaDbContext(_options);
            var sRepo = new SubastaRepository(context);
            var cRepo = new CategoriaRepository(context);
            var uow = new UnitOfWork(context);
            var handler = new CrearSubastaCommandHandler(sRepo, cRepo, uow);

            var cmd = new CrearSubastaCommand
            {
                Titulo = "Reloj Antiguo Suizo",
                Descripcion = "Edición 1950",
                UrlImagen = "https://ejemplo.com/reloj.jpg",
                PrecioBase = 10000m,
                IncrementoMinimo = 500m,
                FechaInicio = DateTime.UtcNow,
                FechaFin = DateTime.UtcNow.AddDays(3),
                CategoriaId = 99999, // Categoria inexistente
                VendedorId = 1
            };

            var ex = await Assert.ThrowsAsync<ReglaNegocioException>(() => handler.HandleAsync(cmd));
            Assert.Contains("categoría con ID 99999 no existe", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        // ========================================================================
        // 2. B09: NINGÚN GET ESCRIBE EN LA BASE NI CAMBIA ESTADO DE VENCIDAS
        // ========================================================================

        [Fact]
        public async Task GetSubasta_SubastaVencida_NoCambiaEstadoEnBaseDeDatos()
        {
            using var context = new SubastaDbContext(_options);
            var sRepo = new SubastaRepository(context);
            var cRepo = new CategoriaRepository(context);

            // Subasta ACTIVA pero cuya fecha ya venció (hace 2 horas)
            var subastaVencida = new Subasta
            {
                Titulo = "Moneda de Oro Vencida",
                Descripcion = "Subasta expirada no cerrada",
                UrlImagen = "https://ejemplo.com/moneda.jpg",
                PrecioBase = 1000m,
                IncrementoMinimo = 100m,
                FechaInicio = DateTime.UtcNow.AddHours(-10),
                FechaFin = DateTime.UtcNow.AddHours(-2), // Expirada
                Estado = "ACTIVA",
                CategoriaId = 1,
                VendedorId = 1,
                Version = 1
            };

            await context.Subastas.AddAsync(subastaVencida);
            await context.SaveChangesAsync();

            var getByIdHandler = new ObtenerSubastaPorIdQueryHandler(sRepo);
            var getListHandler = new ObtenerSubastasActivasQueryHandler(sRepo, cRepo);

            // Act 1: GET por ID
            var detalle = await getByIdHandler.HandleAsync(new ObtenerSubastaPorIdQuery { Id = subastaVencida.Id });
            Assert.NotNull(detalle);

            // Act 2: GET listado
            var listado = await getListHandler.HandleAsync(new ObtenerSubastasQuery { Estado = "ACTIVA" });

            // Assert: Verificar directamente en base de datos que la entidad NO mutó su estado
            using var verifyContext = new SubastaDbContext(_options);
            var subastaEnDb = await verifyContext.Subastas.AsNoTracking().FirstOrDefaultAsync(s => s.Id == subastaVencida.Id);

            Assert.NotNull(subastaEnDb);
            Assert.Equal("ACTIVA", subastaEnDb.Estado); // El estado sigue siendo ACTIVA; GET no liquida operaciones
        }

        // ========================================================================
        // 3. B11: FILTROS, ORDEN Y VALIDACIÓN DE PARÁMETROS EN GET SUBASTAS
        // ========================================================================

        [Fact]
        public async Task ObtenerSubastasQueryHandler_EstadoDesconocido_LanzaReglaNegocioException()
        {
            using var context = new SubastaDbContext(_options);
            var sRepo = new SubastaRepository(context);
            var cRepo = new CategoriaRepository(context);
            var handler = new ObtenerSubastasActivasQueryHandler(sRepo, cRepo);

            var query = new ObtenerSubastasQuery
            {
                Estado = "INVENTADO_ESTADO"
            };

            var ex = await Assert.ThrowsAsync<ReglaNegocioException>(() => handler.HandleAsync(query));
            Assert.Contains("no es válido", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task ObtenerSubastasQueryHandler_PrecioMinimoMayorAlMaximo_LanzaReglaNegocioException()
        {
            using var context = new SubastaDbContext(_options);
            var sRepo = new SubastaRepository(context);
            var cRepo = new CategoriaRepository(context);
            var handler = new ObtenerSubastasActivasQueryHandler(sRepo, cRepo);

            var query = new ObtenerSubastasQuery
            {
                PrecioMin = 50000m,
                PrecioMax = 10000m // Invalido: Min > Max
            };

            var ex = await Assert.ThrowsAsync<ReglaNegocioException>(() => handler.HandleAsync(query));
            Assert.Contains("precio mínimo no puede ser mayor", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task ObtenerSubastasQueryHandler_CategoriaInexistente_RetornaColeccionVacia()
        {
            using var context = new SubastaDbContext(_options);
            var sRepo = new SubastaRepository(context);
            var cRepo = new CategoriaRepository(context);
            var handler = new ObtenerSubastasActivasQueryHandler(sRepo, cRepo);

            var query = new ObtenerSubastasQuery
            {
                CategoriaId = 99999 // Inexistente
            };

            var resultado = await handler.HandleAsync(query);

            Assert.NotNull(resultado);
            Assert.Empty(resultado.Items);
            Assert.Equal(0, resultado.TotalCount);
        }

        // ========================================================================
        // 4. B11: ACTIVIDADES DE USUARIO (LIDER / SUPERADO / GANADA / PERDIDA)
        // ========================================================================

        [Fact]
        public async Task ObtenerActividadesUsuarioQueryHandler_CalculaEstadosParticipacionYMetricas()
        {
            using var context = new SubastaDbContext(_options);
            var uRepo = new Repository<Usuario>(context);
            var sRepo = new SubastaRepository(context);
            var handler = new ObtenerActividadesUsuarioQueryHandler(uRepo, sRepo);

            // Usuario 2 (Comprador Líder)
            var actividades = await handler.HandleAsync(new ObtenerActividadesUsuarioQuery { UsuarioId = 2 });

            Assert.NotNull(actividades);
            Assert.Equal(2, actividades.UsuarioId);
            Assert.NotNull(actividades.Participaciones);
            Assert.NotNull(actividades.Publicaciones);
            Assert.NotNull(actividades.Metricas);

            // Los estados de participación deben pertenecer a los valores de rúbrica
            var estadosValidos = new[] { "LIDER", "SUPERADO", "GANADA", "PERDIDA" };
            foreach (var part in actividades.Participaciones)
            {
                Assert.Contains(part.EstadoParticipacion, estadosValidos);
                Assert.True(part.MiMayorOferta > 0);
            }
        }

        private static CrearSubastaDto CrearDtoValido()
        {
            var ahora = DateTime.UtcNow;
            return new CrearSubastaDto
            {
                Titulo = "Cámara Réflex Profesional",
                Descripcion = "Sensor full frame",
                UrlImagen = "https://ejemplo.com/camara.jpg",
                PrecioBase = 80000m,
                IncrementoMinimo = 5000m,
                FechaInicio = ahora,
                FechaFin = ahora.AddDays(5),
                CategoriaId = 1,
                VendedorId = 1
            };
        }
    }
}
