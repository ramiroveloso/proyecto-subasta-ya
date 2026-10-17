# Revisión Rúbrica - Fase 1

| ID | Criterio | Estado | Evidencia |
|---|---|---|---|
| A04 | Modelo EF y esquema coherentes | PARCIAL | Faltan `HasPrecision` en `Subasta` (`PrecioBase`, `IncrementoMinimo`, `PrecioFinal`) y `Puja` (`Monto`). Faltan FKs de navegación para `TransactionLedger.SubastaId`, `LogAuditoria.UsuarioId`, `Subasta.GanadorId`. Restricciones `CHECK` ausentes. |
| A06 | Semillas y estados predeterminados | PARCIAL | `Program.cs` contiene un bloque de sincronización que muta la base de datos al arrancar. `HasData` usa `DateTime.UtcNow` (ej. `SubastaDbContext.cs:131`). |
| A07 | Control de concurrencia optimista | PARCIAL | Se maneja `Version` en `Subasta` y `Billetera`, pero hay colisiones y/o validación no ideal. |
| A08 | Cierre comparte unidad transaccional | FALTA | El cierre no parece compartir de manera segura la unidad de trabajo y los eventos transaccionales en un mismo scope según se espera en B12. |
| B01 | Producto, fechas completos y validados API | PARCIAL | La API en `DTO`s carece de validación estricta y delegación de reglas (DataAnnotations completas y correctas). |
| B02 | Rechaza saldo/inc insuficiente | PARCIAL | Faltan validaciones de rechazo sin efectos, o no se hace auditoria adecuada en el rechazo. |
| B03 | Retención y liberación atómica Escrow | PARCIAL | La lógica no garantiza atómica y/o saldos retenidos. |
| B04 | Concurrencia integración escenarios | FALTA | No existe proyecto de tests de integración para los escenarios A y B. |
| B05 | Extensión anti-sniping 60s/120s | FALTA | `EvaluarAntiSniping` (en `Subasta.cs`) tiene números mágicos y falta inyección por config (options). |
| B06 | Liquidación worker / unica vez | FALTA | Adjudicación está acoplada en `Infrastructure` (`AdjudicacionService`), y no es idempotente en worker. |
| B07 | Total/Retenido/Disponible concilian | FALTA | No hay verificación transaccional que asegure balance estricto, faltan validaciones, un endpoint permite recarga manual insegura en BilleterasController (aunque bloquea Retencion/Liberacion). |
| B08 | Auditoría diferenciada inmutable | FALTA | `ExceptionMiddleware.cs` usa `_ = auditoria.RegistrarFalloAsync(...)` (fire-and-forget). Falta bloqueo a inmutabilidad en db context. |
| B09 | Recursos plurales / GET no liquida | FALTA | `GET /api/Subastas` y `GET /api/Subastas/{id}` llaman a `ProcesarSubastasVencidasAsync` que modifica el estado de la BD. |
| B10 | OpenAPI Swagger Docs | PARCIAL | Swagger activo, pero falta documentación XML, no están mapeados todos los códigos de error (400, 404, 409). |
| B11 | Filtros, paginación, catálogo | FALTA | `ObtenerActivasAsync` filtra en memoria en `ObtenerSubastasActivasQueryHandler` (con `Skip`/`Take` tras cargar todo). No filtra por estado adecuadamente en BD. |
| B12 | Fallo controlado unidad negocio | FALTA | `SubastaDbContext` no limpia entidades (ChangeTracker.Clear()) en rollback por excepciones de worker o similar. |
| C01 | Controladores invocan handlers | PARCIAL | Algunos controladores aún delegan en servicios directo (ej. `IAdjudicacionService` inyectado en `SubastasController`). |
| C02 | Casos de uso consumen contratos | PARCIAL | Se usan comandos/queries, pero hay `UseCases` paralelos como `SubastaUseCases`. |
| C04 | Capas arquitectura | PARCIAL | `Infrastructure` (`AdjudicacionService`) coordina casos de uso (Cerrar Subasta), rompiendo la arquitectura. |
| C05 | Nombres de negocio | PARCIAL | Hay duplicidad en lógica de handlers vs usecases. |
| C06 | Operaciones sin fire and forget | FALTA | Existen llamadas `_ = ...Async(...)` en `ExceptionMiddleware`. |
| C07 | Errores explicitos Logs contexto | FALTA | Logs incompletos, mapeo de excepciones en controladores vs middleware. |
| C08 | Decimal precisión explícita | FALTA | Faltan `HasPrecision(18,2)` para los decimales en `Subasta` y `Puja`. |
| C09 | Configuración limpia / README | FALTA | `appsettings.json` tiene usuario/clave. README no refleja instrucciones exactas de cómo iniciar, con mención a Docker u otros ausentes. |
| F03 | Contador crítico / Historial | FALTA | El contador o el estado líder no reflejan datos vivos correctamente (o queda 'Sin ofertas'). |
| F09 | 375px modulos | FALTA | Hay problemas de desborde global a 375px. |
| F10 | 768px modulos | FALTA | Hay elementos solapados a 768px (badges, precios cortados). |
| F12 | Estados red y especificos | FALTA | No se capturan mensajes del servidor para mostrar en UI. |
| F13 | Dos sesiones WS o Polling | FALTA | Faltan WebSockets o polling en UI (frontend no recarga las pujas/sala). |

