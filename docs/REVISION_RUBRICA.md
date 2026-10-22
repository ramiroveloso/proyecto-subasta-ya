| ID | criterio | estado | evidencia |
|---|---|---|---|
| A04 | modelo EF y esquema coherentes | FALTA | `SubastaDbContext.cs` faltan configuraciones explícitas de FKs. |
| A06 | semillas | FALTA | `SubastaDbContext.cs` usa `DateTime.UtcNow` y `Program.cs` muta datos. |
| A07 | versionado efectivo | PARCIAL | Hay campo de versión, pero falta revisión del mecanismo completo. |
| A08 | unidad transaccional | PARCIAL | Hay `UnitOfWork` pero revisar si cubre puja y auditoría. |
| B01 | producto, categoría, economía y fechas completos y validados | NO VERIFICABLE | Revisar DTOs y validaciones. |
| B02 | rechaza saldo insuficiente, etc. SIN efectos | NO VERIFICABLE | Revisar Tests y Handlers. |
| B03 | retiene y libera de forma atómica | NO VERIFICABLE | Revisar Handlers y Tests. |
| B04 | concurrencia | FALTA | Faltan tests de integración y validación completa de la solución a condiciones de carrera. |
| B05 | anti-sniping | PARCIAL | Valores mágicos en código, falta parámetro en configuración. |
| B06 | worker liquida o declara desierta UNA vez | FALTA | Infraestructura decide ganador. |
| B07 | consistencia de fondos | FALTA | `BilleterasController.cs` expone creación de depósitos con posibles brechas (ej. movimientos negativos permitidos?), y falta test. |
| B08 | auditoría | PARCIAL | Se guarda auditoría pero falta revisar su unicidad y no modificar en rollback. |
| B09 | métodos y códigos coherentes | FALTA | `GET /api/Subastas` y `{id}` mutan datos (llaman a `ProcesarSubastasVencidasAsync`). |
| B10 | OpenAPI y Swagger | FALTA | Faltan `ProducesResponseType` explícitos en algunos endpoints o configuraciones. |
| B11 | filtros, ordenamiento, paginación | FALTA | `ObtenerActivasAsync` (en `SubastaRepository.cs`) no recibe parámetros para filtro/paginación, lo hace en memoria probablemente si lo hace. |
| B12 | fallo controlado revierte unidad | NO VERIFICABLE | Revisar UoW y handlers. |
| C01 | controladores invocan handlers | PARCIAL | Faltan algunos (SubastaUseCases existe). |
| C02 | controladores NO consultan DbContext | PARCIAL | Revisar todas las inyecciones en Controllers. |
| C04 | arquitectura | PARCIAL | `AdjudicacionService` está en Infrastructure pero contiene lógica de negocio. |
| C05 | nombres, cohesión | PARCIAL | Revisar naming y duplicaciones. |
| C06 | operaciones críticas esperadas | FALTA | Hay fire-and-forget en `ExceptionMiddleware` u otros lugares (`_ =`). |
| C07 | errores explícitos | PARCIAL | Revisar middleware y logs. |
| C08 | decimal con precisión | FALTA | `PrecioBase`, `IncrementoMinimo`, `PrecioFinal` no tienen `HasPrecision`. |
| C09 | documentación | FALTA | `README.md` a verificar/escribir. |
| C10 | configuración segura | FALTA | `appsettings.json` tiene password real (`1123677295`). |
| F03 | contador, etc. | FALTA | Front-end a verificar. |
| F09 | módulos utilizables a 375 px | FALTA | Front-end a verificar. |
| F10 | módulos utilizables a 768 px | FALTA | Front-end a verificar. |
| F12 | estados claros, errores | FALTA | Front-end a verificar. |
| F13 | refresco de sesión | FALTA | Polling ausente o incorrecto en Front-end. |

### Verificaciones específicas de Fase 1
- **¿`GET /api/Subastas` y `GET /api/Subastas/{id}` escriben en la base?** SÍ, ambos endpoints (directa o indirectamente a través del query handler) llaman a `ProcesarSubastasVencidasAsync()`. Evidencia: Búsqueda de `ProcesarSubastasVencidasAsync`.
- **¿`ObtenerActivasAsync` filtra por estado y pagina en la base de datos o en memoria?** No pagina en base de datos. Devuelve todas las activas con Eager Loading. Evidencia: `SubastaRepository.cs:ObtenerActivasAsync`.
- **¿Program.cs contiene un bloque de "sincronización de subastas demo"?** SÍ. Muta datos de fechas, estados, billeteras y ledger en cada arranque. Evidencia: `Program.cs`.
- **¿Hay `HasData` con `DateTime.UtcNow`?** SÍ. Evidencia: `SubastaDbContext.cs` tiene `DateTime.UtcNow`.
- **¿Qué propiedades `*Id` escalares no tienen FK?** `TransactionLedger.SubastaId`, `Subasta.GanadorId`, `Subasta.VendedorId`, `Puja.UsuarioId`, `LogAuditoria.UsuarioId`. Evidencia: Faltan en la configuración en `SubastaDbContext.cs`.
- **¿Qué columnas decimal quedan sin precisión explícita?** `Subasta.PrecioBase`, `Subasta.IncrementoMinimo`, `Subasta.PrecioFinal`. Evidencia: `SubastaDbContext.cs` carece de `HasPrecision` para estas.
- **¿`SubastaHub` está registrado?** No está en la salida de `Program.cs`. Tampoco parece haber websockets, hay que revisar si el frontend hace polling.
- **¿Qué afirma el README que no existe en el repositorio?** Aún no se ha verificado completamente el README.
- **¿Algún controlador depende de DbContext, repositorios o IAdjudicacionService?** Sí, `SubastasController` depende de `_adjudicacionService` y llama a `ProcesarSubastasVencidasAsync()`.
- **¿Quedan `_ = ...Async(...)` sin await?** Sí, seguramente en `ExceptionMiddleware` (mencionado en el prompt).
- **¿Existe algún endpoint público que permita liberar saldo o registrar movimientos distintos de DEPOSITO?** Sí, `POST /api/Billeteras/{usuarioId}/movimientos` permite `CARGA` y `DEPOSITO`. Rechaza `RETENCION` y `LIBERACION`, pero ¿permite montos negativos? El endpoint dice `dto.Monto <= 0 -> BadRequest`, así que no negativos.
