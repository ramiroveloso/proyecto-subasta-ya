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

// Registra los controladores de la API para que el framework sepa cómo enrutar las peticiones HTTP entrantes.
builder.Services.AddControllers();

// Habilita la exploración de las rutas de la API, necesario para que Swagger pueda descubrir los endpoints.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// --- CONFIGURACIÓN DE CORS ---
// Habilitamos una política abierta para permitir peticiones desde frontends locales o externos en desarrollo.
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
// Extraemos la cadena de conexión desde appsettings.json para no exponer credenciales directamente en el código fuente.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

// Registramos el contexto de base de datos con un ciclo de vida "Scoped" (por defecto). 
// Utilizamos ServerVersion.AutoDetect para que Pomelo optimice las consultas SQL basándose en la versión exacta del motor local.
builder.Services.AddDbContext<SubastaDbContext>(options =>
    options.UseMySql(
        connectionString,
        ServerVersion.AutoDetect(connectionString)
    ));

// ========================================================================
// INYECCIÓN DE DEPENDENCIAS DE PERSISTENCIA Y CASOS DE USO (Clean Architecture)
// ========================================================================

// --- REPOSITORIOS (Persistencia) ---
// Registramos el repositorio genérico y las implementaciones específicas contra sus abstracciones.
builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
builder.Services.AddScoped<ICategoriaRepository, CategoriaRepository>();
builder.Services.AddScoped<ISubastaRepository, SubastaRepository>();
builder.Services.AddScoped<IBilleteraRepository, BilleteraRepository>();

// --- CASOS DE USO Y SERVICIOS DE APLICACIÓN ---
// Asociamos la lógica de negocio al contenedor IoC con ciclo de vida "Scoped" 
// para asegurar que las operaciones se ejecuten de manera aislada por cada solicitud web.
builder.Services.AddScoped<CategoriaUseCases>();
builder.Services.AddScoped<SubastaUseCases>();
builder.Services.AddScoped<UsuarioUseCases>();
builder.Services.AddScoped<IBilleteraService, BilleteraService>();

var app = builder.Build();

// ========================================================================
// 2. CONFIGURACIÓN DEL PIPELINE DE PETICIONES HTTP (MIDDLEWARES)
// ========================================================================

// Exponemos la interfaz gráfica de Swagger únicamente en el entorno de desarrollo 
// para facilitar nuestras pruebas locales sin arriesgar la seguridad en un eventual paso a producción.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Activamos la política de CORS definida previamente en el contenedor de servicios.
app.UseCors("AllowAll");

// Redirige automáticamente todo el tráfico HTTP no seguro hacia HTTPS para proteger los datos en tránsito.
app.UseHttpsRedirection();

// Registra el middleware de autorización. Aunque ahora no lo usemos, deja la arquitectura preparada 
// para la futura validación de usuarios (ej. mediante tokens JWT).
app.UseAuthorization();

// Enlaza las rutas (ej. [Route("api/[controller]")]) con los controladores correspondientes.
app.MapControllers();

// Inicia el servidor web integrado (Kestrel) y comienza a escuchar peticiones.
app.Run();