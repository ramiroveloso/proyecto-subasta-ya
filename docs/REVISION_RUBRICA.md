# Revisión Rúbrica - Fase 1

| ID | Criterio | Estado | Evidencia |
|---|---|---|---|
| A04 | Modelo EF y esquema coherentes | PARCIAL | Faltan algunas restricciones explícitas (e.g. FK `Subasta.GanadorId`, `LogAuditoria.UsuarioId` o check constraints en DB). |
| A06 | Semillas (HasData) completas y coherentes | FALTA | `HasData` usa `DateTime.UtcNow`. `Program.cs` modifica la BD en el arranque. |
| A07 | Unidad transaccional para pujas | OK / PARCIAL | El código tiene un Unit of Work para puja, pero falta verificar los rollbacks completos y el versionado efectivo ante excepciones (B12). |
| A08 | Versionado efectivo y Unidad Transaccional en Cierre | FALTA / PARCIAL | El cierre tiene Unit of Work pero hay reglas que no se aplican en Dominio (se aplican en Servicio). |
| B01 | Producto, categoría, economía y fechas validados por API | FALTA | `CrearSubastaDto` y el controlador no tienen validaciones explícitas de atributos. Las de dominio sí existen, pero B01 pide en API (DataAnnotations). |
| B02 | Rechaza saldo/incremento/vencida SIN efectos financieros | OK | En Dominio se lanza excepción antes de tocar la billetera. (Queda ver si todo está cubierto). |
| B03 | Retiene a líder y libera anterior atómicamente | OK | El handler `RegistrarPujaCommandHandler` hace retención/liberación y Unit of Work lo commitea atómicamente. |
| B04 | Concurrencia misma subasta o misma billetera | NO VERIFICABLE | Faltan tests de concurrencia e integración. |
| B05 | Extensión anti-sniping | PARCIAL | Funciona pero usa "números mágicos" (60 y 120s en código en vez de configuración). |
| B06 | Worker liquida o declara desierta y respeta retiros | PARCIAL | El worker (en `AdjudicacionService`) hace el cierre. Falta moverlo a Application. |
| B07 | Consistencia del Ledger | PARCIAL | `BilleterasController` previene 'RETENCION' y 'LIBERACION', pero B07 dice "ningún camino público para registrar movimientos distintos de DEPOSITO". Se debe asegurar esto en profundidad. |
| B08 | Auditoría diferenciada inmutable | FALTA | Hay `IAuditoriaService` pero no se verifica la inmutabilidad de `LogAuditoria` en EF (falta sobreescribir `SaveChanges`). |
| B09 | Recursos plurales, métodos y códigos coherentes. GET no liquida. | FALTA | `ObtenerSubastasActivasQueryHandler` y `ObtenerSubastaPorIdQueryHandler` invocan a `_adjudicacionService.ProcesarSubastasVencidasAsync`, lo que altera la BD. |
| B10 | OpenAPI y Swagger coherentes | PARCIAL | Hay Swagger pero faltan cosas (como comentarios XML en los endpoints). |
| B11 | Filtros, orden, paginación, actividades | FALTA | `SubastasController.ObtenerActivas` trae todo a la memoria y pagina allí, no en DB. Faltan filtros por categoría y estado, y endpoint de actividades de usuario. |
| B12 | Fallo revierte unidad de negocio | FALTA | Faltan pruebas que verifiquen el rollback. |
| C01 | Controladores invocan handlers y retornan DTO | PARCIAL | Algunos devuelven entidades (e.g. Crear Subasta mapeaba, pero en la respuesta dice que el handler devuelve DTO, vamos a ver, el controlador de subasta mapea DTO. El controller devuelve DTOs). |
| C02 | Controladores no consultan DbContext | OK | `SubastasController` usa Handlers. |
| C04 | Infraestructura persiste, Aplicación coordina, Dominio reglas | PARCIAL | `AdjudicacionService` (Infrastructure) contiene mucha lógica de negocio (Application) y regras que deben estar en Domain. |
| C05 | Nombres de negocio, sin duplicación | PARCIAL | |
| C06 | Operaciones críticas sin fire-and-forget | FALTA | En `ExceptionMiddleware` hay un `_ = auditoria.RegistrarFalloAsync` (fire and forget). |
| C07 | Logs con contexto y sin datos sensibles | FALTA | `ExceptionMiddleware` no tiene contexto robusto (Ids). |
| C08 | Decimal precisión explícita | FALTA | Faltan `HasPrecision(18,2)` para `PrecioBase`, `IncrementoMinimo`, `PrecioFinal` en `SubastaDbContext`. |
| C09 | Configuración segura | FALTA | El readme nombra appsettings sin credenciales pero no está el `Example`. |
| F03 | Frontend: Contador crítico y estado de puja | PARCIAL | Funciona pero pide validación en vivo de montos, y otros fix. |
| F09 | Frontend: Móvil 375px sin solapamientos | FALTA | "Mis actividades" tiene desborde (scrollWidth 479px > 375px). |
| F10 | Frontend: 768px distribuido | FALTA | Badges se pisan, textos recortados, etc. |
| F12 | Frontend: Estados y errores | FALTA | Debe mostrar JSON de error (`mensaje`) y desactivar botones. |
| F13 | Frontend: Refresco sin recargar | FALTA | Necesita polling y manejar pestañas inactivas. |

