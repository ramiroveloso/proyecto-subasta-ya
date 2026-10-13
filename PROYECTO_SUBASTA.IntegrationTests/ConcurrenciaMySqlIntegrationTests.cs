using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PROYECTO_SUBASTA.Application.DTOs;
using PROYECTO_SUBASTA.Domain.Entities;
using PROYECTO_SUBASTA.Infrastructure.Data;
using Xunit;
using Xunit.Abstractions;

namespace PROYECTO_SUBASTA.IntegrationTests
{
    public class ConcurrenciaMySqlIntegrationTests : IClassFixture<MySqlWebApplicationFactory>, IAsyncLifetime
    {
        private readonly MySqlWebApplicationFactory _factory;
        private readonly ITestOutputHelper _output;
        private readonly string? _connectionString;

        public ConcurrenciaMySqlIntegrationTests(MySqlWebApplicationFactory factory, ITestOutputHelper output)
        {
            _factory = factory;
            _output = output;
            _connectionString = Environment.GetEnvironmentVariable("TEST_MYSQL_CONNECTION");
        }

        public async Task InitializeAsync()
        {
            // Antes de cada suite de pruebas con MySQL real: borrar y migrar la base de pruebas
            if (!string.IsNullOrWhiteSpace(_connectionString) && _factory.TieneConexionConfigurada)
            {
                await _factory.ReiniciarBaseDeDatosAsync();
            }
        }

        public Task DisposeAsync() => Task.CompletedTask;

        private void VerificarRequisitoMySql()
        {
            Skip.If(string.IsNullOrWhiteSpace(_connectionString),
                "Omitido: Variable de entorno TEST_MYSQL_CONNECTION no configurada (se requiere servidor MySQL real para ejecutar pruebas de concurrencia e integración transaccional).");
        }