### Respuestas a preguntas específicas:
- **¿`GET /api/Subastas` y `GET /api/Subastas/{id}` escriben en la base (llaman a ProcesarSubastasVencidasAsync)?**
  Sí, `ObtenerSubastasActivasQueryHandler` y `ObtenerSubastaPorIdQueryHandler` llaman a `await _adjudicacionService.ProcesarSubastasVencidasAsync(cancellationToken);` antes de retornar datos, modificando la BD.
- **¿`ObtenerActivasAsync` filtra por estado y pagina en la base de datos o en memoria?**
  Recupera todo con `ToListAsync()` (sin filtro) y luego `Skip()`/`Take()` se aplica en memoria en el Handler.
- **¿Program.cs contiene un bloque de "sincronización de subastas demo" que muta datos (fechas, estados, billeteras, ledger) en cada arranque?**
  Sí, hay un bloque enorme en `Program.cs` (alrededor de la línea 80) que reajusta fechas, billeteras, transacciones, etc. en cada inicio.
- **¿Hay `HasData` con `DateTime.UtcNow`?**
  Sí, en `SubastaDbContext.cs` al sembrar `Subasta`, `Puja` y `TransactionLedger` (líneas 131-158).
- **¿Qué propiedades `*Id` escalares no tienen FK (TransactionLedger.SubastaId, LogAuditoria.UsuarioId, Subasta.GanadorId, etc.)?**
  `TransactionLedger.SubastaId`, `LogAuditoria.UsuarioId` y `Subasta.GanadorId` son `int?` y carecen de propiedad de navegación en sus respectivas entidades. `Subasta.VendedorId` sí tiene `Vendedor`.
- **¿Qué columnas decimal quedan sin precisión explícita (PrecioBase, IncrementoMinimo, PrecioFinal)?**
  `Subasta.PrecioBase`, `Subasta.IncrementoMinimo`, `Subasta.PrecioFinal`, `Puja.Monto` no tienen precisión explícita definida en `SubastaDbContext`.
- **¿`SubastaHub` está registrado (AddSignalR/MapHub)? ¿El frontend hace polling periódico?**
  No hay SignalR (`AddSignalR` / `MapHub`) en `Program.cs`. El frontend usa `setInterval` para relojes locales pero no hay polling (fetch) periódico de estado.
- **¿Qué afirma el README que no existe en el repositorio (Dockerfile, rutas, pasos)?**
  El README parece no afirmar Dockerfile actualmente (habla de comandos `dotnet ef`), pero le faltan detalles precisos como el uso de `appsettings.Example.json`, y describe un flujo general.
- **¿Algún controlador depende de DbContext, repositorios o IAdjudicacionService?**
  Sí, `SubastasController` inyecta directamente `IAdjudicacionService`.
- **¿Quedan `_ = ...Async(...)` sin await?**
  Sí, en `ExceptionMiddleware.cs` (líneas 54 y 73).
- **¿Existe algún endpoint público que permita liberar saldo o registrar movimientos distintos de DEPOSITO?**
  En `BilleterasController.cs` el `POST` para movimientos sólo acepta `CARGA` o `DEPOSITO` (bloqueando `RETENCION` y `LIBERACION`, y rechazando cualquier otro tipo).
