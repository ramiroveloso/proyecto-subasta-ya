using Microsoft.EntityFrameworkCore;
using PROYECTO_SUBASTA.Infraestructure;
using PROYECTO_SUBASTA.Repositories;

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

// Vinculamos la abstracción del repositorio de categorías con su implementación concreta. 
// Esto desacopla las reglas de negocio de la tecnología de base de datos (Principio de Inversión de Dependencias - DIP),
// permitiendo aislar el dominio y facilitar las pruebas unitarias. El ciclo de vida "Scoped" 
// garantiza una única instancia por cada petición HTTP, preservando la consistencia transaccional.
builder.Services.AddScoped<ICategoriaRepository, CategoriaRepository>();

// Asociamos la interfaz de subastas con su infraestructura de datos subyacente. 
// Al programar contra esta abstracción, protegemos los Casos de Uso frente a cambios 
// en el motor de persistencia y aseguramos que todas las operaciones de la solicitud compartan 
// el mismo contexto de base de datos de manera segura.
builder.Services.AddScoped<ISubastaRepository, SubastaRepository>();

// ----------------------------------------------

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