        // ========================================================================
        // ESCENARIO A: MISMA SUBASTA (20 CICLOS)
        // Dos usuarios nuevos envían el mismo monto con la misma versión a la vez.
        // Esperado: exactamente un 200; el otro 409 o 400; nunca dos 200.
        // ========================================================================
        [SkippableFact]
        public async Task EscenarioA_MismaSubasta_PujasConcurrentes_ExactamenteUnGanador_20Ciclos()
        {
            VerificarRequisitoMySql();

            int total200 = 0;
            int total409 = 0;
            int total400 = 0;

            for (int ciclo = 1; ciclo <= 20; ciclo++)
            {
                using var scope = _factory.Services.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<SubastaDbContext>();

                // 1. Preparar subasta activa nueva
                int subastaId = 1000 + ciclo;
                var ahora = DateTime.UtcNow;

                var subasta = new Subasta
                {
                    Id = subastaId,
                    Titulo = $"Subasta Concurrencia A #{ciclo}",
                    Descripcion = "Prueba de colisión OCC en misma subasta",
                    PrecioBase = 1000m,
                    IncrementoMinimo = 100m,
                    FechaInicio = ahora.AddDays(-1),
                    FechaFin = ahora.AddHours(2),
                    Estado = "ACTIVA",
                    CategoriaId = 1,
                    VendedorId = 1,
                    Version = 1
                };
                context.Subastas.Add(subasta);

                // 2. Crear dos usuarios NUEVOS con billetera propia y ledger coherente (DEPOSITO = SaldoTotal)
                var userA = new Usuario
                {
                    Nombre = $"Postor A #{ciclo}",
                    Email = $"postora_{ciclo}_{Guid.NewGuid():N}@test.com",
                    PasswordHash = "hash123",
                    FechaRegistro = ahora
                };
                var userB = new Usuario
                {
                    Nombre = $"Postor B #{ciclo}",
                    Email = $"postorb_{ciclo}_{Guid.NewGuid():N}@test.com",
                    PasswordHash = "hash123",
                    FechaRegistro = ahora
                };
                context.Usuarios.AddRange(userA, userB);
                await context.SaveChangesAsync();

                var bA = new Billetera
                {
                    UsuarioId = userA.Id,
                    SaldoTotal = 100000m,
                    SaldoDisponible = 100000m,
                    SaldoRetenido = 0m,
                    Version = 1
                };
                var bB = new Billetera
                {
                    UsuarioId = userB.Id,
                    SaldoTotal = 100000m,
                    SaldoDisponible = 100000m,
                    SaldoRetenido = 0m,
                    Version = 1
                };
                context.Billeteras.AddRange(bA, bB);
                await context.SaveChangesAsync();

                context.TransactionLedgers.AddRange(
                    new TransactionLedger { BilleteraId = bA.Id, Tipo = TipoTransaccion.DEPOSITO, Monto = 100000m, Fecha = ahora },
                    new TransactionLedger { BilleteraId = bB.Id, Tipo = TipoTransaccion.DEPOSITO, Monto = 100000m, Fecha = ahora }
                );
                await context.SaveChangesAsync();

                // 3. Obtener versión actual mediante GET a la API justo antes de disparar
                var client = _factory.CreateClient();
                var getResp = await client.GetAsync($"/api/Subastas/{subastaId}");
                Assert.True(getResp.IsSuccessStatusCode);
                var subastaDto = await getResp.Content.ReadFromJsonAsync<SubastaResponseDto>();
                Assert.NotNull(subastaDto);
                int versionActual = (int)subastaDto.Version;

                // 4. Disparar ambas peticiones simultáneamente
                decimal montoPuja = 50000m;
                var payloadUsuarioA = new { usuarioId = userA.Id, monto = montoPuja, version = versionActual };
                var payloadUsuarioB = new { usuarioId = userB.Id, monto = montoPuja, version = versionActual };

                var tareaA = client.PostAsJsonAsync($"/api/Subastas/{subastaId}/pujas", payloadUsuarioA);
                var tareaB = client.PostAsJsonAsync($"/api/Subastas/{subastaId}/pujas", payloadUsuarioB);

                var respuestas = await Task.WhenAll(tareaA, tareaB);

                foreach (var r in respuestas)
                {
                    if (r.StatusCode == HttpStatusCode.OK) total200++;
                    else if (r.StatusCode == HttpStatusCode.Conflict) total409++;
                    else if (r.StatusCode == HttpStatusCode.BadRequest) total400++;
                }

                // Esperado: exactamente un 200, el otro 409 o 400
                int conteoExitos = respuestas.Count(r => r.StatusCode == HttpStatusCode.OK);
                Assert.True(conteoExitos == 1,
                    $"[Ciclo {ciclo}] Se esperaba exactamente un 200 OK, pero se obtuvieron {conteoExitos}. Status 1: {respuestas[0].StatusCode}, Status 2: {respuestas[1].StatusCode}");

                var otraRespuesta = respuestas.First(r => r.StatusCode != HttpStatusCode.OK);
                Assert.True(otraRespuesta.StatusCode == HttpStatusCode.Conflict || otraRespuesta.StatusCode == HttpStatusCode.BadRequest,
                    $"[Ciclo {ciclo}] La petición perdedora debió retornar 409 Conflict o 400 BadRequest. Retornó: {otraRespuesta.StatusCode}");

                // 5. Verificar estado persistido en base de datos
                using var assertScope = _factory.Services.CreateScope();
                var assertContext = assertScope.ServiceProvider.GetRequiredService<SubastaDbContext>();

                var subastaDb = await assertContext.Subastas.Include(s => s.Pujas).FirstAsync(s => s.Id == subastaId);
                Assert.Single(subastaDb.Pujas!);
                Assert.Equal(montoPuja, subastaDb.Pujas!.First().Monto);

                var postorGanadorId = subastaDb.Pujas!.First().UsuarioId;
                var postorPerdedorId = postorGanadorId == userA.Id ? userB.Id : userA.Id;

                var bGanador = await assertContext.Billeteras.FirstAsync(b => b.UsuarioId == postorGanadorId);
                Assert.Equal(montoPuja, bGanador.SaldoRetenido);
                Assert.Equal(100000m - montoPuja, bGanador.SaldoDisponible);

                var bPerdedor = await assertContext.Billeteras.FirstAsync(b => b.UsuarioId == postorPerdedorId);
                Assert.Equal(0m, bPerdedor.SaldoRetenido);
                Assert.Equal(100000m, bPerdedor.SaldoDisponible);

                // Helper de conciliación financiera contable estricta
                await AssertConciliacionAsync(assertContext);
            }

            _output.WriteLine($"[ESCENARIO A COMPLETADO - 20 CICLOS] Total 200 OK: {total200}, Total 409 Conflict: {total409}, Total 400 BadRequest: {total400}");
        }

