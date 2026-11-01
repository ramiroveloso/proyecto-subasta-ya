# Revisión Rúbrica Fase 1

| ID | Criterio | Estado | Evidencia |
|---|---|---|---|
| A04 | Modelo EF y esquema coherentes | PARCIAL | Faltan restricciones CHECK y Foreign Keys sin restricción explícita en `SubastaDbContext.cs` |
| A06 | Semillas y datos iniciales | FALTA | Existen en `SubastaDbContext.cs:133`, pero mutan datos de forma constante en `Program.cs:79-229` y se usa `DateTime.UtcNow` |
| A07 | Unidad Transaccional | FALTA | Falta encapsulamiento de UoW completo en un solo lugar (`RegistrarPujaCommandHandler` no lo respeta cabalmente o usa SaveChanges sueltos) |
| A08 | Cierre comparte unidad transaccional | FALTA | `ExceptionMiddleware` y cierres tienen lógicas asíncronas no await o no unitarias |
| B01 | Producto, categoría y fechas completos/validados en API | PARCIAL | Faltan DataAnnotations y validaciones en los DTOs. |
| B02 | Rechazos sin efectos financieros | NO VERIFICABLE | Se necesitan test de las excepciones/validaciones. |
| B03 | Retiene nuevo lider y libera anterior de forma atómica | FALTA | Hay problemas o fallos de transaccionalidad al ocurrir `SaveChanges` parciales |
| B04 | Concurrencia | OK | Implementado usando `IsConcurrencyToken` en `Version` de `Subasta` y `Billetera` |
| B05 | Anti-sniping | FALTA | Umbrales mágicos 60/120 hardcodeados y falta parametrización. |
| B06 | Worker liquida o declara desierta 1 vez | FALTA | La adjudicación muta pero no respeta todas las reglas o falla con reintentos silenciosos |
| B07 | Saldos concilian | PARCIAL | Faltan tests para asegurar no se crea dinero ficticio |
| B08 | Auditoría inmutable | FALTA | No hay sobrescritura en `SaveChanges` para evitar modificaciones/borrados. `RegistrarFalloAsync` tiene problemas |
| B09 | Recursos plurales, métodos y códigos coherentes | FALTA | Existen endpoints GET mutando base (e.g., Cierres) |
| B10 | OpenAPI y Swagger coherentes | FALTA | Faltan `[ProducesResponseType]` completos o tipos concretos |
| B11 | Filtros, orden, paginación | FALTA | Paginación en memoria en `ObtenerSubastasActivasQueryHandler` |
| B12 | Fallo controlado revierte toda la unidad | FALTA | Falta `DescartarCambiosPendientes` en UoW |
| C01 | Controladores invocan handlers, retornan DTO | PARCIAL | Cierres y algunos endpoints devuelven Entidades o mapean a mano |
| C02 | Casos de uso consumen contratos, no DbContext | PARCIAL | Controladores inyectan DbContext o IAdjudicacionService, que no es Handler |
| C04 | Capas de arquitectura | PARCIAL | `IAdjudicacionService` en Infra que procesa lógica de dominio |
| C05 | Nombres de negocio, cohesión, sin duplicación | PARCIAL | Nombres de handler con duplicaciones, múltiples use cases. |
| C06 | Operaciones críticas esperadas, sin fire and forget | FALTA | Existen llamadas `_ = ...Async(...)` en `ExceptionMiddleware.cs` |
| C07 | Errores consistentes y logs con contexto | PARCIAL | Middleware captura pero falta contexto/parametrización completa. |
| C08 | Decimal precisión explícita | FALTA | Faltan HasPrecision para `PrecioBase`, `IncrementoMinimo`, `PrecioFinal` |
| C09 | README | FALTA | Referencias inexistentes o URLs incorrectas |
| F03 | Contador, historial, sugerencia/monto y líder en frontend | NO VERIFICABLE | Front no está plenamente conectado y ajustado (valida en vivo falta) |
| F09 | Diseño responsive 375px | FALTA | Existen desbordes en "Mis Actividades" |
| F10 | Diseño responsive 768px | FALTA | Badges solapadas y tarjetas dispares |
| F12 | Estados claros (incl. fondos insuficientes y conflicto) | FALTA | UI no refleja mensajes específicos del error en la response |
| F13 | Sesiones sin recargar | FALTA | Front requiere polling o SignalR parametrizado sin pisar el monto. |

