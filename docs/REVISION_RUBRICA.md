# Revisión Rúbrica Fase 1

## Constancias de comprobaciones específicas:
- **¿`GET /api/Subastas` y `GET /api/Subastas/{id}` escriben en la base (llaman a ProcesarSubastasVencidasAsync)?** Sí, llaman a `_adjudicacionService.ProcesarSubastasVencidasAsync`, que modifica el estado de subastas. Verificado en `ObtenerSubastasActivasQueryHandler.cs:35` y `ObtenerSubastaPorIdQueryHandler.cs:34`.
- **¿`ObtenerActivasAsync` filtra por estado y pagina en la base de datos o en memoria?** Pagina en memoria; obtiene todas las subastas con `_subastaRepository.ObtenerActivasAsync()` y aplica `.Skip().Take()` sobre la colección en `ObtenerSubastasActivasQueryHandler.cs:41`. No filtra por estado explícitamente.
- **¿Program.cs contiene un bloque de "sincronización de subastas demo" que muta datos (fechas, estados, billeteras, ledger) en cada arranque?** Sí, en `Program.cs` (después de ApplyMigrations), se obtienen entidades del contexto, se cambian `FechaInicio`, `FechaFin`, `Estado`, y se añaden `Puja` y `TransactionLedger`.
- **¿Hay `HasData` con `DateTime.UtcNow`?** Sí, en `SubastaDbContext.cs` líneas 131, 145, 152, etc., donde se definen las semillas de `Subasta`, `Puja`, y `TransactionLedger`.
- **¿Qué propiedades `*Id` escalares no tienen FK (TransactionLedger.SubastaId, LogAuditoria.UsuarioId, Subasta.GanadorId, etc.)?** En `SubastaDbContext.cs` no se definen relaciones FK para `TransactionLedger.SubastaId`, `LogAuditoria.UsuarioId`, `Subasta.GanadorId`, `Subasta.VendedorId`, ni `Puja.UsuarioId`.
- **¿Qué columnas decimal quedan sin precisión explícita (PrecioBase, IncrementoMinimo, PrecioFinal)?** En `SubastaDbContext.cs`, faltan las definiciones de precisión `HasPrecision(18,2)` para `PrecioBase`, `IncrementoMinimo` y `PrecioFinal` en `Subasta`.
- **¿`SubastaHub` está registrado (AddSignalR/MapHub)? ¿El frontend hace polling periódico?** No hay llamadas a `AddSignalR` ni `MapHub` en la configuración de la API (`Program.cs`). El frontend (en `app.js` y `api.js`) hace polling usando `setInterval` y `setTimeout`.
- **¿Qué afirma el README que no existe en el repositorio (Dockerfile, rutas, pasos)?** El README asume que hay que hacer `dotnet ef migrations add InitialCreate` pero el directorio Migrations ya lo tiene. No hay Dockerfile, aunque no está mencionado, la creación de DB asume que un MySQL local existe en el puerto por defecto, etc.
- **¿Algún controlador depende de DbContext, repositorios o IAdjudicacionService?** `SubastasController.cs` (línea 23) inyecta `IAdjudicacionService` directamente.
- **¿Quedan `_ = ...Async(...)` sin await?** Sí, en `ExceptionMiddleware.cs` (líneas 43 y 62) hay `_ = auditoria.RegistrarFalloAsync(...)` lo cual es un fire-and-forget.
- **¿Existe algún endpoint público que permita liberar saldo o registrar movimientos distintos de DEPOSITO?** `POST /api/Billeteras/{usuarioId}/movimientos` en `BilleterasController` bloquea explícitamente "RETENCION" y "LIBERACION", pero es una implementación frágil.

## Tabla de Criterios

