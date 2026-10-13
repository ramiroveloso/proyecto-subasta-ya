| ID | Criterio | Estado | Evidencia |
|---|---|---|---|
| A04 | Modelo EF y esquema coherentes: claves, relaciones y restricciones | PARCIAL | Faltan foreign keys explícitas y constraints CHECK. |
| A06 | Cuatro usuarios/billeteras, cuatro categorías, cinco escenarios | PARCIAL | Los HasData en DbContext tienen fechas relativas (DateTime.UtcNow). Hay manipulaciones en Program.cs. |
| A07 | Versionado efectivo en todos los escritores relevantes | PARCIAL | RegistrarPuja llama a ActualizarConConcurrenciaAsync. |
| A08 | Cierre comparte unidad transaccional | PARCIAL | Cierre se hace en worker e invoca base. |
| B01 | Producto, categoría, economía y fechas validados por la API | FALTA | CrearSubastaDto no tiene validaciones. |
| B02 | Rechaza saldo insuficiente, incremento inválido, etc | PARCIAL | Validaciones están en dominio, pero el front las simula? |
| B03 | Retiene al nuevo líder y libera al anterior | PARCIAL | Backend lo hace, habría que revisar si hay rollback completo. |
| B04 | Pruebas de integración para escenarios de concurrencia | FALTA | No existe proyecto IntegrationTests. |
| B05 | Anti-sniping parametrizado | FALTA | Valores hardcodeados en Subasta.cs. |
| B06 | El worker liquida o declara desierta | PARCIAL | Infrastructure implementa IAdjudicacionService. |
| B07 | Total, retenido y disponible concilian | PARCIAL | BilleterasController prohíbe liberar saldo, pero la API pública `RegistrarMovimiento` permite depositar (y tiene algunas validaciones extrañas). |
| B08 | Auditoría diferenciada | PARCIAL | Registra, pero _ = auditoria... en middleware. |
| B09 | Recursos plurales, GET no liquida | FALTA | GET /api/Subastas y GET /api/Subastas/{id} llaman a ProcesarSubastasVencidasAsync (mutan datos). |
| B10 | OpenAPI / Swagger | PARCIAL | ProducesResponseType en SubastasController, pero faltan comentarios xml. |
| B11 | Catálogo, detalle, filtros, paginación | FALTA | Filtros y paginación de ObtenerActivasAsync ocurre en memoria (.Skip().Take() después de ToListAsync, en el repositorio). |
| B12 | Fallo controlado revierte unidad | PARCIAL | SaveChangesAsync puede quedar a medias si la auditoría falla o no hay transacciones. |
| C01 | Controladores invocan handlers | PARCIAL | Existen handlers, pero algunos devuelven entidades (CrearSubasta devuelve Subasta en Handler). |
| C02 | Casos de uso consumen contratos | PARCIAL | Application/Queries/Subastas/ObtenerSubastasActivasQueryHandler.cs invoca repositorio (IQueryable vs Ienumerable). |
| C04 | Infraestructura persiste, Aplicación coordina | FALTA | Infrastructure.AdjudicacionService coordina el cierre. |
| C05 | Nombres de negocio, sin duplicación | PARCIAL | Hay SubastaUseCases. |
| C06 | Operaciones críticas esperadas (sin fire-and-forget) | FALTA | `_ = auditoria.RegistrarFalloAsync` en ExceptionMiddleware. |
| C07 | Errores explícitos | PARCIAL | LogError en ExceptionMiddleware. |
| C08 | Decimal precisión | FALTA | Sin precisión en DbContext `HasPrecision`. |
| C09 | Configuración segura | FALTA | Contraseñas en appsettings.json. |
| F03 | Contador crítico, historial, sugerencia/monto libre | NO VERIFICADO | Frontend. |
| F09 | Módulos a 375 px | NO VERIFICADO | Frontend. |
| F10 | Módulos a 768 px | NO VERIFICADO | Frontend. |
| F12 | Estados claros, recuperables | NO VERIFICADO | Frontend. |
| F13 | Dos sesiones reflejan cambios | NO VERIFICADO | Frontend. |

### Respuestas a preguntas específicas de la Fase 1:
- ¿`GET /api/Subastas` y `GET /api/Subastas/{id}` escriben en la base (llaman a ProcesarSubastasVencidasAsync)?
  - **Sí**, `ObtenerSubastasActivasQueryHandler` y `ObtenerSubastaPorIdQueryHandler` llaman a `await _adjudicacionService.ProcesarSubastasVencidasAsync(cancellationToken);` antes de listar.
- ¿`ObtenerActivasAsync` filtra por estado y pagina en la base de datos o en memoria?
  - **En memoria**. `SubastaRepository.ObtenerActivasAsync()` devuelve un `IEnumerable<Subasta>` ejecutando la consulta (aunque internamente usa `ToListAsync()`). La paginación en `ObtenerSubastasActivasQueryHandler` usa `.Skip().Take()` sobre el resultado en memoria.
- ¿Program.cs contiene un bloque de "sincronización de subastas demo" que muta datos (fechas, estados, billeteras, ledger) en cada arranque?
  - **Sí**, hay un bloque masivo en `Program.cs` para resincronizar subastas, pujas, transacciones y crear auditorías iniciales cada vez.
- ¿Hay `HasData` con `DateTime.UtcNow`?
  - **Sí**, en `SubastaDbContext.cs` se usa `DateTime.UtcNow` repetidas veces para semillas de Subastas, Pujas y TransactionLedgers.
- ¿Qué propiedades `*Id` escalares no tienen FK (TransactionLedger.SubastaId, LogAuditoria.UsuarioId, Subasta.GanadorId, etc.)?
  - `TransactionLedger.SubastaId`, `Subasta.GanadorId`, `LogAuditoria.UsuarioId`, `Subasta.VendedorId`, `Puja.UsuarioId`, etc., están declaradas como escalares y muchas no tienen configuradas explícitamente sus claves foráneas con Fluent API (`HasOne...`).
- ¿Qué columnas decimal quedan sin precisión explícita (PrecioBase, IncrementoMinimo, PrecioFinal)?
  - `Subasta.PrecioBase`, `Subasta.IncrementoMinimo`, `Subasta.PrecioFinal`, `Puja.Monto`, y `TransactionLedger.Monto` (este último sí tiene `[Column(TypeName = "decimal(18,2)")]` pero no por API Fluent).
- ¿`SubastaHub` está registrado (AddSignalR/MapHub)? ¿El frontend hace polling periódico?
  - **No**, `grep` no encontró resultados para `AddSignalR` ni `MapHub`. El frontend hace polling.
- ¿Qué afirma el README que no existe en el repositorio (Dockerfile, rutas, pasos)?
  - El README no tiene Dockerfile, asume base de datos y correr local (pero dice "clonar el repositorio" sin url, o appsettings con usuario y clave que están hardcodeados).
- ¿Algún controlador depende de DbContext, repositorios o IAdjudicacionService?
  - **Sí**, `SubastasController` depende de `IAdjudicacionService`.
- ¿Quedan `_ = ...Async(...)` sin await?
  - **Sí**, en `PROYECTO_SUBASTA.Api/Middlewares/ExceptionMiddleware.cs:54` y `:73` se invoca `_ = auditoria.RegistrarFalloAsync(...)`.
- ¿Existe algún endpoint público que permita liberar saldo o registrar movimientos distintos de DEPOSITO?
  - En `BilleterasController`, `RegistrarMovimiento` bloquea `RETENCION` y `LIBERACION`, pero admite `CARGA` y `DEPOSITO`.
