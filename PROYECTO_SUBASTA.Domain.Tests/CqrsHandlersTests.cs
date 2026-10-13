using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PROYECTO_SUBASTA.Application.Commands.Billeteras;
using PROYECTO_SUBASTA.Application.Commands.Categorias;
using PROYECTO_SUBASTA.Application.Commands.Subastas;
using PROYECTO_SUBASTA.Application.Commands.Usuarios;
using PROYECTO_SUBASTA.Application.DTOs;
using PROYECTO_SUBASTA.Application.Exceptions;
using PROYECTO_SUBASTA.Application.Mappers;
using PROYECTO_SUBASTA.Application.Queries.Auditorias;
using PROYECTO_SUBASTA.Application.Queries.Billeteras;
using PROYECTO_SUBASTA.Application.Queries.Categorias;
using PROYECTO_SUBASTA.Application.Queries.Subastas;
using PROYECTO_SUBASTA.Application.Queries.Usuarios;
using PROYECTO_SUBASTA.Application.Repositories;
using PROYECTO_SUBASTA.Application.UseCases;
using PROYECTO_SUBASTA.Domain.Entities;
using PROYECTO_SUBASTA.Infrastructure.Data;
using PROYECTO_SUBASTA.Infrastructure.Repositories;
using PROYECTO_SUBASTA.Infrastructure.Services;
using Xunit;

namespace PROYECTO_SUBASTA.Domain.Tests
{
    public class CqrsHandlersTests : IDisposable
    {
        private readonly string _connectionString;
        private readonly SqliteConnection _masterConnection;
        private readonly DbContextOptions<SubastaDbContext> _options;
        private readonly IServiceProvider _serviceProvider;

        public CqrsHandlersTests()
        {
            _connectionString = $"DataSource=test_{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
            _masterConnection = new SqliteConnection(_connectionString);
            _masterConnection.Open();

            _options = new DbContextOptionsBuilder<SubastaDbContext>()
                .UseSqlite(_connectionString)
                .Options;

            var services = new ServiceCollection();
            services.AddDbContext<SubastaDbContext>(opt => opt.UseSqlite(_connectionString));
            services.AddLogging();
            _serviceProvider = services.BuildServiceProvider();

            using var initContext = new SubastaDbContext(_options);
            initContext.Database.EnsureCreated();
        }

        public void Dispose()
        {
            _masterConnection.Dispose();
        }

        [Fact]
        public async Task IniciarSesionCommandHandler_CredencialesValidas_RetornaRespuestaExitosa()
        {
            // Arrange
            using var context = new SubastaDbContext(_options);
            var repo = new Repository<Usuario>(context);
            var handler = new IniciarSesionCommandHandler(repo);

            // Act
            var res = await handler.HandleAsync(new IniciarSesionCommand { Usuario = "vendedor@test.com" });

            // Assert
            Assert.NotNull(res);
            Assert.NotNull(res.Usuario);
            Assert.Equal("Vendedor Test", res.Usuario.Nombre);
            Assert.Equal("vendedor@test.com", res.Usuario.Email);
        }

        [Fact]
        public async Task IniciarSesionCommandHandler_UsuarioInexistente_LanzaUnauthorized()
        {
            // Arrange
            using var context = new SubastaDbContext(_options);
            var repo = new Repository<Usuario>(context);
            var handler = new IniciarSesionCommandHandler(repo);

            // Act & Assert
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                handler.HandleAsync(new IniciarSesionCommand { Usuario = "fantasma@inexistente.com" }));
        }

        [Fact]
        public async Task IniciarSesionCommandHandler_InputVacio_LanzaReglaNegocioException()
        {
            // Arrange
            using var context = new SubastaDbContext(_options);
            var repo = new Repository<Usuario>(context);
            var handler = new IniciarSesionCommandHandler(repo);

            // Act & Assert
            await Assert.ThrowsAsync<ReglaNegocioException>(() =>
                handler.HandleAsync(new IniciarSesionCommand { Usuario = "   " }));
        }

