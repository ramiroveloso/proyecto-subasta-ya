using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PROYECTO_SUBASTA.Api.Middlewares;
using PROYECTO_SUBASTA.Api.Workers;
using PROYECTO_SUBASTA.Application.Commands.Billeteras;
using PROYECTO_SUBASTA.Application.Commands.Categorias;
using PROYECTO_SUBASTA.Application.Commands.Subastas;
using PROYECTO_SUBASTA.Application.Commands.Usuarios;
using PROYECTO_SUBASTA.Application.Configuration;
using PROYECTO_SUBASTA.Application.DTOs;
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
    })
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var errores = context.ModelState
                .Where(e => e.Value?.Errors.Count > 0)
                .ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value!.Errors.Select(err => string.IsNullOrWhiteSpace(err.ErrorMessage) ? "Valor inválido." : err.ErrorMessage).ToArray()
                );

            var errorDto = new ErrorResponseDto
            {
                StatusCode = StatusCodes.Status400BadRequest,
                Mensaje = "Uno o más campos presentan errores de validación.",
                Timestamp = DateTime.UtcNow,
                Errores = errores
            };

            return new BadRequestObjectResult(errorDto);
        };
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "SubastaYa API",
        Version = "v1",
        Description = "API RESTful para el sistema de subastas en tiempo real SubastaYa (UNAJ)."
    });

    var xmlFilename = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFilename);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }
});

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
ServerVersion serverVersion;

if (EF.IsDesignTime)
{
    // Solo en tiempo de diseño (ej: dotnet ef migrations add / script) se permite fallback
    // para posibilitar el scaffolding del modelo si no se dispone de un servidor activo.
    try
    {
        serverVersion = !string.IsNullOrWhiteSpace(connectionString)
            ? ServerVersion.AutoDetect(connectionString)
            : new MySqlServerVersion(new Version(8, 0, 36));
    }
    catch
    {
        serverVersion = new MySqlServerVersion(new Version(8, 0, 36));
    }
}
else
{
    // En ejecución normal (dotnet run / producción), la aplicación DEBE conectarse a la base real.
    // Si la cadena está vacía o el servidor es inaccesible, falla de forma inmediata y visible en el arranque.
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        throw new InvalidOperationException("La cadena de conexión 'DefaultConnection' no fue encontrada ni configurada.");
    }
    serverVersion = ServerVersion.AutoDetect(connectionString);
}

builder.Services.AddDbContext<SubastaDbContext>(options =>
    options.UseMySql(
        connectionString,
        serverVersion,
        mySqlOptions =>
        {
        }
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

builder.Services.AddScoped<IBilleteraService, BilleteraService>();
builder.Services.AddScoped<IDemoDataService, DemoDataService>();
builder.Services.Configure<AntiSnipingOptions>(builder.Configuration.GetSection(AntiSnipingOptions.SectionName));

// Handlers CQRS (Commands)
builder.Services.AddScoped<RegistrarPujaCommandHandler>();
builder.Services.AddScoped<CrearSubastaCommandHandler>();
builder.Services.AddScoped<CrearCategoriaCommandHandler>();
builder.Services.AddScoped<CrearUsuarioCommandHandler>();
builder.Services.AddScoped<CargarSaldoCommandHandler>();
builder.Services.AddScoped<IniciarSesionCommandHandler>();
builder.Services.AddScoped<CerrarSubastaCommandHandler>();
builder.Services.AddScoped<ActivarSubastasProgramadasCommandHandler>();
builder.Services.AddScoped<EjecutarCierresCommandHandler>();

// Handlers CQRS (Queries)
builder.Services.AddScoped<ObtenerSubastasActivasQueryHandler>();
builder.Services.AddScoped<ObtenerSubastaPorIdQueryHandler>();
builder.Services.AddScoped<ObtenerSubastasPorCerrarQueryHandler>();
builder.Services.AddScoped<ObtenerCategoriasQueryHandler>();
builder.Services.AddScoped<ObtenerCategoriaPorIdQueryHandler>();
builder.Services.AddScoped<ObtenerBilleteraPorUsuarioQueryHandler>();
builder.Services.AddScoped<ObtenerMovimientosQueryHandler>();
builder.Services.AddScoped<ObtenerUsuariosQueryHandler>();
builder.Services.AddScoped<ObtenerUsuarioPorIdQueryHandler>();
builder.Services.AddScoped<ObtenerLogsAuditoriaQueryHandler>();
builder.Services.AddScoped<ObtenerActividadesUsuarioQueryHandler>();

// ========================================================================
// 2.3 PROCESO EN SEGUNDO PLANO (BACKGROUND WORKER)
// ========================================================================
var workerHabilitado = builder.Configuration.GetValue<bool?>("Worker:Habilitado") ?? true;
if (workerHabilitado)
{
    builder.Services.AddHostedService<SubastaBackgroundWorker>();
}

var app = builder.Build();

// ========================================================================
// APLICACIÓN DE MIGRACIONES Y SERVICIO DEMO
// ========================================================================
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();
    try
    {
        var context = services.GetRequiredService<SubastaDbContext>();
        context.Database.Migrate();

        var demoDataService = services.GetRequiredService<IDemoDataService>();
        await demoDataService.ReubicarFechasDemoSiCorrespondeAsync();
    }
    catch (Exception ex)
    {
        logger.LogCritical(ex, "Fallo crítico al aplicar migraciones o inicializar la base de datos.");
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

public partial class Program { }
