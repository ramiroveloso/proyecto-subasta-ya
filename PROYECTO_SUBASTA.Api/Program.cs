using Microsoft.EntityFrameworkCore;
using PROYECTO_SUBASTA.Api.Middlewares;
using PROYECTO_SUBASTA.Api.Workers;
using PROYECTO_SUBASTA.Application.Commands.Billeteras;
using PROYECTO_SUBASTA.Application.Commands.Subastas;
using PROYECTO_SUBASTA.Application.Commands.Usuarios;
using PROYECTO_SUBASTA.Application.Queries.Billeteras;
using PROYECTO_SUBASTA.Application.Queries.Subastas;
using PROYECTO_SUBASTA.Application.Queries.Usuarios;
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
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

builder.Services.AddScoped<IAuditoriaService, AuditoriaService>();
builder.Services.AddScoped<IAdjudicacionService, AdjudicacionService>();

builder.Services.AddScoped<CategoriaUseCases>();
builder.Services.AddScoped<SubastaUseCases>();
builder.Services.AddScoped<UsuarioUseCases>();
builder.Services.AddScoped<IBilleteraService, BilleteraService>();

// Handlers CQRS (Commands)
builder.Services.AddScoped<RegistrarPujaCommandHandler>();
builder.Services.AddScoped<CrearSubastaCommandHandler>();
builder.Services.AddScoped<CargarSaldoCommandHandler>();
builder.Services.AddScoped<IniciarSesionCommandHandler>();

// Handlers CQRS (Queries)
builder.Services.AddScoped<ObtenerSubastasActivasQueryHandler>();
builder.Services.AddScoped<ObtenerSubastaPorIdQueryHandler>();
builder.Services.AddScoped<ObtenerBilleteraPorUsuarioQueryHandler>();
builder.Services.AddScoped<ObtenerMovimientosQueryHandler>();
builder.Services.AddScoped<ObtenerUsuariosQueryHandler>();
builder.Services.AddScoped<ObtenerUsuarioPorIdQueryHandler>();

builder.Services.AddScoped<DemoDataService>();

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

        var demoService = services.GetRequiredService<DemoDataService>();
        demoService.Initialize();
    }
    catch (Exception ex)
    {
        // Fail hard on database error
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Ocurrió un error al aplicar las migraciones o los datos semilla.");
        throw;
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