        // ========================================================================
        // ESCENARIO B: MISMA BILLETERA EN DOS SUBASTAS DISTINTAS (20 CICLOS)
        // Usuario NUEVO con 100.000 disponibles, no líder en ninguna de las dos subastas,
        // puja 70.000 en cada una a la vez.
        // Esperado: a lo sumo un 200; nunca dos retenciones de 70.000.
        // La otra respuesta debe distinguir 409 (versión) de 400 (fondos).
        // ========================================================================
        [SkippableFact]
        public async Task EscenarioB_MismaBilletera_DosSubastasDistintas_SaldoInsuficienteParaAmbas_20Ciclos()
        {
            VerificarRequisitoMySql();

            int total200 = 0;
            int total409 = 0;
            int total400 = 0;

            for (int ciclo = 1; ciclo <= 20; ciclo++)
            {
                using var scope = _factory.Services.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<SubastaDbContext>();

                int subasta1Id = 2000 + (ciclo * 2);
                int subasta2Id = 2000 + (ciclo * 2) + 1;
                var ahora = DateTime.UtcNow;

                var s1 = new Subasta
                {
                    Id = subasta1Id,
                    Titulo = $"Subasta B1 #{ciclo}",
                    Descripcion = "Subasta 1 para prueba concurrente de billetera",
                    PrecioBase = 10000m,
                    IncrementoMinimo = 1000m,
                    FechaInicio = ahora.AddDays(-1),
                    FechaFin = ahora.AddHours(2),
                    Estado = "ACTIVA",
                    CategoriaId = 1,
                    VendedorId = 1,
                    Version = 1
                };
                var s2 = new Subasta
                {
                    Id = subasta2Id,
                    Titulo = $"Subasta B2 #{ciclo}",
                    Descripcion = "Subasta 2 para prueba concurrente de billetera",
                    PrecioBase = 10000m,
                    IncrementoMinimo = 1000m,
                    FechaInicio = ahora.AddDays(-1),
                    FechaFin = ahora.AddHours(2),
                    Estado = "ACTIVA",
                    CategoriaId = 1,
                    VendedorId = 1,
                    Version = 1
                };
                context.Subastas.AddRange(s1, s2);

                // Usuario NUEVO con exactamente $100.000 disponibles, no líder en ninguna subasta
                var userDual = new Usuario
                {
                    Nombre = $"Comprador Dual #{ciclo}",
                    Email = $"compradordual_{ciclo}_{Guid.NewGuid():N}@test.com",
                    PasswordHash = "hash123",
                    FechaRegistro = ahora
                };
                context.Usuarios.Add(userDual);
                await context.SaveChangesAsync();

                var bDual = new Billetera
                {
                    UsuarioId = userDual.Id,
                    SaldoTotal = 100000m,
                    SaldoDisponible = 100000m,
                    SaldoRetenido = 0m,
                    Version = 1
                };
                context.Billeteras.Add(bDual);
                await context.SaveChangesAsync();

                context.TransactionLedgers.Add(new TransactionLedger
                {
                    BilleteraId = bDual.Id,
                    Tipo = TipoTransaccion.DEPOSITO,
                    Monto = 100000m,
                    Fecha = ahora
                });
                await context.SaveChangesAsync();

                // Obtener versión de cada subasta con GET justo antes de disparar
                var client = _factory.CreateClient();
                var getResp1 = await client.GetAsync($"/api/Subastas/{subasta1Id}");
                Assert.True(getResp1.IsSuccessStatusCode);
                var subDto1 = await getResp1.Content.ReadFromJsonAsync<SubastaResponseDto>();
                Assert.NotNull(subDto1);
                int v1 = (int)subDto1.Version;

                var getResp2 = await client.GetAsync($"/api/Subastas/{subasta2Id}");
                Assert.True(getResp2.IsSuccessStatusCode);
                var subDto2 = await getResp2.Content.ReadFromJsonAsync<SubastaResponseDto>();
                Assert.NotNull(subDto2);
                int v2 = (int)subDto2.Version;

                // Disparar ambas ofertas de 70.000 simultáneamente
                decimal montoPuja = 70000m;
                var payload1 = new { usuarioId = userDual.Id, monto = montoPuja, version = v1 };
                var payload2 = new { usuarioId = userDual.Id, monto = montoPuja, version = v2 };

                var tarea1 = client.PostAsJsonAsync($"/api/Subastas/{subasta1Id}/pujas", payload1);
                var tarea2 = client.PostAsJsonAsync($"/api/Subastas/{subasta2Id}/pujas", payload2);

                var respuestas = await Task.WhenAll(tarea1, tarea2);

                int conteoExitos = 0;
                int conteo409 = 0;
                int conteo400 = 0;

                foreach (var r in respuestas)
                {
                    if (r.StatusCode == HttpStatusCode.OK) { total200++; conteoExitos++; }
                    else if (r.StatusCode == HttpStatusCode.Conflict) { total409++; conteo409++; }
                    else if (r.StatusCode == HttpStatusCode.BadRequest) { total400++; conteo400++; }
                }

                // Esperado: A lo sumo UN 200 (0 o 1). NUNCA dos retenciones de 70.000.
                Assert.True(conteoExitos <= 1,
                    $"[Ciclo {ciclo}] Se aceptaron ambas pujas de $70.000 con solo $100.000 disponibles. Conteo de éxitos 200: {conteoExitos}");

                // Si una petición no fue 200, debe ser 409 o 400
                var respuestasNoExito = respuestas.Where(r => r.StatusCode != HttpStatusCode.OK).ToList();
                foreach (var rNoExito in respuestasNoExito)
                {
                    Assert.True(rNoExito.StatusCode == HttpStatusCode.Conflict || rNoExito.StatusCode == HttpStatusCode.BadRequest,
                        $"[Ciclo {ciclo}] Código inesperado: {rNoExito.StatusCode}");
                }

                // Verificar que el SaldoRetenido nunca exceda los 100.000 en base
                using var assertScope = _factory.Services.CreateScope();
                var assertContext = assertScope.ServiceProvider.GetRequiredService<SubastaDbContext>();

                var bDualFinal = await assertContext.Billeteras.FirstAsync(b => b.UsuarioId == userDual.Id);
                Assert.True(bDualFinal.SaldoRetenido <= 100000m,
                    $"[Ciclo {ciclo}] Inconsistencia: SaldoRetenido (${bDualFinal.SaldoRetenido}) excedió el total disponible ($100.000).");
                Assert.True(bDualFinal.SaldoDisponible >= 0m,
                    $"[Ciclo {ciclo}] Inconsistencia: SaldoDisponible quedó en negativo (${bDualFinal.SaldoDisponible}).");

                // Helper de conciliación financiera contable estricta
                await AssertConciliacionAsync(assertContext);
            }

            _output.WriteLine($"[ESCENARIO B COMPLETADO - 20 CICLOS] Total 200 OK: {total200}, Total 409 Conflict: {total409}, Total 400 BadRequest: {total400}");
        }

