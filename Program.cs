using Microsoft.EntityFrameworkCore;
using PROYECTO_SUBASTA.Infraestructure;
using PROYECTO_SUBASTA.Repositories;
using PROYECTO_SUBASTA.UseCases;

var builder = WebApplication.CreateBuilder(args);

// ========================================================================
// 1. REGISTRO DE SERVICIOS (CONTENEDOR DE INYECCIÓN DE DEPENDENCIAS)
// ========================================================================

// Registra los controladores de la API para que el framework sepa cómo enrutar las peticiones HTTP entrantes.
builder.Services.AddControllers();

// Habilita la exploración de las rutas de la API, necesario para que Swagger pueda descubrir los endpoints.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

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
// INYECCIÓN DE DEPENDENCIAS DE PERSISTENCIA (Clean Architecture)
// ========================================================================

// Registramos el servicio de aplicación para categorías en el contenedor IoC con un ciclo de vida "Scoped". 
// Esto permite que el controlador reciba la lógica de negocio desacoplada y que la instancia 
// persista durante toda la duración de la petición HTTP actual, compartiendo el mismo contexto de base de datos.
// 1. Primero registramos las implementaciones de los repositorios contra sus abstracciones
builder.Services.AddScoped<ICategoriaRepository, CategoriaRepository>();
builder.Services.AddScoped<ISubastaRepository, SubastaRepository>();
builder.Services.AddScoped<IPujaRepository, PujaRepository>();

// Asociamos el caso de uso de subastas al contenedor de dependencias. Al registrarlo como "Scoped", 
// aseguramos que todas las validaciones de negocio y operaciones de esta capa se ejecuten de manera aislada 
// y coordinada por cada solicitud web entrante, cumpliendo con los principios de inversión de control (IoC).
// 2. Luego registramos los Casos de Uso que dependen de dichos repositorios
builder.Services.AddScoped<CategoriaUseCases>();
builder.Services.AddScoped<SubastaUseCases>();
builder.Services.AddScoped<PujaUseCases>();

// ----------------------------------------------

// Repositorios
builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
builder.Services.AddScoped<IBilleteraRepository, BilleteraRepository>();

// Servicios de Negocio
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

// Redirige automáticamente todo el tráfico HTTP no seguro hacia HTTPS para proteger los datos en tránsito.
app.UseHttpsRedirection();

// Registra el middleware de autorización. Aunque ahora no lo usemos, deja la arquitectura preparada 
// para la futura validación de usuarios (ej. mediante tokens JWT).
app.UseAuthorization();

// Enlaza las rutas (ej. [Route("api/[controller]")]) con los controladores correspondientes.
app.MapControllers();

// Inicia el servidor web integrado (Kestrel) y comienza a escuchar peticiones.
app.Run();