using Microsoft.EntityFrameworkCore;
using PROYECTO_SUBASTA.Api.Middlewares;
using PROYECTO_SUBASTA.Api.Workers;
using PROYECTO_SUBASTA.Application.Repositories;
using PROYECTO_SUBASTA.Application.UseCases;
using PROYECTO_SUBASTA.Domain.Entities;
using PROYECTO_SUBASTA.Infrastructure.Data;
using PROYECTO_SUBASTA.Infrastructure.Repositories;
using PROYECTO_SUBASTA.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

// ========================================================================
// 1. REGISTRO DE SERVICIOS (CONTENEDOR DE INYECCIÓN DE DEPENDENCIAS)
// ========================================================================
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.Converters.Add(new UtcDateTimeJsonConverter());
        options.JsonSerializerOptions.Converters.Add(new NullableUtcDateTimeJsonConverter());
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// --- CONFIGURACIÓN DE CORS ---
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// --- CONFIGURACIÓN DE BASE DE DATOS (MYSQL) ---
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<SubastaDbContext>(options =>
    options.UseMySql(
        connectionString,
        ServerVersion.AutoDetect(connectionString),
        mySqlOptions => mySqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(5),
            errorNumbersToAdd: null
        )
    ));

// ========================================================================
// INYECCIÓN DE DEPENDENCIAS DE PERSISTENCIA Y CASOS DE USO (Clean Architecture)
// ========================================================================
builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
builder.Services.AddScoped<ICategoriaRepository, CategoriaRepository>();
builder.Services.AddScoped<ISubastaRepository, SubastaRepository>();
builder.Services.AddScoped<IBilleteraRepository, BilleteraRepository>();

builder.Services.AddScoped<IAuditoriaService, AuditoriaService>();
builder.Services.AddScoped<IAdjudicacionService, AdjudicacionService>();

builder.Services.AddScoped<CategoriaUseCases>();
builder.Services.AddScoped<SubastaUseCases>();
builder.Services.AddScoped<UsuarioUseCases>();
builder.Services.AddScoped<IBilleteraService, BilleteraService>();

// ========================================================================
// 2.3 PROCESO EN SEGUNDO PLANO (BACKGROUND WORKER)
// ========================================================================
builder.Services.AddHostedService<SubastaBackgroundWorker>();

var app = builder.Build();

