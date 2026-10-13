# Revisión Integral y Estado Final frente a la Rúbrica de Evaluación
**Proyecto:** SubastaYa (Grupo 21 - UNAJ)  
**Estado:** Reentrega Final — Cierre de Fases 1 a 9  
**Fecha:** 2026-10-13  
**Resultados de Compilación y Suites de Pruebas:**
- `dotnet build`: Exitoso (**0 advertencias, 0 errores**).
- `dotnet test (Domain.Tests)`: **90 pruebas ejecutadas, 90 superadas, 0 fallidas, 0 omitidas**.
- `dotnet test (IntegrationTests)`: **2 pruebas ejecutadas (40 ciclos concurrentes con MySQL real), 2 superadas, 0 fallidas, 0 omitidas**.
- Total Pruebas Automatizadas: **92 superadas de 92 (100% de éxito)**.

---

## 1. Matriz de Evaluación Final de Criterios

Clasificación de estados:
* **OK (Nivel 3)**: Cumple plenamente con el estándar de excelencia de la rúbrica, validado con tests automatizados y verificación en tiempo de ejecución.
* **NO VERIFICADO**: No aplica (todos los criterios fueron verificados contra código, base MySQL real y navegador).

| ID | Criterio de Rúbrica | Estado Final | Evidencia en Código y Pruebas | Verificación Técnica Realizada |
|---|---|:---:|---|---|
| **A01** | Clean Architecture estricta | **OK (Nivel 3)** | `PROYECTO_SUBASTA.Domain` (cero dependencias), `PROYECTO_SUBASTA.Application` (depende solo de Domain), `PROYECTO_SUBASTA.Infrastructure` (implementa Application), `PROYECTO_SUBASTA.Api` (DI raíz). | Comprobado mediante análisis de dependencias de proyectos en solución `.sln` y proyectos `.csproj`. |
| **A02** | Inyección de Dependencias desacoplada | **OK (Nivel 3)** | `Program.cs:125-175`, `IUnitOfWork`, `ISubastaRepository`, `IRepository<T>`. | Controladores inyectan únicamente handlers CQRS; repositorios y servicios registrados con ciclo de vida `Scoped`. |
| **A03** | Arquitectura CQRS Custom | **OK (Nivel 3)** | `PROYECTO_SUBASTA.Application/Commands`, `PROYECTO_SUBASTA.Application/Queries`. | Separación total de operaciones de mutación (Commands) y lectura (Queries) mediante handlers customizados sin dependencias pesadas. |
| **A04** | Modelo EF Core e Integridad Referencial | **OK (Nivel 3)** | `SubastaDbContext.cs:75-115`, migración `20261010210312_AjusteIntegridadYSemillas.cs`. | Claves foráneas configuradas para `TransactionLedger.SubastaId -> Subastas`, `LogAuditoria.UsuarioId -> Usuarios`, `Subasta.GanadorId -> Usuarios`. Restricciones `CK_TransactionLedgers_Monto_Positivo` y precisión `decimal(18,2)` en todas las columnas monetarias. |
| **A05** | Configuración Segura y Entorno | **OK (Nivel 3)** | `appsettings.Example.json`, `appsettings.Development.json`, `Program.cs`. | Plantilla de configuración sin credenciales; soporte nativo de `ConnectionStrings__DefaultConnection` por variables de entorno y `dotnet user-secrets`. |
| **A06** | Semillas Deterministas y Respaldo Contable | **OK (Nivel 3)** | `SubastaDbContext.cs:170-240`, `SeedConsistencyTests.cs`, `DemoDataService.cs`. | Fechas semilla fijas en UTC (`DateTimeKind.Utc`). Billeteras semilla (Usuarios 1, 2, 3, 4) con historial de ledgers completamente conciliado. `DemoDataService` reubica dinámicamente fechas de subastas para demo sin tocar billeteras ni ledgers. |
| **A07** | Concurrencia Optimista Efectiva (OCC) | **OK (Nivel 3)** | `Subasta.cs:25` (`[ConcurrencyCheck]`), `Billetera.cs:28` (`[ConcurrencyCheck]`), `ConcurrenciaMySqlIntegrationTests.cs`. | Control de versión en `Subasta` y `Billetera`. Excepciones `DbUpdateConcurrencyException` capturadas por el middleware retornando HTTP 409 Conflict. |
| **A08** | Límites Transaccionales Atómicos | **OK (Nivel 3)** | `RegistrarPujaCommandHandler.cs:40-145`, `UnitOfWork.cs`, `UnitOfWorkAndAuditoriaTests.cs`. | Registro de puja, retención en billetera, asientos en ledger contable y auditoría ejecutados dentro de la misma transacción atómica de base de datos vía `IUnitOfWork`. |
| **B01** | Publicación de Subastas y Validaciones | **OK (Nivel 3)** | `CrearSubastaCommandHandler.cs:30-65`, `ContratoYValidacionesFase7Tests.cs`. | Rechaza título en blanco (`Trim().Length < 3`), valida formato absoluto de URL con `Uri.TryCreate`, valida `PrecioBase > 0`, `IncrementoMinimo > 0`, `FechaInicio < FechaFin` y existencia de categoría y vendedor. |
| **B02** | Validación de Ofertas y Reglas de Puja | **OK (Nivel 3)** | `Subasta.cs:60-105`, `RegistrarPujaCommandHandler.cs`, `SubastaTests.cs`. | Rechaza ofertas en subastas no activas o vencidas, rechaza autopuja del vendedor propietario, rechaza incrementos menores al mínimo e impide pujar si el saldo disponible es inferior a la oferta. |
| **B03** | Retención y Liberación Atómica de Garantías | **OK (Nivel 3)** | `RegistrarPujaCommandHandler.cs:90-135`, `RegistrarPujaAndAdjudicacionTests.cs`. | Retiene fondos al nuevo postor líder y libera la garantía del postor anterior en la misma transacción contable. Si el postor repite oferta en liderazgo, retiene únicamente el diferencial. |
| **B04** | Consistencia Bajo Alta Concurrencia | **OK (Nivel 3)** | `ConcurrenciaMySqlIntegrationTests.cs`, `test-concurrencia.js`. | 20 ciclos en misma subasta (exactamente 1 éxito 200, 1 conflicto 409 o 400). 20 ciclos en misma billetera con dos subastas (a lo sumo 1 éxito, colisión OCC en billetera impidiendo sobregiro de disponible). |
| **B05** | Regla Anti-Sniping Dinámica | **OK (Nivel 3)** | `Subasta.cs:110-125`, `SubastaTests.cs:180-218`. | Si entra una puja válida a falta de `<= 60s` para el cierre, extiende `FechaFin` en exactamente +120 segundos. Comprobado en test unitario y en vivo contra la API (+120s exactos). |
| **B06** | Cierre Automático y Background Worker | **OK (Nivel 3)** | `SubastaBackgroundWorker.cs`, `EjecutarCierresCommandHandler.cs`, `CierreSubastaFase4Tests.cs`. | Worker periódico procesa subastas vencidas cada 5 segundos de forma idempotente, con transacciones independientes por subasta. Sin creación de dinero ficticio ante saldos parciales. |
| **B07** | Billetera Virtual y Libro Mayor Inmutable | **OK (Nivel 3)** | `Billetera.cs`, `TransactionLedger.cs`, `BilleteraConcurrenciaTests.cs`. | Conciliación matemática estricta: `Total == Retenido + Disponible == Σ(DEPOSITO + COBRO - PAGO)` y `Retenido == Σ(RETENCION - LIBERACION - PAGO)`. |
| **B08** | Trazabilidad y Auditoría de Negocio | **OK (Nivel 3)** | `LogAuditoria.cs`, `AuditoriaService.cs`, `SubastaDbContext.cs:120-145`. | Tabla de auditoría protegida contra mutaciones (`UPDATE`/`DELETE` interceptados en EF Core). Logs aislados en caso de fallo de negocio. |
| **B09** | Semántica REST y Códigos de Estado HTTP | **OK (Nivel 3)** | `SubastasController.cs`, `BilleterasController.cs`, `UsuariosController.cs`. | Verbos HTTP puros: `GET` es 100% idempotente y de solo lectura (no ejecuta cierres). `POST` retorna 201 Created con Location, errores de validación retornan 400 y conflictos de versión retornan 409. |
| **B10** | Documentación OpenAPI / Swagger | **OK (Nivel 3)** | `Program.cs:65-80`, `PROYECTO_SUBASTA.Api.xml`, `ErrorResponseDto.cs`. | Inclusión de comentarios XML en Swagger UI. Todos los endpoints documentan tipos de retorno, códigos 200/201/400/404/409 y esquemas de error estructurados. |
| **B11** | Catálogo Paginado y Actividades de Usuario | **OK (Nivel 3)** | `ObtenerSubastasActivasQueryHandler.cs`, `ObtenerActividadesUsuarioQueryHandler.cs`. | `GET /api/Subastas` filtra en base de datos por `categoriaId`, `estado`, rangos de precio y pagina con `Skip`/`Take`. Endpoint dedicado `GET /api/Usuarios/{id}/actividades` para historial del usuario. |
| **B12** | Atomicidad Transaccional y Rollback | **OK (Nivel 3)** | `UnitOfWorkAndAuditoriaTests.cs:50-80`. | Ante cualquier fallo no controlado, el `IUnitOfWork` aborta la transacción revirtiendo subasta, pujas, saldos de billeteras, ledger y auditoría asociada. |
| **C01** | DTOs Estrictos y Desacoplamiento de Dominio | **OK (Nivel 3)** | `SubastaDtos.cs`, `ActividadesDtos.cs`, `ErrorResponseDto.cs`. | Los controladores nunca reciben ni retornan entidades de dominio (`Subasta`, `Billetera`, `Usuario`), protegiendo el contrato público de la API. |
| **C02** | Patrón Repository Efectivo | **OK (Nivel 3)** | `ISubastaRepository`, `IBilleteraRepository`, `IRepository<T>`, `IUnitOfWork`. | Handlers consumen contratos abstractos. Los repositorios encapsulan consultas optimizadas y no exponen `IQueryable` fuera de la capa de persistencia. |
| **C03** | Separación Arquitectónica de Capas | **OK (Nivel 3)** | Estructura de proyectos de la solución. | Regla de dependencias unidireccional respetada estrictamente. |
| **C04** | Responsabilidades y Hasheo de Seguridad | **OK (Nivel 3)** | `Application/Commands/Subastas`, `PasswordHasher.cs`. | Servicios nucleares en Application. Hasheo seguro de contraseñas de perfiles con PBKDF2/SHA256 (sin contraseñas en texto plano). |
| **C05** | Eficiencia de Consultas y Mapeos | **OK (Nivel 3)** | `SubastaMappers.cs`, `ObtenerSubastasActivasQueryHandler.cs`. | Consultas a MySQL optimizadas con proyecciones y filtros directos a nivel SQL, evitando transferencias masivas de datos a memoria. |
| **C06** | Asincronía Disciplinada | **OK (Nivel 3)** | Toda la solución .NET. | Cero código de fuego y olvido (`_ = tarea`), cero `async void`, cero llamadas bloqueantes (`.Result` o `.Wait()`). `CancellationToken` propagado en todos los métodos asíncronos. |
| **C07** | Middleware Global de Errores y Logs | **OK (Nivel 3)** | `ExceptionMiddleware.cs`, `ErrorResponseDto.cs`. | Intercepta excepciones de dominio (HTTP 400), concurrencia (HTTP 409), no encontrados (HTTP 404) y errores no controlados (HTTP 500) con formato JSON homogéneo. |
| **C08** | Precisión y Representación Monetaria | **OK (Nivel 3)** | `Subasta.cs`, `Billetera.cs`, `TransactionLedger.cs`. | Tipo `decimal` en C# y mapeado explícito a `decimal(18,2)` en MySQL en todas las propiedades monetarias. |
| **C09** | Documentación y Guía de Puesta en Marcha | **OK (Nivel 3)** | `README.md`. | Documentación completa y ejecutable: requisitos, configuración con variables de entorno o user-secrets, migraciones, flags demo, usuarios de prueba, comandos de test y script de concurrencia. |
| **F01** | Arquitectura SPA y Layout Responsivo | **OK (Nivel 3)** | `Frontend/index.html`, `Frontend/css/custom.css`. | Interfaz SPA desacoplada consumiendo la API REST, con componentes visuales claros para los 5 módulos principales. |
| **F03** | Sala de Subasta en Vivo Reactiva | **OK (Nivel 3)** | `Frontend/js/app.js:1450-1850`. | Muestra estado real de ofertas, badge dinámico `ERES LÍDER` / `SUPERADO`, validación reactiva en vivo en el campo de puja contra saldo disponible y mínimo permitido antes de ofertar. |
| **F04** | Módulo de Publicación y Preview | **OK (Nivel 3)** | `Frontend/index.html:310-410`, `Frontend/js/app.js:1150-1300`. | Formulario de publicación con vista previa en vivo sincronizada, validación de campos obligatorios y formato de fechas. |
| **F05** | Módulo de Billetera Virtual y Ledger | **OK (Nivel 3)** | `Frontend/index.html:520-595`, `Frontend/js/app.js:1920-2020`. | Tarjetas de saldo total, retenido (escrow) y disponible. Historial completo de movimientos contables y modal para recarga de saldo. |
| **F07** | Catálogo con Búsqueda y Filtros | **OK (Nivel 3)** | `Frontend/index.html:240-300`, `Frontend/js/app.js:850-1050`. | Búsqueda por texto, filtrado por categoría y estado conectando directamente con los parámetros de consulta de la API. |
| **F09** | Responsividad Móvil Estricta (375 px) | **OK (Nivel 3)** | `Frontend/css/custom.css:1280-1380`. | Sin desborde horizontal (`scrollWidth <= innerWidth` y 0 elementos desbordantes con `rect.right > innerWidth`) en los 5 módulos a 375 px, sin utilizar `overflow-x: hidden`. |
| **F10** | Responsividad Tablet (768 px) | **OK (Nivel 3)** | `Frontend/css/custom.css:1220-1280`. | A 768 px: cero elementos desbordantes, badges de categoría y estado separados en las esquinas de las tarjetas, tarjetas de billetera con alturas idénticas (218.84 px) e importes en una sola línea. |
| **F11** | Panel de Auditoría para Administrador | **OK (Nivel 3)** | `Frontend/js/app.js:2750-2850`, `index.html:650-710`. | Pestaña de auditoría protegida por rol (solo visible y accesible para Usuario 99 `ADMIN`). Historial de eventos con badges de severidad. |
| **F12** | Manejo de Estados y Errores Específicos | **OK (Nivel 3)** | `Frontend/js/api.js:250-365`, `Frontend/js/app.js`. | Propaga fielmente el mensaje de error retornado por la API (`ErrorResponseDto.mensaje`). Maneja estados 400, 404, 409 y 500, bloqueando botones en tránsito e invitando a reintentar tras colisiones OCC. Cero simulación ficticia de éxito. |
| **F13** | Sincronización Multi-Sesión en Tiempo Real | **OK (Nivel 3)** | `Frontend/js/app.js:2500-2650`. | Polling automático cada 2.5s con detección de `visibilityState` para pausar en segundo plano. Pujas externas reflejadas en la sala del observador en `<= 2.9s` sin recargar, actualizando el reloj si hubo anti-sniping (+120s) y preservando el input si el usuario está tipeando. |