        // ========================================================================
        // HELPER DE CONCILIACIÓN FINANCIERA (RÚBRICA B07)
        // Valida para TODAS las billeteras de la base de datos:
        // 1. Total = Retenido + Disponible
        // 2. Total = Σ(DEPOSITO + COBRO − PAGO)
        // 3. Retenido = Σ(RETENCION − LIBERACION − PAGO)
        // ========================================================================
        public static async Task AssertConciliacionAsync(SubastaDbContext context)
        {
            var billeteras = await context.Billeteras.AsNoTracking().ToListAsync();
            var todosLedgers = await context.TransactionLedgers.AsNoTracking().ToListAsync();

            foreach (var b in billeteras)
            {
                // Regla 1: Invariante fundamental de saldos
                Assert.Equal(b.SaldoTotal, b.SaldoRetenido + b.SaldoDisponible);

                var ledgersBilletera = todosLedgers.Where(l => l.BilleteraId == b.Id).ToList();

                // Regla 2: Total = Σ(DEPOSITO + COBRO − PAGO)
                var depositos = ledgersBilletera.Where(l => l.Tipo == TipoTransaccion.DEPOSITO).Sum(l => l.Monto);
                var cobros = ledgersBilletera.Where(l => l.Tipo == TipoTransaccion.COBRO).Sum(l => l.Monto);
                var pagos = ledgersBilletera.Where(l => l.Tipo == TipoTransaccion.PAGO).Sum(l => l.Monto);

                decimal totalEsperado = depositos + cobros - pagos;
                Assert.Equal(totalEsperado, b.SaldoTotal);

                // Regla 3: Retenido = Σ(RETENCION − LIBERACION − PAGO)
                var retenciones = ledgersBilletera.Where(l => l.Tipo == TipoTransaccion.RETENCION).Sum(l => l.Monto);
                var liberaciones = ledgersBilletera.Where(l => l.Tipo == TipoTransaccion.LIBERACION).Sum(l => l.Monto);

                decimal retenidoEsperado = retenciones - liberaciones - pagos;
                Assert.Equal(retenidoEsperado, b.SaldoRetenido);
            }
        }
    }
}