// ========================================================================
// APLICACIÓN DE MIGRACIONES Y DATOS SEMILLA
// ========================================================================
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<SubastaDbContext>();
        context.Database.Migrate();

        if (!context.Categorias.Any())
        {
            context.Categorias.AddRange(
                new Categoria { Id = 1, Nombre = "Tecnología" },
                new Categoria { Id = 2, Nombre = "Coleccionables" },
                new Categoria { Id = 3, Nombre = "Indumentaria" },
                new Categoria { Id = 4, Nombre = "Vehículos" }
            );
            context.SaveChanges();
        }

        // ========================================================================
        // SINCRONIZACIÓN Y RENOVACIÓN DE SUBASTAS DEMO EN UTC
        // Garantiza que las subastas de prueba (IDs 1 a 5) permanezcan en su estado previsto:
        // Subasta 1: ACTIVA (cierra en ~25 min)
        // Subasta 2: ACTIVA crítica (cierra en ~2 min para anti-sniping)
        // Subasta 3: PROGRAMADA (+24 hs)
        // Subasta 4: FINALIZADA (cerrada con ganador: Usuario 2 "Comprador Líder")
        // Subasta 5: DESIERTA (cerrada sin ofertas)
        // ========================================================================
        var ahoraUtc = DateTime.UtcNow;

        var subasta1 = context.Subastas.Include(s => s.Pujas).FirstOrDefault(s => s.Id == 1);
        if (subasta1 != null)
        {
            if (subasta1.FechaFin <= ahoraUtc || subasta1.Estado != "ACTIVA")
            {
                subasta1.FechaInicio = ahoraUtc.AddMinutes(-20);
                subasta1.FechaFin = ahoraUtc.AddMinutes(25);
                subasta1.Estado = "ACTIVA";
            }
            if (subasta1.Version == 0)
            {
                subasta1.Version = (uint)(subasta1.Pujas?.Count > 0 ? subasta1.Pujas.Count + 1 : 3);
            }
        }

        var subasta2 = context.Subastas.Include(s => s.Pujas).FirstOrDefault(s => s.Id == 2);
        if (subasta2 != null)
        {
            if (subasta2.FechaFin <= ahoraUtc || subasta2.Estado != "ACTIVA")
            {
                subasta2.FechaInicio = ahoraUtc.AddMinutes(-58);
                subasta2.FechaFin = ahoraUtc.AddSeconds(120);
                subasta2.Estado = "ACTIVA";
            }
            if (subasta2.Version == 0)
            {
                subasta2.Version = (uint)(subasta2.Pujas?.Count > 0 ? subasta2.Pujas.Count + 1 : 1);
            }
        }

        var subasta3 = context.Subastas.Find(3);
        if (subasta3 != null)
        {
            if (subasta3.FechaInicio <= ahoraUtc)
            {
                subasta3.FechaInicio = ahoraUtc.AddHours(24);
                subasta3.FechaFin = ahoraUtc.AddHours(48);
                subasta3.Estado = "PROGRAMADA";
            }
            if (subasta3.Version == 0)
            {
                subasta3.Version = 1;
            }
        }

        // Subasta 4 (Dato Semilla Obligatorio: Finalizada con ganador Comprador Líder)
        var subasta4 = context.Subastas.Find(4);
        if (subasta4 == null)
        {
            subasta4 = new Subasta
            {
                Id = 4,
                VendedorId = 1,
                CategoriaId = 3,
                Titulo = "Campera de Cuero Vintage",
                Descripcion = "Indumentaria clásica",
                UrlImagen = "https://images.unsplash.com/photo-1551028719-00167b16eac5?w=500",
                PrecioBase = 20000.00m,
                IncrementoMinimo = 2000.00m,
                FechaInicio = ahoraUtc.AddDays(-2),
                FechaFin = ahoraUtc.AddHours(-2),
                Estado = "FINALIZADA",
                GanadorId = 2, // Comprador Líder
                PrecioFinal = 25000.00m,
                Version = 2
            };
            context.Subastas.Add(subasta4);
        }
        else
        {
            subasta4.FechaInicio = ahoraUtc.AddDays(-2);
            subasta4.FechaFin = ahoraUtc.AddHours(-2);
            subasta4.Estado = "FINALIZADA";
            subasta4.GanadorId = 2; // Usuario con distinción de Comprador (Comprador Líder)
            subasta4.PrecioFinal = 25000.00m;
            if (subasta4.Version == 0) subasta4.Version = 2;
        }

        // Subasta 5 (Dato Semilla Obligatorio: DESIERTA sin ofertas)
        var subasta5 = context.Subastas.Find(5);
        if (subasta5 == null)
        {
            subasta5 = new Subasta
            {
                Id = 5,
                VendedorId = 1,
                CategoriaId = 4,
                Titulo = "Repuesto Clásico de Vehículo",
                Descripcion = "Sin ofertas registradas",
                UrlImagen = "https://images.unsplash.com/photo-1486006920555-c77dce18193b?w=500",
                PrecioBase = 80000.00m,
                IncrementoMinimo = 10000.00m,
                FechaInicio = ahoraUtc.AddDays(-3),
                FechaFin = ahoraUtc.AddDays(-1),
                Estado = "DESIERTA",
                GanadorId = null,
                PrecioFinal = null,
                Version = 1
            };
            context.Subastas.Add(subasta5);
        }
        else
        {
            subasta5.FechaInicio = ahoraUtc.AddDays(-3);
            subasta5.FechaFin = ahoraUtc.AddDays(-1);
            subasta5.Estado = "DESIERTA";
            subasta5.GanadorId = null;
            subasta5.PrecioFinal = null;
            if (subasta5.Version == 0) subasta5.Version = 1;

            // Garantizar que la subasta 5 no tenga pujas asociadas
            var pujasSub5 = context.Pujas.Where(p => p.SubastaId == 5).ToList();
            if (pujasSub5.Any())
            {
                context.Pujas.RemoveRange(pujasSub5);
            }
        }

        // Garantizar que ninguna subasta en la base de datos quede con Version = 0
        var todasLasSubastas = context.Subastas.Include(s => s.Pujas).ToList();
        foreach (var s in todasLasSubastas)
        {
            if (s.Version == 0)
            {
                s.Version = (uint)(s.Pujas != null && s.Pujas.Count > 0 ? s.Pujas.Count + 1 : 1);
            }
        }

        // Sincronizar fechas de pujas y movimientos demo de las subastas activas
        var puja1 = context.Pujas.Find(1);
        if (puja1 != null) puja1.FechaCreacion = ahoraUtc.AddMinutes(-15);
        var puja2 = context.Pujas.Find(2);
        if (puja2 != null) puja2.FechaCreacion = ahoraUtc.AddMinutes(-5);

        // Garantizar puja ganadora para Subasta 4
        var pujaGanadora4 = context.Pujas.FirstOrDefault(p => p.SubastaId == 4);
        if (pujaGanadora4 == null)
        {
            context.Pujas.Add(new Puja
            {
                Id = 3,
                SubastaId = 4,
                UsuarioId = 2, // Comprador Líder
                Monto = 25000.00m,
                FechaCreacion = ahoraUtc.AddDays(-1)
            });
        }

        // Garantizar asientos contables de liquidación (PAGO / COBRO) para Subasta 4
        if (!context.TransactionLedgers.Any(t => t.SubastaId == 4 && t.Tipo == TipoTransaccion.PAGO))
        {
            context.TransactionLedgers.Add(new TransactionLedger
            {
                BilleteraId = 2, // Billetera Comprador Líder
                Tipo = TipoTransaccion.PAGO,
                Monto = 25000.00m,
                Fecha = ahoraUtc.AddHours(-2),
                SubastaId = 4
            });
        }

        if (!context.TransactionLedgers.Any(t => t.SubastaId == 4 && t.Tipo == TipoTransaccion.COBRO))
        {
            context.TransactionLedgers.Add(new TransactionLedger
            {
                BilleteraId = 1, // Billetera Vendedor Test
                Tipo = TipoTransaccion.COBRO,
                Monto = 25000.00m,
                Fecha = ahoraUtc.AddHours(-2),
                SubastaId = 4
            });
        }

        // Logs de auditoría semilla si la tabla está vacía
        if (!context.LogsAuditoria.Any())
        {
            context.LogsAuditoria.AddRange(
                new LogAuditoria
                {
                    Accion = "CAMBIO_ESTADO_SUBASTA",
                    Detalle = "Subasta #4 ('Campera de Cuero Vintage') finalizada y adjudicada por Background Worker. Ganador: Usuario #2 (Comprador Líder) con oferta de $25.000,00.",
                    UsuarioId = 2,
                    FechaRegistro = ahoraUtc.AddHours(-2)
                },
                new LogAuditoria
                {
                    Accion = "VENTA_REGISTRADA",
                    Detalle = "Liquidación final de Subasta #4: Saldo retenido ($25.000,00) de Comprador Líder transferido a Billetera de Vendedor Test.",
                    UsuarioId = 1,
                    FechaRegistro = ahoraUtc.AddHours(-2)
                },
                new LogAuditoria
                {
                    Accion = "CAMBIO_ESTADO_SUBASTA",
                    Detalle = "Subasta #5 ('Repuesto Clásico de Vehículo') declarada DESIERTA por el Background Worker al vencer el tiempo sin ofertas.",
                    UsuarioId = null,
                    FechaRegistro = ahoraUtc.AddDays(-1)
                }
            );
        }

        context.SaveChanges();
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Ocurrió un error al aplicar las migraciones o los datos semilla.");
    }
}

