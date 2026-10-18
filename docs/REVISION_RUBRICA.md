| ID | Criterio | Estado | Evidencia |
|---|---|---|---|
| A04 | modelo EF y esquema coherentes: claves, relaciones y restricciones preservan la integridad. | FALTA | Falta constraints, precision en algunas props (Ej. Subasta.PrecioBase) |
| A06 | cuatro usuarios/billeteras, cuatro categorías, cinco escenarios, pujas y ledger completos y coherentes. | FALTA | Program.cs muta data (L255) y HasData usa DateTime.UtcNow (SubastaDbContext.cs L133) |
| A07 | versionado efectivo en todos los escritores relevantes | FALTA | |
| A08 | la puja, garantías, estado y registros obligatorios en una sola unidad transaccional | FALTA | |
| B01 | producto, categoría, economía y fechas completos y validados directamente por la API | FALTA | |
| B02 | rechaza saldo insuficiente, incremento inválido y subastas próximas/vencidas SIN efectos financieros | FALTA | |
| B03 | retiene al nuevo líder y libera al anterior de forma atómica | FALTA | |
| B04 | ambos escenarios preservan ofertas, garantías y ledger | NO VERIFICABLE | Falta tests e2e en MySQL real |
| B05 | oferta válida en los últimos 60 s suma 120 s al cierre previo | FALTA | |
| B06 | el worker liquida o declara desierta, registra la venta UNA vez | FALTA | |
| B07 | total, retenido y disponible concilian con depósitos, garantías, liberaciones y liquidación | FALTA | Falta endpoint block (BilleterasController.cs) |
| B08 | auditoría diferenciada del ledger | FALTA | |
| B09 | recursos plurales, métodos y códigos coherentes (400, 404, 409) | FALTA | |
| B10 | OpenAPI y Swagger UI coherentes con operaciones y contratos reales | FALTA | |
| B11 | catálogo, detalle, filtros, ordenamiento, paginación y actividades con datos | FALTA | |
| B12 | un fallo controlado revierte toda la unidad de negocio | FALTA | |
| C01 | los controladores invocan handlers de Aplicación con Command/Query | PARCIAL | SubastasController usa IAdjudicacionService (L21) |
| C02 | casos de uso consumen contratos | FALTA | |
| C04 | la API adapta HTTP; Aplicación coordina; Dominio expresa reglas | FALTA | |
| C05 | nombres de negocio, funciones cohesionadas | FALTA | |
| C06 | operaciones críticas esperadas (sin fire-and-forget) | FALTA | `_ = auditoria.RegistrarFalloAsync(...)` en ExceptionMiddleware L54, L73 |
| C07 | errores explícitos y consistentes | FALTA | |
| C08 | decimal con precisión/escala coherentes en entradas, cálculos, entidades y columnas | FALTA | Faltan HasPrecision(18,2) en DbContext (e.g. Subasta.PrecioBase) |
| C09 | configuración de ejemplo segura; sin secretos en código ni salidas | FALTA | appsettings.json expone secrets |
| F03 | contador crítico, historial, sugerencia/monto libre | FALTA | |
| F09 | todos los módulos utilizables a 375 px | FALTA | |
| F10 | todos los módulos mantienen distribución a 768 px | FALTA | |
| F12 | estados claros, específicos y recuperables | FALTA | |
| F13 | dos sesiones reflejan ofertas, líder | FALTA | |

- `GET /api/Subastas` y `GET /api/Subastas/{id}` escriben en la base: Sí (llaman a _adjudicacionService.ProcesarSubastasVencidasAsync).
- `ObtenerActivasAsync` filtra por estado y pagina en la base de datos o en memoria: En memoria (Application/Queries/Subastas/ObtenerSubastasActivasQueryHandler.cs L42-44 usa Skip/Take despues de traer todos los activos con await _subastaRepository.ObtenerActivasAsync()).
- Program.cs contiene un bloque de "sincronización de subastas demo": Sí (L255).
- Hay `HasData` con `DateTime.UtcNow`: Sí (PROYECTO_SUBASTA.Infrastructure/Data/SubastaDbContext.cs L133+).
- Propiedades `*Id` sin FK: TransactionLedger.SubastaId, LogAuditoria.UsuarioId, Subasta.GanadorId, Subasta.VendedorId.
- Columnas decimal sin precisión explícita: Subasta.PrecioBase, Subasta.IncrementoMinimo, Subasta.PrecioFinal.
- `SubastaHub` registrado: No (no se encontró). El front hace polling? Sí, F13 indica implementar polling o WS, actualmente front hace algo.
- README afirma pero no existe: Dockerfile (se menciona en instrucciones de F1).
- Algún controlador depende de DbContext o repositorios: CategoriasController depende de CategoriaUseCases, SubastasController depende de IAdjudicacionService.
- Quedan `_ = ...Async(...)`: Sí, ExceptionMiddleware.cs (L54, L73).
- Existe endpoint público que permite liberar saldo: Sí, POST `/api/Billeteras/{usuarioId}/movimientos` pero tiene bloqueos L70 `if (tipo == "RETENCION" || tipo == "LIBERACION") return BadRequest(...)` aunque existe el endpoint.