### Respuestas a preguntas específicas de Fase 1

- **¿`GET /api/Subastas` y `GET /api/Subastas/{id}` escriben en la base (llaman a ProcesarSubastasVencidasAsync)?**
  Sí, tanto `ObtenerSubastasActivasQueryHandler` como `ObtenerSubastaPorIdQueryHandler` llaman a `await _adjudicacionService.ProcesarSubastasVencidasAsync(cancellationToken)`.
- **¿`ObtenerActivasAsync` filtra por estado y pagina en la base de datos o en memoria?**
  Recupera todas de la base de datos (`await _context.Subastas...ToListAsync()`) y pagina en memoria. No filtra explícitamente en la query DB.
- **¿Program.cs contiene un bloque de "sincronización de subastas demo" que muta datos en cada arranque?**
  Sí, hay un gran bloque "SINCRONIZACIÓN Y RENOVACIÓN DE SUBASTAS DEMO EN UTC" en `Program.cs` que modifica fechas, versiones, e inserta auditoría.
- **¿Hay `HasData` con `DateTime.UtcNow`?**
  Sí, en `SubastaDbContext` (p. ej. `new Subasta { FechaInicio = DateTime.UtcNow.AddMinutes(-20)... }`).
- **¿Qué propiedades `*Id` escalares no tienen FK?**
  `Subasta.GanadorId`, `TransactionLedger.SubastaId`, `LogAuditoria.UsuarioId` (probablemente, no vimos la entidad `LogAuditoria` en el db context), `Puja.UsuarioId`.
- **¿Qué columnas decimal quedan sin precisión explícita?**
  En `SubastaDbContext`, faltan `PrecioBase`, `IncrementoMinimo` y `PrecioFinal` en `Subasta`.
- **¿`SubastaHub` está registrado? ¿El frontend hace polling periódico?**
  No se ve `AddSignalR`. El frontend (según la rúbrica) no hace polling todavía (F13).
- **¿Qué afirma el README que no existe en el repositorio?**
  Afirma rutas de MySQL y comando para crear `InitialCreate` ("si no existiera") e `index.html`.
- **¿Algún controlador depende de DbContext, repositorios o IAdjudicacionService?**
  `SubastasController` depende directamente de `IAdjudicacionService`.
- **¿Quedan `_ = ...Async(...)` sin await?**
  Sí, en `ExceptionMiddleware.cs` hay `_ = auditoria.RegistrarFalloAsync(...)`.
- **¿Existe algún endpoint público que permita liberar saldo o registrar movimientos distintos de DEPOSITO?**
  En `BilleterasController` se bloquea explícitamente "RETENCION" y "LIBERACION", pero otros strings como vacíos los procesa como Carga. Hay que revisar esto en FASE 4/7.