// ========================================================================
// 2. CONFIGURACIÓN DEL PIPELINE DE PETICIONES HTTP (MIDDLEWARES)
// ========================================================================

// 1. REGISTRAR EL MIDDLEWARE DE CONCURRENCIA AQUÍ (Al inicio del pipeline)
app.UseMiddleware<ExceptionMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowAll");

var frontendPath = Path.Combine(builder.Environment.ContentRootPath, "Frontend");
if (Directory.Exists(frontendPath))
{
    var fileServerOptions = new FileServerOptions
    {
        FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(frontendPath),
        RequestPath = "",
        EnableDefaultFiles = true
    };
    fileServerOptions.DefaultFilesOptions.DefaultFileNames.Clear();
    fileServerOptions.DefaultFilesOptions.DefaultFileNames.Add("index.html");
    app.UseFileServer(fileServerOptions);
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();

// ========================================================================
// CONVERTIDORES JSON PARA HOMOGENEIZAR FECHAS EN UTC
// ========================================================================
public class UtcDateTimeJsonConverter : System.Text.Json.Serialization.JsonConverter<DateTime>
{
    public override DateTime Read(ref System.Text.Json.Utf8JsonReader reader, Type typeToConvert, System.Text.Json.JsonSerializerOptions options)
    {
        var dt = reader.GetDateTime();
        return dt.Kind == DateTimeKind.Utc ? dt : DateTime.SpecifyKind(dt.ToUniversalTime(), DateTimeKind.Utc);
    }

    public override void Write(System.Text.Json.Utf8JsonWriter writer, DateTime value, System.Text.Json.JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ"));
    }
}

public class NullableUtcDateTimeJsonConverter : System.Text.Json.Serialization.JsonConverter<DateTime?>
{
    public override DateTime? Read(ref System.Text.Json.Utf8JsonReader reader, Type typeToConvert, System.Text.Json.JsonSerializerOptions options)
    {
        if (reader.TokenType == System.Text.Json.JsonTokenType.Null) return null;
        var dt = reader.GetDateTime();
        return dt.Kind == DateTimeKind.Utc ? dt : DateTime.SpecifyKind(dt.ToUniversalTime(), DateTimeKind.Utc);
    }

    public override void Write(System.Text.Json.Utf8JsonWriter writer, DateTime? value, System.Text.Json.JsonSerializerOptions options)
    {
        if (!value.HasValue)
        {
            writer.WriteNullValue();
        }
        else
        {
            writer.WriteStringValue(value.Value.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ"));
        }
    }
}
