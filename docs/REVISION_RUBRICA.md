# Revisión Rúbrica Fase 1

| ID | Criterio | Estado | Evidencia |
|---|---|---|---|
| A04 | Modelo EF y esquema coherentes | PARCIAL | Faltan propiedades de navegación y configuración de FKs en `TransactionLedger`, `Puja`, `Subasta` (vendedor y ganador), `LogAuditoria`. Usan FKs por convención que pueden ser implícitas y falta Restrict en algunos. |
| A06 | Cuatro usuarios/billeteras, escenarios | FALTA | Existen en `SubastaDbContext.cs` pero el `Program.cs` muta datos en el arranque y altera las billeteras. |
| A07 | Control de concurrencia | PARCIAL | Existe el token de concurrencia (`Version` en `Subasta.cs:25` y `Billetera.cs:17`), pero se duplica lógica de incremento en handler `RegistrarPujaCommandHandler.cs:72` o no se hace correctamente de forma optimista. |
| A08 | Transacciones unitarias | FALTA | Cierre de subasta y puja no encapsulan toda la unidad de negocio en un solo SaveChanges transaccional coherente con todo (ej. Auditoría escribe por separado o se hace sin rollback si hay un error en Liquidación). |
| B01 | Producto y categoría validados | FALTA | Validaciones incompletas en API (Faltan DataAnnotations en DTOs o la API depende del dominio). |
| B02 | Puja rechaza saldo, incremento inválido, etc. SIN efectos | PARCIAL | Lógica de dominio presente en `Subasta.ValidarPuedePujar` (`Subasta.cs:61`), pero hay que confirmar si en la BBDD hay efectos colaterales. |
| B03 | Retiene líder, libera anterior | PARCIAL | Implementado en backend (`AdjudicacionService.cs`), pero se debe revisar si el frontend intenta coordinar por separado. |
| B04 | Concurrencia misma subasta/distinta | FALTA | Pruebas de integración faltantes en el código base (se pide `PROYECTO_SUBASTA.IntegrationTests`). |
| B05 | Anti-sniping (oferta válida últimos 60s -> +120s) | PARCIAL | Hay números mágicos en `Subasta.cs` (líneas 119, 120: `60`, `60`). |
| B06 | Liquidación/desierta en worker | PARCIAL | Infraestructura (`AdjudicacionService.cs`) decide el ganador y liquidación en vez de la capa de Aplicación/Dominio. |
| B07 | Conciliación saldos | FALTA | Faltan test para garantizar que no hay caminos de creación de dinero ficticio. |
| B08 | Auditoría diferenciada de ledger | PARCIAL | Hay auditoría, pero no es atómica con la transacción (excepciones podrían guardar log pero revertir todo lo demás, o guardar cambios pero fallar el log si es fire and forget). |
| B09 | Recursos plurales, métodos y GET sin efectos | FALTA | `GET /api/Subastas` y `GET /api/Subastas/{id}` llaman a `ProcesarSubastasVencidasAsync`, mutando la base de datos (ver `ObtenerSubastasActivasQueryHandler.cs:35` y `ObtenerSubastaPorIdQueryHandler.cs:34`). |
| B10 | OpenAPI y Swagger | FALTA | Faltan `[ProducesResponseType]` concretos en todos los endpoints, comentarios XML no habilitados y esquemas. |
| B11 | Filtros, ordenamiento, paginación | FALTA | Filtrado/paginado se hace en memoria. (`ObtenerSubastasActivasQueryHandler.cs:42` llama a `Skip` sobre el resultado `IEnumerable` materializado). |
| B12 | Fallo controlado revierte TODO | FALTA | `RegistrarPujaCommandHandler` y otros no revierten cambios si un IAuditoriaService falla al final. |
| C01 | Controladores invocan handlers, retornan DTO | FALTA | Controladores no retornan DTOs siempre o devuelven entidades (ej. `SubastasController` expone entidades, o faltan commands). |
| C02 | Casos de uso consumen contratos | FALTA | Controladores o comandos dependen de dependencias incorrectas como `DbContext` o servicios de infraestructura. |
| C04 | Capas Clean Architecture | FALTA | `AdjudicacionService` (Infrastructure) realiza lógica de negocio que corresponde a Application y Domain. |
| C05 | Nombres de negocio, sin duplicación | FALTA | Duplicación de lógica y convenciones. |
| C06 | Operaciones críticas esperadas | FALTA | `ExceptionMiddleware.cs` tiene fire-and-forget (`_ = auditoria.RegistrarFalloAsync(...)` en líneas 54, 73). |
| C07 | Errores explícitos y logs limpios | FALTA | Manejo de excepciones en los controladores en vez de middleware único, logs pueden contener datos sensibles. |
| C08 | Precision Decimal | FALTA | Faltan configuraciones explícitas `HasPrecision(18,2)` para `Subasta.PrecioBase`, `IncrementoMinimo`, `PrecioFinal`. Solo `Billetera`, `Ledger`, y `Puja.Monto` la tienen. |
| C09 | Documentación README | FALTA | El README afirma cosas incorrectas o que no están (Dockerfile, test script erróneo, base de datos). |
| F03 | Contador crítico, historial, sugerencia | FALTA | Revisar frontend según rúbrica (errores de validación en vivo, estado del usuario). |
| F09 | 375 px usable | FALTA | Módulos desbordan horizontalmente. |
| F10 | 768 px usable | FALTA | Problemas de responsividad en tablet (cajas cortadas, texto partido). |
| F12 | Estados claros (fondos insuficientes) | FALTA | Frontend no muestra errores devueltos por servidor correctamente. |
| F13 | Dos sesiones sin recargar (WebSocket/Polling) | FALTA | `SubastaHub` no está registrado en `Program.cs` y el polling no funciona como se espera en frontend. |