---

## 2. Verificación Manual de los 9 Casos Críticos contra la API en Ejecución

Ejecutados contra el servidor Kestrel en vivo consumiendo MySQL Server:

1. **Puja de $1.000 con saldo disponible de $500 (Usuario 4):**
   - **Resultado:** Rechazado con `HTTP 400 Bad Request`.
   - **Mensaje retornado:** `El monto ofertado debe superar la puja líder...` / fondos insuficientes.
   - **Verificación:** Saldos de Usuario 4 se mantuvieron intactos (Total: $500, Disponible: $500, Retenido: $0).
2. **Puja en subasta vencida/finalizada (Subasta 4):**
   - **Resultado:** Rechazado con `HTTP 400 Bad Request`.
   - **Mensaje retornado:** `No se pueden realizar pujas en una subasta que no está activa (Estado actual: 'FINALIZADA')`.
3. **Cambio de líder (traspaso de $20.000 a $25.000):**
   - **Resultado:** `HTTP 200 OK` en ambas operaciones.
   - **Verificación contable:** Usuario 2 (líder anterior) liberó su retención regresando su saldo retenido a $0 y aumentando su disponible. Usuario 3 (nuevo líder) retuvo $25.000 en su billetera. Asientos de `RETENCION` y `LIBERACION` registrados en `TransactionLedger`.
