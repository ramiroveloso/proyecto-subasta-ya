using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PROYECTO_SUBASTA.Application.Commands.Billeteras;
using PROYECTO_SUBASTA.Application.Commands.Subastas;
using PROYECTO_SUBASTA.Application.Commands.Usuarios;
using PROYECTO_SUBASTA.Application.DTOs;
using PROYECTO_SUBASTA.Application.Exceptions;
using PROYECTO_SUBASTA.Application.Queries.Billeteras;
using PROYECTO_SUBASTA.Application.Queries.Subastas;
using PROYECTO_SUBASTA.Application.Queries.Usuarios;
using PROYECTO_SUBASTA.Application.Repositories;
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
            var uow = new UnitOfWork(context);
            var handler = new CrearSubastaCommandHandler(sRepo, uow);

            var ahora = DateTime.UtcNow;
            var dto = new CrearSubastaDto
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
            var subasta = await handler.HandleAsync(dto);

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
    }
}
