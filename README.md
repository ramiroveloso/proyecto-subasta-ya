# SubastaYa - Plataforma de Subastas en Vivo

Sistema de subastas en tiempo real desarrollado bajo los principios de **Arquitectura Limpia (Clean Architecture)** en **.NET 8** con **Entity Framework Core (Code-First / MySQL)** y un patrón **CQRS custom** (Commands y Queries con Handlers concretos, sin dependencias a MediatR).

---

## 📋 Requisitos Previos

Asegúrate de contar con las siguientes herramientas instaladas en tu entorno de desarrollo:
* **[.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)** o superior.
* **[MySQL Server 8.0+](https://dev.mysql.com/downloads/mysql/)** configurado y activo en el puerto 3306 (o el configurado en la cadena).

---

## ⚙️ Configuración de la Cadena de Conexión

Para evitar exponer credenciales en archivos versionados, `PROYECTO_SUBASTA.Api/appsettings.json` contiene únicamente una plantilla de ejemplo con `Pwd=CAMBIAR;`. Configura tu cadena de conexión real mediante **User Secrets** (recomendado en desarrollo) o variable de entorno:

### Opción 1: User Secrets de .NET (Recomendada en desarrollo local)
Desde la raíz del repositorio, ejecuta:
```bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=127.0.0.1;Port=3306;Database=SubastaYaDb;Uid=root;Pwd=1123677295;" --project PROYECTO_SUBASTA.Api
```

### Opción 2: Variable de entorno
* **En Windows (PowerShell):**
  ```powershell
  $env:ConnectionStrings__DefaultConnection="Server=127.0.0.1;Port=3306;Database=SubastaYaDb;Uid=root;Pwd=TU_PASSWORD;"
  ```
* **En Linux / macOS (Bash):**
  ```bash
  export ConnectionStrings__DefaultConnection="Server=127.0.0.1;Port=3306;Database=SubastaYaDb;Uid=root;Pwd=TU_PASSWORD;"
  ```

---

## 🗄️ Migraciones y Seed Idempotente en Arranque

El sistema cuenta con inicialización automática y resiliente al iniciar la API (`DatabaseInitializer`):
1. **Migraciones automáticas:** En cada arranque se invoca `Database.MigrateAsync()`, aplicando cualquier migración pendiente de Entity Framework Core sobre la base de datos MySQL (incluyendo esquemas, índices únicos, FKs y triggers de inmutabilidad de auditoría).
2. **Seed idempotente:** Solo si la base de datos no contiene usuarios (`!await ctx.Usuarios.AnyAsync()`), se insertan automáticamente los perfiles demo con fechas relativas a UTC, subastas en diferentes estados, balances contables coherentes entre billetera y libro mayor (ledger) y registros de auditoría. Si la base ya contiene datos, el seed no se repite ni altera saldos existentes.

No se requiere ejecutar manualmente comandos de `dotnet ef database update` para iniciar.

---

## ▶️ Ejecución del Proyecto

### 1. Iniciar la API y el BackgroundWorker
Para iniciar el servidor backend:
```bash
dotnet run --project PROYECTO_SUBASTA.Api
```
El servicio de segundo plano (`SubastaBackgroundWorker`) está registrado como un `IHostedService` dentro del mismo proceso de la API. Se ejecuta en ciclos periódicos evaluando de forma concurrente y atómica la activación de subastas programadas y el cierre/liquidación de subastas vencidas.

### 2. Documentación Interactiva (Swagger / OpenAPI)
Con la API en ejecución, accede en tu navegador a:
```
http://localhost:<puerto>/swagger
```
Swagger queda habilitado por defecto según la configuración en `appsettings.json` (`"Swagger": { "Enabled": true }`).

### 3. Frontend Web Corporativo
La API sirve los archivos estáticos del frontend directamente desde `PROYECTO_SUBASTA.Api/Frontend`:
* Accede desde el navegador a la raíz `http://localhost:<puerto>/` o `http://localhost:<puerto>/index.html`.
* La interfaz interactúa en tiempo real con los endpoints REST del backend (sin mocks ni datos simulados), reflejando la concurrencia optimista, extensiones anti-sniping y saldos retenidos en Escrow.

---

## 👥 Usuarios Demo y Credenciales

El sistema cuenta con 4 usuarios sembrados automáticamente durante el primer arranque con la contraseña demo: **`1234`**

| ID | Nombre | Email / Usuario | Contraseña | Rol y Balance Inicial |
|:---|:---|:---|:---|:---|
| **1** | Vendedor Test | `vendedor@test.com` | `1234` | Vendedor de artículos demo. Saldo: $25.000 |
| **2** | Comprador Líder | `comprador1@test.com` | `1234` | Postor líder en Subasta 1. Saldo disponible: $80.000 / Retenido: $45.000 |
| **3** | Comprador Habilitado | `comprador2@test.com` | `1234` | Postor con fondos suficientes para pujar. Saldo disponible: $200.000 |
| **4** | Usuario Sin Fondos | `sinfondos@test.com` | `1234` | Postor sin saldo ($0) para validar rechazos de oferta |

---

## 🔄 Reiniciar los Datos Demo

El seed solo corre sobre una base de datos sin usuarios (`!await ctx.Usuarios.AnyAsync()`) y sus fechas son relativas al momento de ejecución (por ejemplo, la subasta crítica configurada a +90 segundos caduca transcurrido ese tiempo). Por lo tanto, para repetir los escenarios de prueba desde el estado inicial es necesario recrear la base local y reiniciar la API:

1. Eliminar la base de datos de desarrollo (solo en entornos locales):
```bash
dotnet ef database drop -f --project PROYECTO_SUBASTA.Infrastructure --startup-project PROYECTO_SUBASTA.Api
```
2. Reiniciar la API (aplicará automáticamente las migraciones y volverá a sembrar el seed completo con fechas frescas):
```bash
dotnet run --project PROYECTO_SUBASTA.Api
```

---

## 🏗️ Estructura de Capas (Clean Architecture)

```
proyecto-subasta-ya/
│
├── PROYECTO_SUBASTA.Domain/
│   ├── Entities/             # Entidades de dominio ricas (Subasta, Billetera, Puja, TransactionLedger, Usuario, Categoria, LogAuditoria)
│   └── Exceptions/           # Excepciones de negocio y dominio (DominioException)
│
├── PROYECTO_SUBASTA.Application/
│   ├── Commands/             # Commands y CommandHandlers (Subastas, Billeteras, Categorías, Usuarios)
│   ├── Queries/              # Queries y QueryHandlers (Subastas, Billeteras, Categorías, Auditoría, Usuarios)
│   ├── DTOs/                 # Contratos de transferencia de datos y respuestas HTTP
│   ├── Mappers/              # Mapeadores estáticos centralizados (SubastaMapper, LedgerMapper)
│   ├── Repositories/         # Interfaces de repositorios e IUnitOfWork (desacopladas de EF Core)
│   └── Validators/           # Validaciones de precisión monetaria y límites permitidos
│
├── PROYECTO_SUBASTA.Infrastructure/
│   ├── Data/                 # SubastaDbContext, DatabaseInitializer y configuración de entidades
│   ├── Migrations/           # Migraciones EF Core versionadas
│   ├── Repositories/         # Implementación de repositorios sobre MySQL y UnitOfWork con control de concurrencia
│   └── Services/             # Servicios de infraestructura (AuditoriaService inmutable)
│
└── PROYECTO_SUBASTA.Api/
    ├── Controllers/          # Controladores REST delegadores (sin lógica de negocio)
    ├── Middlewares/          # ExceptionMiddleware centralizado para mapeo de errores consistentes y auditoría
    ├── Workers/              # SubastaBackgroundWorker para cierre y activación automática
    └── Frontend/             # SPA corporativa en HTML5, CSS3 responsive y Vanilla JS
```