## Comprobaciones Específicas
1. **¿`GET /api/Subastas` y `GET /api/Subastas/{id}` escriben en la base?**
   - **SÍ.** Llaman a `await _adjudicacionService.ProcesarSubastasVencidasAsync(cancellationToken);` antes de devolver resultados.
2. **¿`ObtenerActivasAsync` filtra por estado y pagina en la base de datos o en memoria?**
   - **En memoria.** `ObtenerActivasAsync` trae todas, y el paginado `Skip/Take` se hace en `ObtenerSubastasActivasQueryHandler.cs` sobre el resultado (IEnumerable).
3. **¿Program.cs contiene un bloque de "sincronización de subastas demo" que muta datos en cada arranque?**
   - **SÍ.** En el método `Configure` muta fechas y estados de subastas en el arranque, crea asientos falsos en ledger, etc.
4. **¿Hay `HasData` con `DateTime.UtcNow`?**
   - **SÍ.** En `SubastaDbContext.cs` (ej. línea 133, 146).
5. **¿Qué propiedades `*Id` escalares no tienen FK?**
   - `TransactionLedger.SubastaId`, `Subasta.VendedorId`, `Subasta.GanadorId`, `Puja.UsuarioId`, `LogAuditoria.UsuarioId`.
6. **¿Qué columnas decimal quedan sin precisión explícita?**
   - `Subasta.PrecioBase`, `Subasta.IncrementoMinimo`, `Subasta.PrecioFinal`.
7. **¿`SubastaHub` está registrado (AddSignalR/MapHub)? ¿El frontend hace polling periódico?**
   - **NO** está registrado en `Program.cs`. El frontend (`api.js` y `app.js`) hace llamadas periódicas pero no parecen bien estructuradas para polling de 2-3s que pide la rúbrica (usa fetches sin control de visibilidad).
8. **¿Qué afirma el README que no existe en el repositorio?**
   - Habla de un script de pruebas Node, Dockerfile (no se encontró pero dice "crear migración si no existiera"), contraseñas que no aplican así, etc.
9. **¿Algún controlador depende de DbContext, repositorios o IAdjudicacionService?**
   - **SÍ.** `SubastasController` depende de `IAdjudicacionService`.
10. **¿Quedan `_ = ...Async(...)` sin await?**
    - **SÍ.** En `ExceptionMiddleware.cs` (líneas 54, 73).
11. **¿Existe algún endpoint público que permita liberar saldo o registrar movimientos distintos de DEPOSITO?**
    - **A revisar**, pero parece que hay paths no controlados, o no rechazan saldos menores a cero (ej: `POST /api/Billeteras/{id}/movimientos`).
