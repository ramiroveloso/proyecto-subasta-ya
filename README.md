# SubastaYa - Sistema de Subastas en Línea

Sistema de subastas electrónicas en tiempo real desarrollado bajo **Clean Architecture** (Arquitectura Limpia) con **.NET 8**, **Entity Framework Core (Code-First)** y **MySQL Server**. Implementa control estricto de concurrencia optimista (OCC), trazabilidad contable inmutable de saldos (Libro Mayor / Escrow), regla anti-sniping y cierre automático de subastas.

---

## 📋 Requisitos Previos

* **[.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)** o superior instalado.
* **[MySQL Server 8.0](https://dev.mysql.com/downloads/mysql/)** instalado y corriendo localmente en el puerto `3306`.
* **[Node.js](https://nodejs.org/)** (v18 o superior) para la ejecución del script de verificación de concurrencia en paralelo.

---

## ⚙️ 1. Creación de la Base de Datos y Configuración

### 1.1 Crear la Base de Datos en MySQL
Ejecuta la siguiente sentencia en MySQL Workbench o cliente de consola MySQL:
```sql
CREATE DATABASE IF NOT EXISTS subastaYaDb CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
```

### 1.2 Configurar la Cadena de Conexión
El repositorio incluye una plantilla en `PROYECTO_SUBASTA.Api/appsettings.Example.json`. Para configurar las credenciales seguras de tu servidor MySQL local sin exponer claves en el repositorio, utiliza **dotnet user-secrets** o una **variable de entorno**:

#### Opción A: dotnet user-secrets (Recomendada para desarrollo local)
```bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=localhost;Port=3306;Database=subastaYaDb;Uid=TU_USUARIO;Pwd=TU_PASSWORD;" --project PROYECTO_SUBASTA.Api
```

#### Opción B: Variable de Entorno
* **En Windows PowerShell:**
  ```powershell
  $env:ConnectionStrings__DefaultConnection="Server=localhost;Port=3306;Database=subastaYaDb;Uid=TU_USUARIO;Pwd=TU_PASSWORD;"
  ```
* **En Linux / macOS / Bash:**
  ```bash
  export ConnectionStrings__DefaultConnection="Server=localhost;Port=3306;Database=subastaYaDb;Uid=TU_USUARIO;Pwd=TU_PASSWORD;"
  ```

---

## 🗄️ 2. Migraciones y Datos Semilla (Seeds)

Las migraciones de Entity Framework Core ya están compiladas e incluidas en el proyecto `PROYECTO_SUBASTA.Infrastructure/Migrations`.

### 2.1 Aplicar Migraciones Manualmente
Para impactar el esquema en la base de datos MySQL desde la terminal:
```bash
dotnet ef database update --project PROYECTO_SUBASTA.Infrastructure --startup-project PROYECTO_SUBASTA.Api
```

> **Nota:** La aplicación también aplica las migraciones pendientes automáticamente al iniciar (`context.Database.Migrate()`), por lo que si arrancas la API directamente, la base se actualizará de forma transparente.

### 2.2 Datos Semilla Iniciales
La migración inicial puebla automáticamente registros deterministas con invariantes contables 100% conciliadas:
* **Categorías:** Tecnología, Arte, Indumentaria, Coleccionables.
* **Usuarios Semilla:**
  * Usuario 1 (`vendedor@test.com`): Vendedor inicial.
  * Usuario 2 (`comprador1@test.com`): Comprador líder (Saldo Total: $150.000, Retenido: $45.000, Disponible: $105.000).
  * Usuario 3 (`comprador2@test.com`): Comprador habilitado (Saldo Total: $200.000, Disponible: $200.000).
  * Usuario 4 (`sinfondos@test.com`): Comprador con saldo bajo (Saldo Total: $500).
  * Usuario 99 (`admin@subastaya.com`): Administrador de la plataforma (acceso al panel de auditoría).
* **Subastas Base:**
  * Subasta #1: Placa de Video RTX (Activa).
  * Subasta #2: Procesador de Alta Gama (Activa / Zona Crítica).
  * Subasta #3: Consola Retro (Programada).
  * Subasta #4: Campera de Cuero Vintage (Finalizada).

### 2.3 Flag `Demo:ReiniciarFechas`
En `appsettings.Development.json`:
```json
"Demo": {
  "ReiniciarFechas": true
}
```
Cuando este valor está en `true`, el servicio `DemoDataService` reubica dinámicamente las fechas de inicio y fin de las subastas demo respecto del momento actual (UTC) al iniciar la aplicación. Esto asegura que la Subasta #1 quede activa (~25 min restantes) y la Subasta #2 quede en ventana crítica (~2 min restantes) para probar el worker de cierre y anti-sniping. `DemoDataService` **no modifica** saldos, billeteras ni asientos del libro mayor.

---

## ▶️ 3. Ejecución de la Aplicación

Para iniciar el servidor backend:
```bash
dotnet run --project PROYECTO_SUBASTA.Api
```

* **Frontend Web:** La API de ASP.NET Core sirve estáticamente la interfaz web SPA en la raíz. Abre en tu navegador la URL asignada por Kestrel (por ejemplo `https://localhost:65102/index.html` o `http://localhost:65103/index.html`).
* **Documentación OpenAPI / Swagger:** Disponible en `/swagger` (por ejemplo `https://localhost:65102/swagger`).

---

## 🧪 4. Ejecución de Pruebas Automatizadas

### 4.1 Pruebas Unitarias y de Dominio (Sin dependencias externas)
Ejecuta las 90 pruebas de arquitectura, lógica de dominio, invariantes contables y CQRS:
```bash
dotnet test PROYECTO_SUBASTA.Domain.Tests\PROYECTO_SUBASTA.Domain.Tests.csproj
```

### 4.2 Pruebas de Integración y Concurrencia en Base de Datos Real
Para ejecutar las pruebas de integración contra un servidor MySQL real, define la variable de entorno `TEST_MYSQL_CONNECTION` apuntando a una base de datos de test:
```powershell
$env:TEST_MYSQL_CONNECTION="Server=localhost;Port=3306;Database=subastaYaDb_test;Uid=root;Pwd=TU_PASSWORD;"
dotnet test PROYECTO_SUBASTA.IntegrationTests\PROYECTO_SUBASTA.IntegrationTests.csproj
```
Estas pruebas corren 20 ciclos de colisión simultánea de ofertas (Escenario A) y 20 ciclos de saturación de saldo en múltiples subastas (Escenario B), verificando la conciliación del ledger tras cada iteración.

---

## ⚡ 5. Script de Verificación de Concurrencia en Vivo

Con la API en ejecución, puedes disparar la prueba de estrés de concurrencia optimista utilizando el script Node.js incluido en la raíz:

```bash
# Ejecutar ambos escenarios (A y B)
node test-concurrencia.js --url=https://localhost:65102

# O ejecutar un escenario específico:
node test-concurrencia.js --url=https://localhost:65102 --escenario=A
node test-concurrencia.js --url=https://localhost:65102 --escenario=B
```

* **Escenario A:** Dos postores ofertan en paralelo con la misma versión en la misma subasta. Esperado: exactamente un HTTP 200 OK y un HTTP 409 Conflict (OCC).
* **Escenario B:** Un postor intenta ofertar simultáneamente en dos subastas comprometiendo más de su disponible. Esperado: a lo sumo un HTTP 200 OK, impidiendo el sobregiro.

---

## 🏛️ Estructura del Proyecto

* `PROYECTO_SUBASTA.Domain`: Entidades (`Subasta`, `Billetera`, `Usuario`, `TransactionLedger`, `LogAuditoria`), excepciones de negocio e invariantes contables. No posee dependencias externas.
* `PROYECTO_SUBASTA.Application`: Comandos y Consultas CQRS (`RegistrarPujaCommand`, `CerrarSubastaCommand`, `ObtenerSubastasActivasQuery`), DTOs y contratos de repositorios (`IUnitOfWork`, `ISubastaRepository`, `IRepository`).
* `PROYECTO_SUBASTA.Infrastructure`: Persistencia con Entity Framework Core (`SubastaDbContext`), repositorios, Unit of Work, migraciones y auditoría.
* `PROYECTO_SUBASTA.Api`: Controladores REST, middleware de excepciones global (`ExceptionMiddleware`), worker de fondo (`SubastaBackgroundWorker`) y frontend web estático en `/Frontend`.
* `PROYECTO_SUBASTA.Domain.Tests`: Suite de pruebas unitarias xUnit de dominio, billetera, auditoría y CQRS.
* `PROYECTO_SUBASTA.IntegrationTests`: Suite de pruebas de integración con MySQL real para validación de concurrencia y reconciliación contable.