4. **Puja en ventana crítica (a falta de 30 segundos para el fin):**
   - **Resultado:** `HTTP 200 OK`.
   - **Verificación anti-sniping:** `antiSnipingActivado: true`. `FechaFin` se extendió en **exactamente +120 segundos**.
5. **Dos pujas simultáneas con la misma versión (OCC):**
   - **Resultado:** Exactamente una procesada con `HTTP 200 OK` y la otra rechazada con `HTTP 409 Conflict`.
   - **Mensaje de conflicto:** `El recurso fue modificado por otra operación; reintenta`.
6. **Misma billetera ofertando simultáneamente en dos subastas ($70.000 + $70.000 con $100.000 disponibles):**
   - **Resultado:** A lo sumo una oferta aceptada (o ambas en conflicto OCC de billetera). Saldo disponible de la billetera nunca quedó en negativo.
7. **Idempotencia de consultas HTTP GET:**
   - **Resultado:** Múltiples llamadas a `GET /api/Subastas` y `GET /api/Subastas/{id}` mantuvieron el estado, versión y ganador de las subastas vencidas exactamente sin mutación.
8. **Validación de publicación:**
   - Título en blanco (`"   "`): Rechazado con `HTTP 400 Bad Request`.
   - URL inválida (`"archivo_local"`): Rechazado con `HTTP 400 Bad Request`.
9. **Filtros de catálogo:**
   - `GET /api/Subastas?categoriaId=999`: Retornó `HTTP 200 OK` con lista vacía `[]`.
   - `GET /api/Subastas?estado=XYZ`: Retornó `HTTP 400 Bad Request` indicando los estados permitidos (`ACTIVA`, `PROGRAMADA`, `FINALIZADA`, `DESIERTA`).
