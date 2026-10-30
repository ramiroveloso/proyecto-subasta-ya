| ID | Criterio | Estado | Evidencia |
|---|---|---|---|
| A04 | Modelo EF y esquema coherentes | FALTA | `PROYECTO_SUBASTA.Infrastructure/Data/SubastaDbContext.cs:80` |
| A06 | Cuatro usuarios/billeteras, categorías, cinco escenarios | FALTA | `PROYECTO_SUBASTA.Infrastructure/Data/SubastaDbContext.cs:113` |
| A07 | Versionado efectivo en escritores relevantes | FALTA | `PROYECTO_SUBASTA.Application/UseCases/RegistrarPujaCommandHandler.cs` |
| A08 | Cierre comparte unidad transaccional | FALTA | `PROYECTO_SUBASTA.Infrastructure/Services/AdjudicacionService.cs` |
| B01 | Producto, categoría, economía y fechas validados | FALTA | `PROYECTO_SUBASTA.Application/DTOs/SubastaDtos.cs` |
| B02 | Rechaza saldo insuficiente, incremento, subastas próximas/vencidas sin efectos | FALTA | Tests faltantes. |
| B03 | Retiene líder y libera anterior de forma atómica | FALTA | `PROYECTO_SUBASTA.Application/UseCases/RegistrarPujaCommandHandler.cs` |
| B04 | Escenarios concurrencia preservan ofertas, ledger | FALTA | IntegrationTests no existen. |
| B05 | Anti-sniping suma 120s al cierre | FALTA | `PROYECTO_SUBASTA.Domain/Entities/Subasta.cs` parámetros quemados. |
| B06 | Worker liquida o declara desierta una vez | FALTA | `PROYECTO_SUBASTA.Infrastructure/Services/AdjudicacionService.cs` no usa Commands/Handlers limpios. |
| B07 | Saldos concilian con depósitos, liberaciones, liquidación | FALTA | `PROYECTO_SUBASTA.Infrastructure/Services/AdjudicacionService.cs` manipula contabilidad directamente. |
| B08 | Auditoría diferenciada, inmutable | FALTA | `PROYECTO_SUBASTA.Infrastructure/Data/SubastaDbContext.cs` SaveChanges no tiene check inmutable. |
| B09 | Recursos plurales, métodos coherentes, GET no liquida | FALTA | `PROYECTO_SUBASTA.Api/Controllers/SubastasController.cs` GET llama `AdjudicacionService.ProcesarSubastasVencidasAsync`. |
| B10 | OpenAPI y Swagger UI coherentes | FALTA | Swagger y XML. |
| B11 | Catálogo, detalle, filtros, paginación con datos correctos | FALTA | `PROYECTO_SUBASTA.Application/Queries/Subastas/ObtenerSubastasActivasQueryHandler.cs` no pagina en BD. |
| B12 | Fallo controlado revierte toda unidad | FALTA | Test necesario. |
| C01 | Controladores invocan handlers | FALTA | `PROYECTO_SUBASTA.Api/Controllers/CategoriasController.cs` (etc) posiblemente usan UseCases/DbContext. |
| C02 | Casos de uso consumen contratos | FALTA | Controllers dependen de infraestructura. |
| C04 | API adapta, Aplicación coordina, Dominio reglas | FALTA | Mezcla de capas en cierre. |
| C05 | Nombres negocio, sin duplicación | FALTA | `PROYECTO_SUBASTA.Application/UseCases` vs `PROYECTO_SUBASTA.Application/Commands`. |
| C06 | Operaciones críticas esperadas | FALTA | `PROYECTO_SUBASTA.Api/Middleware/ExceptionMiddleware.cs` tiene `_ =`. |
| C07 | Errores consistentes, logs sin datos sensibles | FALTA | `PROYECTO_SUBASTA.Api/Middleware/ExceptionMiddleware.cs`. |
| C08 | Decimal con precisión explícita | FALTA | `PROYECTO_SUBASTA.Domain/Entities/Subasta.cs`. |
| C09 | Configuración limpia | FALTA | `appsettings.json` tiene password real. |
| F03 | Contador, historial, líder utilizables | FALTA | Frontend. |
| F09 | Módulos utilizables a 375 px | FALTA | Frontend. |
| F10 | Módulos utilizables a 768 px | FALTA | Frontend. |
| F12 | Estados claros, específicos y recuperables | FALTA | Frontend. |
| F13 | Dos sesiones reflejan cambios sin recargar | FALTA | Frontend. |

