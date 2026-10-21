| ID | Criterio | Estado (OK / PARCIAL / FALTA / NO VERIFICABLE) | Evidencia |
|---|---|---|---|
| A04 | Modelo EF y esquema coherentes | PARCIAL | Faltan restricciones CHECK y propiedades de navegación en algunas entidades |
| A06 | Cuatro usuarios/billeteras, escenarios, ledger | PARCIAL | Faltan datos semilla exactos pedidos y no borrar bloque Program.cs |
| A07 | Versionado efectivo en escritores | PARCIAL | Puja y estado en transacción, pero revisar doble guardado/Auditoria |
| A08 | Cierre comparte unidad transaccional | FALTA | Infraestructura decide liquidación, y worker no maneja alcance correcto |
| B01 | Producto, categoría, economía validados por API | FALTA | DataAnnotations incompletos en CrearSubastaDto y controladores |
| B02 | Rechaza saldo/incremento/vencimiento sin efectos financieros | PARCIAL | Se lanzan excepciones pero verificar test de rollback |
| B03 | Retiene al nuevo líder y libera al anterior | PARCIAL | Lógica está, pero a revisar transacción / tests |
| B04 | Concurrencia misma subasta / misma billetera | FALTA | No hay tests de integración con MySQL real para concurrencia |
| B05 | Anti-sniping suma 120s si puja válida últimos 60s | PARCIAL | Está en Subasta.cs pero números mágicos (no IOptions) |
| B06 | Worker liquida o declara desierta una vez | PARCIAL | A revisar en AdjudicacionService |
| B07 | Fórmulas Billetera vs Ledger | FALTA | El frontend puede mover saldos / faltan tests |
| B08 | Auditoría diferenciada | PARCIAL | Rechazos con RegistrarFalloAsync usan _ = , no se espera en middleware |
| B09 | Recursos plurales, métodos HTTP | PARCIAL | GET Subastas llama a ProcesarSubastasVencidasAsync, lo que muta estado |
| B10 | OpenAPI y Swagger UI coherentes | FALTA | Faltan ProducesResponseType concretos, XML docs |
| B11 | Catálogo, detalle, filtros, paginación | FALTA | ObtenerSubastasActivas no filtra en BD, falta actividades |
| B12 | Fallo controlado revierte toda unidad | FALTA | Faltan tests y verificar que rechazo no genere efectos |
| C01 | Controladores invocan handlers | PARCIAL | Algunos controladores aún usan servicios/casos de uso de infraestructura o repiten try-catch |
| C02 | Casos de uso consumen contratos | PARCIAL | Application conoce DbContext/Infrastructure indirectamente |
| C04 | Capas Clean Architecture | PARCIAL | AdjudicacionService está en Infrastructure y coordina negocio |
| C05 | Nombres negocio, cohesión | PARCIAL | Archivos de UseCases redundantes |
| C06 | Operaciones críticas esperadas | FALTA | \_ = auditoria.RegistrarFalloAsync en ExceptionMiddleware |
| C07 | Errores explícitos | PARCIAL | Logs con contexto en ExceptionMiddleware pero falta afinar |
| C08 | Decimal precisión explícita | FALTA | PrecioBase, IncrementoMinimo sin HasPrecision(18,2) |
| C09 | README | FALTA | Volcados basura, README no adaptado |
| F03 | Contador crítico, historial | FALTA | No verificado frontend |
| F09 | Frontend responsive 375px | FALTA | No verificado frontend |
| F10 | Frontend responsive 768px | FALTA | No verificado frontend |
| F12 | Estados claros frontend | FALTA | No verificado frontend |
| F13 | Dos sesiones WebSockets/Polling | FALTA | No verificado frontend |

### Verificaciones específicas

* **¿`GET /api/Subastas` y `GET /api/Subastas/{id}` escriben en la base (llaman a ProcesarSubastasVencidasAsync)?**
Sí, `ObtenerSubastasActivasQueryHandler` y `ObtenerSubastaPorIdQueryHandler` llaman a `_adjudicacionService.ProcesarSubastasVencidasAsync()`.

* **¿`ObtenerActivasAsync` filtra por estado y pagina en la base de datos o en memoria?**
En memoria. El repositorio hace `return await _context.Subastas.Include(s => s.Categoria).Include(s => s.Pujas).ToListAsync();` (trae todas), y la paginación `.Skip((pageNumber - 1) * pageSize).Take(pageSize)` ocurre en el QueryHandler (en memoria). No filtra por estado de subasta.

* **¿Program.cs contiene un bloque de "sincronización de subastas demo" que muta datos (fechas, estados, billeteras, ledger) en cada arranque?**
Sí. Program.cs (línea 146 en adelante) modifica subastas 1 a 5, altera pujas, borra transacciones falsas, actualiza billeteras y agrega logs de auditoría cada vez que arranca.

* **¿Hay `HasData` con `DateTime.UtcNow`?**
Sí, en `SubastaDbContext.cs` (líneas 131, 145, 152) se usa `DateTime.UtcNow` para subastas, pujas, y transacciones de ledger.

* **¿Qué propiedades `*Id` escalares no tienen FK (TransactionLedger.SubastaId, LogAuditoria.UsuarioId, Subasta.GanadorId, etc.)?**
Faltan propiedades de navegación y FK para `TransactionLedger.SubastaId`, `LogAuditoria.UsuarioId`, `Subasta.GanadorId`, `Subasta.VendedorId` y en `Puja` la navegación desde `Usuario`.

* **¿Qué columnas decimal quedan sin precisión explícita (PrecioBase, IncrementoMinimo, PrecioFinal)?**
En el DbContext no se establece `HasPrecision(18,2)` para `PrecioBase`, `IncrementoMinimo`, `PrecioFinal` de `Subasta`, ni `Monto` de `Puja`.

* **¿`SubastaHub` está registrado (AddSignalR/MapHub)? ¿El frontend hace polling periódico?**
No hay registro de `SubastaHub` en `Program.cs`.

* **¿Qué afirma el README que no existe en el repositorio (Dockerfile, rutas, pasos)?**
Afirma que hay que crear la migración ("si no existiera en el repositorio") y tiene texto pegado de IA ("Aquí tienes una documentación técnica estructurada...").

* **¿Algún controlador depende de DbContext, repositorios o IAdjudicacionService?**
Sí, `SubastasController` recibe y depende de `IAdjudicacionService`.

* **¿Quedan `_ = ...Async(...)` sin await?**
Sí, en `PROYECTO_SUBASTA.Api/Middlewares/ExceptionMiddleware.cs` hay llamadas `_ = auditoria.RegistrarFalloAsync(...)` (líneas 54, 73).

* **¿Existe algún endpoint público que permita liberar saldo o registrar movimientos distintos de DEPOSITO?**
Se debe revisar si algún endpoint público permite esto (por ahora los controladores BilleterasController tienen endpoints).