        [Fact]
        public async Task CargarSaldoCommandHandler_MontoValido_AcreditaSaldoYGeneraAsientoLedger()
        {
            // Arrange
            using var context = new SubastaDbContext(_options);
            var bRepo = new BilleteraRepository(context);
            var lRepo = new Repository<TransactionLedger>(context);
            var uow = new UnitOfWork(context);
            var scopeFactory = _serviceProvider.GetRequiredService<IServiceScopeFactory>();
            var auditoria = new AuditoriaService(context, scopeFactory, NullLogger<AuditoriaService>.Instance);

            var handler = new CargarSaldoCommandHandler(bRepo, lRepo, uow, auditoria);

            var bInicial = await context.Billeteras.FirstAsync(b => b.UsuarioId == 4);
            var saldoInicial = bInicial.SaldoTotal;

            // Act
            var exito = await handler.HandleAsync(new CargarSaldoCommand { UsuarioId = 4, Monto = 25000 });

            // Assert
            Assert.True(exito);
            var bFinal = await context.Billeteras.FirstAsync(b => b.UsuarioId == 4);
            Assert.Equal(saldoInicial + 25000, bFinal.SaldoTotal);
            Assert.Equal(saldoInicial + 25000, bFinal.SaldoDisponible);

            var ledger = await context.TransactionLedgers.FirstOrDefaultAsync(t => t.BilleteraId == bFinal.Id && t.Tipo == TipoTransaccion.DEPOSITO);
            Assert.NotNull(ledger);
            Assert.Equal(25000, ledger.Monto);
        }

        [Fact]
        public async Task CrearSubastaCommandHandler_ParametrosValidos_CreaSubastaConVersion1()
        {
            // Arrange
            using var context = new SubastaDbContext(_options);
            var sRepo = new SubastaRepository(context);
            var cRepo = new CategoriaRepository(context);
            var uow = new UnitOfWork(context);
            var handler = new CrearSubastaCommandHandler(sRepo, cRepo, uow);

            var ahora = DateTime.UtcNow;
            var command = new CrearSubastaCommand
            {
                Titulo = "Notebook Dell XPS",
                Descripcion = "Ultrabook premium",
                UrlImagen = "https://ejemplo.com/dell.jpg",
                PrecioBase = 45000,
                IncrementoMinimo = 3000,
                FechaInicio = ahora.AddMinutes(-5),
                FechaFin = ahora.AddDays(3),
                CategoriaId = 1,
                VendedorId = 1
            };

            // Act
            var subasta = await handler.HandleAsync(command);

            // Assert
            Assert.NotNull(subasta);
            Assert.True(subasta.Id > 0);
            Assert.Equal("ACTIVA", subasta.Estado);
            Assert.Equal(1u, subasta.Version);
            Assert.Equal(45000, subasta.PrecioBase);
        }

        [Fact]
        public async Task ObtenerBilleteraPorUsuarioQueryHandler_RetornaBilleteraYMovimientosMapeados()
        {
            // Arrange
            using var context = new SubastaDbContext(_options);
            var bRepo = new BilleteraRepository(context);
            var lRepo = new Repository<TransactionLedger>(context);
            var handler = new ObtenerBilleteraPorUsuarioQueryHandler(bRepo, lRepo);

            // Act
            var res = await handler.HandleAsync(new ObtenerBilleteraPorUsuarioQuery { UsuarioId = 2 });

            // Assert
            Assert.NotNull(res);
            Assert.Equal(2, res.UsuarioId);
            Assert.NotNull(res.Movimientos);
        }

        [Fact]
        public async Task ObtenerUsuariosQueryHandler_RetornaListaAnonimizadaDeUsuarios()
        {
            // Arrange
            using var context = new SubastaDbContext(_options);
            var uRepo = new Repository<Usuario>(context);
            var handler = new ObtenerUsuariosQueryHandler(uRepo);

            // Act
            var usuarios = await handler.HandleAsync(new ObtenerUsuariosQuery());

            // Assert
            Assert.NotNull(usuarios);
            Assert.True(usuarios.Count() >= 4);
            Assert.Contains(usuarios, u => u.Email == "vendedor@test.com");
        }

        [Fact]
        public async Task CategoriasCQRS_CrearYObtener_RetornaDTOsEsperados()
        {
            // Arrange
            using var context = new SubastaDbContext(_options);
            var catRepo = new CategoriaRepository(context);
            var crearHandler = new CrearCategoriaCommandHandler(catRepo);
            var obtenerTodasHandler = new ObtenerCategoriasQueryHandler(catRepo);
            var obtenerPorIdHandler = new ObtenerCategoriaPorIdQueryHandler(catRepo);

            var cmd = new CrearCategoriaCommand
            {
                Nombre = "Electrodomésticos",
                UrlIcono = "https://ejemplo.com/icono.png"
            };

            // Act
            var creadaDto = await crearHandler.HandleAsync(cmd);
            var todas = await obtenerTodasHandler.HandleAsync(new ObtenerCategoriasQuery());
            var obtenidaDto = await obtenerPorIdHandler.HandleAsync(new ObtenerCategoriaPorIdQuery { Id = creadaDto.Id });

            // Assert
            Assert.NotNull(creadaDto);
            Assert.True(creadaDto.Id > 0);
            Assert.Equal("Electrodomésticos", creadaDto.Nombre);
            Assert.Contains(todas, c => c.Id == creadaDto.Id && c.Nombre == "Electrodomésticos");
            Assert.NotNull(obtenidaDto);
            Assert.Equal(creadaDto.Id, obtenidaDto.Id);
            Assert.Equal("Electrodomésticos", obtenidaDto.Nombre);
        }

