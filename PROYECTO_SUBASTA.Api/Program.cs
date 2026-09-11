using Microsoft.EntityFrameworkCore;
using PROYECTO_SUBASTA.Domain.Entities;
using PROYECTO_SUBASTA.Application.UseCases;
using PROYECTO_SUBASTA.Application.Repositories;
using PROYECTO_SUBASTA.Infrastructure.Data;
using PROYECTO_SUBASTA.Infrastructure.Repositories;

var builder = WebApplication.CreateBuilder(args);

// ========================================================================
// 1. REGISTRO DE SERVICIOS (CONTENEDOR DE INYECCIÓN DE DEPENDENCIAS)
// ========================================================================
builder.Services.AddControllers();
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
        ServerVersion.AutoDetect(connectionString)
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
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowAll");
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();