## Comprobaciones Específicas
*   **¿`GET /api/Subastas` y `GET /api/Subastas/{id}` escriben en la base?**: SÍ. En `PROYECTO_SUBASTA.Api/Controllers/SubastasController.cs` y `PROYECTO_SUBASTA.Application/Queries/Subastas/ObtenerSubastasActivasQueryHandler.cs`, se llama a `await _adjudicacionService.ProcesarSubastasVencidasAsync(cancellationToken);`.
*   **¿`ObtenerActivasAsync` filtra por estado y pagina en la base de datos o en memoria?**: Pagina en memoria (`Skip` y `Take` están en `ObtenerSubastasActivasQueryHandler.cs:41` después de que `ObtenerActivasAsync()` devuelve los datos). Filtra en base de datos `ObtenerActivasAsync` devuelve `Where(s => s.Estado == "ACTIVA")`.
*   **¿Program.cs contiene un bloque de sincronización de subastas demo que muta datos?**: SÍ. Hay un gran bloque en `Program.cs` de la línea 79 a 229 (aproximadamente) que busca subastas (ej. subasta 1, 2, 3) y actualiza sus fechas en cada arranque.
*   **¿Hay `HasData` con `DateTime.UtcNow`?**: SÍ, `PROYECTO_SUBASTA.Infrastructure/Data/SubastaDbContext.cs` líneas 133 a 140 usan `DateTime.UtcNow`.
*   **¿Qué propiedades `*Id` escalares no tienen FK?**: `TransactionLedger.SubastaId`, `LogAuditoria.UsuarioId`, `Subasta.GanadorId`. También `Subasta.VendedorId` y `Puja.UsuarioId` no tienen explícita la relación/FK a `Usuario` (en `OnModelCreating`, aunque sí se generan en migrations por la convención de nombres).
*   **¿Qué columnas decimal quedan sin precisión explícita?**: `PrecioBase`, `IncrementoMinimo`, `PrecioFinal` de `Subasta`. (Solo `Billetera` y `Puja.Monto` tienen `HasPrecision(18,2)`).
*   **¿`SubastaHub` está registrado?**: NO, no hay mención a `SignalR`. El frontend (`app.js` / `api.js`) hace polling.
*   **¿Qué afirma el README que no existe?**: El `README.md` actual afirma que existe un proyecto `proyecto-subasta-ya` (clone URL inválida). El Prompt menciona Dockerfile, pero el README no lo menciona. (En general, el README menciona "desarrollado bajo los principios de Arquitectura Limpia", que es cierto en la intención).
*   **¿Algún controlador depende de DbContext, repositorios o IAdjudicacionService?**: SÍ. `SubastasController` depende de `IAdjudicacionService` en `PROYECTO_SUBASTA.Api/Controllers/SubastasController.cs:122`.
*   **¿Quedan `_ = ...Async(...)` sin await?**: SÍ. `_ = auditoria.RegistrarFalloAsync(...)` en `ExceptionMiddleware.cs` (líneas 54 y 73).
*   **¿Existe algún endpoint público que permita liberar saldo o registrar movimientos distintos de DEPOSITO?**: No hay endpoints directos, PERO se ejecuta lógicamente a través del cierre de subastas (`AdjudicacionService`), que es disparado por `GET /api/Subastas`. Además, existe un endpoint `POST /api/Billeteras/{id}/depositos` pero no parece haber para retiros directos, solo mediante lógica de cierre.