| ID | Criterio | Estado | Evidencia |
|---|---|---|---|
| A04 | modelo EF y esquema coherentes | FALTA | `SubastaDbContext.cs` (faltan FKs, check constraints y cascade delete restrict) |
| A06 | cuatro usuarios/billeteras, cuatro categorías, cinco escenarios | FALTA | `Program.cs` muta datos; `SubastaDbContext.cs` usa `DateTime.UtcNow` |
| A07 | versionado efectivo en todos los escritores relevantes | PARCIAL | `SubastaDbContext.cs` define OCC para Subasta/Billetera, pero el guardado no está perfecto en concurrencia (RegistrarPujaCommand) |
| A08 | puja, garantías, estado y registros obligatorios en una sola unidad transaccional | OK | `RegistrarPujaCommandHandler.cs` usa `IUnitOfWork` |
| B01 | producto, categoría, economía y fechas completos y validados directamente por la API | FALTA | Faltan validaciones de `DataAnnotations` en `CrearSubastaDto.cs` |
| B02 | rechaza saldo insuficiente, incremento inválido y subastas próximas/vencidas SIN efectos financieros | OK | Lógica en `Subasta.cs:ValidarPuedePujar` y `RegistrarPujaCommandHandler` |
| B03 | retiene al nuevo líder y libera al anterior de forma atómica | OK | `RegistrarPujaCommandHandler.cs:72-88` (libera y retiene atómicamente) |
| B04 | ambos escenarios preservan ofertas, garantías y ledger | NO VERIFICABLE | Faltan pruebas de concurrencia de integración reales (`IntegrationTests`) |
| B05 | oferta válida en los últimos 60 s suma 120 s al cierre previo | FALTA | Números mágicos 60s hardcodeados en `Subasta.cs:EvaluarAntiSniping` |
| B06 | worker liquida o declara desierta, registra la venta UNA vez | FALTA | En `AdjudicacionService.cs`, podría no ser idempotente y hay mutación manual |
| B07 | total, retenido y disponible concilian | FALTA | Falta verificación fuerte y pruebas de integridad (no hay HasCheckConstraint) |
| B08 | auditoría diferenciada del ledger | OK | Separados en tablas y entidades `LogAuditoria` y `TransactionLedger` |
| B09 | recursos plurales, métodos y códigos coherentes | FALTA | `GET /api/Subastas` modifica estado por invocar cierres. |
| B10 | OpenAPI y Swagger UI coherentes | FALTA | Faltan atributos `ProducesResponseType` correctos y documentación XML habilitada. |
| B11 | catálogo, detalle, filtros, ordenamiento, paginación y actividades con datos completos y correctos | FALTA | Paginación en memoria `ObtenerSubastasActivasQueryHandler.cs` y falta filtros. |
| B12 | fallo controlado revierte toda la unidad de negocio | OK | Catch block en `RegistrarPujaCommandHandler.cs` hace Rollback. |
| C01 | controladores invocan handlers con Command/Query | PARCIAL | `CategoriasController` y `AuditoriasController` invocan Services en vez de Handlers. |
| C02 | casos de uso consumen contratos | OK | Controllers no inyectan DbContext, sino Handlers / Services. |
| C04 | API adapta HTTP; Aplicación coordina; Dominio expresa reglas; Infraestructura persiste | PARCIAL | Infraestructura (`AdjudicacionService`) coordina cierres en vez de Aplicación. |
| C05 | nombres de negocio, funciones cohesionadas | PARCIAL | Nombres redundantes, mapeo en Handlers (`ObtenerSubastasActivasQueryHandler.MapearADto`). |
| C06 | operaciones críticas esperadas (sin fire-and-forget) | FALTA | `ExceptionMiddleware.cs` (líneas 43, 62) usa `_ =`. |
| C07 | errores explícitos y consistentes | PARCIAL | Los errores son genéricos o mezclados en `ExceptionMiddleware`. |
| C08 | decimal con precisión/escala coherentes | FALTA | `SubastaDbContext.cs` no tiene `HasPrecision` para `PrecioBase`, `IncrementoMinimo`, `PrecioFinal`. |
| C09 | README | FALTA | `README.md` no refleja la versión actual del proyecto. |
| F03 | contador crítico, historial, sugerencia/monto libre y líder/superado | FALTA | `app.js` / `api.js` (frontend necesita ajustes de usabilidad/validación). |
| F09 | utilizables a 375 px | FALTA | Frontend desborda. |
| F10 | utilizables a 768 px | FALTA | Frontend elementos se pisan. |
| F12 | estados claros, específicos y recuperables | FALTA | Manejo de mensajes de error de la API no está completo en UI. |
| F13 | dos sesiones reflejan ofertas, líder, extensión y saldos sin recargar | FALTA | No hay WebSockets funcionando (Hub no registrado); Polling no pausa. |