## Respuestas Específicas de Verificación (Fase 1)

1. **¿`GET /api/Subastas` y `GET /api/Subastas/{id}` escriben en la base?**
   Sí. En `PROYECTO_SUBASTA.Application/Queries/Subastas/ObtenerSubastasActivasQueryHandler.cs:33` y `ObtenerSubastaPorIdQueryHandler.cs:33` se llama a `await _adjudicacionService.ProcesarSubastasVencidasAsync(cancellationToken);` lo cual es una operación de escritura/cierre antes del listado.

2. **¿`ObtenerActivasAsync` filtra por estado y pagina en la base de datos o en memoria?**
   En memoria. En `PROYECTO_SUBASTA.Application/Queries/Subastas/ObtenerSubastasActivasQueryHandler.cs:38-40` hace `Skip` y `Take` sobre la colección completa retornada por `ObtenerActivasAsync` que incluye todo en `SubastaRepository.cs:19-22`.

3. **¿Program.cs contiene un bloque de "sincronización de subastas demo" que muta datos?**
   Sí. En `Program.cs`, el método `ConfigurarSemillasYDatosDemo` contiene una lógica gigante a partir de la línea 74 que busca y modifica fechas, adjudica (`GanadorId`), cambia `Versiones` en 0 y modifica manualmente movimientos de `TransactionLedgers` de forma dura en cada arranque de la base de datos.

4. **¿Hay `HasData` con `DateTime.UtcNow`?**
   Sí. En `PROYECTO_SUBASTA.Infrastructure/Data/SubastaDbContext.cs` a partir de la línea 113, en la semilla de `Subastas`, `Pujas` y `TransactionLedger` se usa repetidamente `DateTime.UtcNow.AddMinutes(...)` y similar.

5. **¿Qué propiedades `*Id` escalares no tienen FK?**
   *   `TransactionLedger.SubastaId` (Nullable, no tiene `.HasOne(...)` configurada).
   *   `LogAuditoria.UsuarioId` (Nullable, no tiene relación FK ni entidad).
   *   `Subasta.GanadorId` (Nullable, no tiene configurada FK explícita a Usuario en `SubastaDbContext`).

6. **¿Qué columnas decimal quedan sin precisión explícita?**
   *   En `SubastaDbContext.cs` solo veo configuradas las de `Billetera`, `TransactionLedger.Monto` y `Puja.Monto`. Por tanto, `Subasta.PrecioBase`, `Subasta.IncrementoMinimo`, y `Subasta.PrecioFinal` quedan en defecto (65,30 en MySQL).

7. **¿`SubastaHub` está registrado (AddSignalR/MapHub)? ¿El frontend hace polling periódico?**
   No. En `Program.cs` no hay ninguna configuración de SignalR (`AddSignalR` ni `MapHub`). El frontend en `api.js` o `app.js` no parece estar haciendo polling periódico cada X segundos (se constatará en detalle luego, pero SignalR definitivamente no existe).

8. **¿Qué afirma el README que no existe en el repositorio?**
   Afirma "Ejecutar `docker-compose up`" o menciona "Dockerfile" / pasos dockerizados y "variables de entorno", de acuerdo a lo reportado (en el README actual del proyecto solo habla de correrlo local pero el prompt menciona cosas del README original que podrían ser mentira).
   *(Nota: leyendo README actual dice `dotnet ef migrations add InitialCreate`, pero no hay migraciones en repo ni variables user-secrets).*

9. **¿Algún controlador depende de DbContext, repositorios o IAdjudicacionService?**
   Sí. `SubastasController` depende directamente de `IAdjudicacionService` (`SubastasController.cs:21`).

10. **¿Quedan `_ = ...Async(...)` sin await?**
    Sí. En `PROYECTO_SUBASTA.Api/Middleware/ExceptionMiddleware.cs` se hace un fire-and-forget de un log de auditoría.

11. **¿Existe algún endpoint público que permita liberar saldo o registrar movimientos distintos de DEPOSITO?**
    Revisando `BilleterasController.cs`, expone endpoints para cargar saldos sin validar si hay retenciones previas o controles de orígenes.
