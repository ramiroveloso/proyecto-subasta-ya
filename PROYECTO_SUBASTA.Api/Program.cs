using Microsoft.EntityFrameworkCore;
using PROYECTO_SUBASTA.Api.Middlewares;
using PROYECTO_SUBASTA.Application.Repositories;
using PROYECTO_SUBASTA.Application.UseCases;
using PROYECTO_SUBASTA.Domain.Entities;
using PROYECTO_SUBASTA.Infrastructure.Data;
using PROYECTO_SUBASTA.Infrastructure.Repositories;

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

builder.Services.AddScoped<CategoriaUseCases>();
builder.Services.AddScoped<SubastaUseCases>();
builder.Services.AddScoped<UsuarioUseCases>();
builder.Services.AddScoped<IBilleteraService, BilleteraService>();

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
        // Subasta 4: FINALIZADA (cerrada con ganador)
        // Subasta 5: DESIERTA (cerrada sin ofertas)
        // ========================================================================
        var ahoraUtc = DateTime.UtcNow;

        var subasta1 = context.Subastas.Find(1);
        if (subasta1 != null && (subasta1.FechaFin <= ahoraUtc || subasta1.Estado != "ACTIVA"))
        {
            subasta1.FechaInicio = ahoraUtc.AddMinutes(-20);
            subasta1.FechaFin = ahoraUtc.AddMinutes(25);
            subasta1.Estado = "ACTIVA";
        }

        var subasta2 = context.Subastas.Find(2);
        if (subasta2 != null && (subasta2.FechaFin <= ahoraUtc || subasta2.Estado != "ACTIVA"))
        {
            subasta2.FechaInicio = ahoraUtc.AddMinutes(-58);
            subasta2.FechaFin = ahoraUtc.AddSeconds(120);
            subasta2.Estado = "ACTIVA";
        }

        var subasta3 = context.Subastas.Find(3);
        if (subasta3 != null && subasta3.FechaInicio <= ahoraUtc)
        {
            subasta3.FechaInicio = ahoraUtc.AddHours(24);
            subasta3.FechaFin = ahoraUtc.AddHours(48);
            subasta3.Estado = "PROGRAMADA";
        }

        var subasta4 = context.Subastas.Find(4);
        if (subasta4 != null && subasta4.FechaFin > ahoraUtc)
        {
            subasta4.FechaInicio = ahoraUtc.AddDays(-2);
            subasta4.FechaFin = ahoraUtc.AddHours(-2);
            subasta4.Estado = "FINALIZADA";
            subasta4.GanadorId = 2;
            subasta4.PrecioFinal = 25000.00m;
        }

        var subasta5 = context.Subastas.Find(5);
        if (subasta5 != null && subasta5.FechaFin > ahoraUtc)
        {
            subasta5.FechaInicio = ahoraUtc.AddDays(-3);
            subasta5.FechaFin = ahoraUtc.AddDays(-1);
            subasta5.Estado = "DESIERTA";
        }

        // Sincronizar fechas de pujas y movimientos demo de las subastas activas
        var puja1 = context.Pujas.Find(1);
        if (puja1 != null) puja1.FechaCreacion = ahoraUtc.AddMinutes(-15);
        var puja2 = context.Pujas.Find(2);
        if (puja2 != null) puja2.FechaCreacion = ahoraUtc.AddMinutes(-5);

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