        [Fact]
        public async Task CrearUsuarioCommandHandler_CreaUsuarioYBilleteraTransaccionalmente()
        {
            // Arrange
            using var context = new SubastaDbContext(_options);
            var uRepo = new Repository<Usuario>(context);
            var bRepo = new BilleteraRepository(context);
            var uow = new UnitOfWork(context);
            var handler = new CrearUsuarioCommandHandler(uRepo, bRepo, uow);

            var cmd = new CrearUsuarioCommand
            {
                Nombre = "Nuevo Postor Test",
                Email = "nuevo_postor@test.com"
            };

            // Act
            var usuarioDto = await handler.HandleAsync(cmd);

            // Assert
            Assert.NotNull(usuarioDto);
            Assert.True(usuarioDto.Id > 0);
            Assert.Equal("Nuevo Postor Test", usuarioDto.Nombre);
            Assert.Equal("nuevo_postor@test.com", usuarioDto.Email);

            // Verificar billetera creada con saldo 0
            var billetera = await bRepo.ObtenerPorUsuarioIdAsync(usuarioDto.Id);
            Assert.NotNull(billetera);
            Assert.Equal(0m, billetera.SaldoTotal);
            Assert.Equal(0m, billetera.SaldoDisponible);
            Assert.Equal(0m, billetera.SaldoRetenido);
            Assert.Equal(1, billetera.Version);
        }

        [Fact]
        public async Task ObtenerLogsAuditoriaQueryHandler_RetornaLogsMapeadosADto()
        {
            // Arrange
            using var context = new SubastaDbContext(_options);
            var scopeFactory = _serviceProvider.GetRequiredService<IServiceScopeFactory>();
            var audService = new AuditoriaService(context, scopeFactory, NullLogger<AuditoriaService>.Instance);
            await audService.RegistrarAsync("TEST_EVENTO", "Detalle de prueba para CQRS", 1);

            var handler = new ObtenerLogsAuditoriaQueryHandler(audService);

            // Act
            var logs = await handler.HandleAsync(new ObtenerLogsAuditoriaQuery { Limite = 10 });

            // Assert
            Assert.NotNull(logs);
            Assert.NotEmpty(logs);
            Assert.Contains(logs, l => l.Accion == "TEST_EVENTO" && l.Detalle.Contains("CQRS"));
        }

        [Fact]
        public void SubastaMapper_MapearADto_MapeaCamposYPujasOrdenadasDescendente()
        {
            // Arrange
            var subasta = new Subasta
            {
                Id = 42,
                Titulo = "Subasta Mapper Test",
                Descripcion = "Descripción test",
                UrlImagen = "https://ejemplo.com/img.jpg",
                PrecioBase = 1000m,
                IncrementoMinimo = 100m,
                FechaInicio = DateTime.UtcNow.AddHours(-1),
                FechaFin = DateTime.UtcNow.AddHours(2),
                Estado = "ACTIVA",
                CategoriaId = 1,
                Categoria = new Categoria { Id = 1, Nombre = "Tecnología" },
                VendedorId = 2,
                Version = 3,
                Pujas = new List<Puja>
                {
                    new Puja { Id = 1, SubastaId = 42, UsuarioId = 10, Monto = 1200m, FechaCreacion = DateTime.UtcNow.AddMinutes(-30) },
                    new Puja { Id = 2, SubastaId = 42, UsuarioId = 11, Monto = 1500m, FechaCreacion = DateTime.UtcNow.AddMinutes(-10) }
                }
            };

            // Act
            var dto = SubastaMapper.MapearADto(subasta);

            // Assert
            Assert.NotNull(dto);
            Assert.Equal(42, dto.Id);
            Assert.Equal("Subasta Mapper Test", dto.Titulo);
            Assert.Equal("Tecnología", dto.CategoriaNombre);
            Assert.Equal(3u, dto.Version);
            Assert.Equal(2, dto.Pujas.Count);
            // La primera puja debe ser la más alta
            Assert.Equal(1500m, dto.Pujas[0].Monto);
            Assert.Equal(1200m, dto.Pujas[1].Monto);
        }
    }
}
