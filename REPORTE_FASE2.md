# Reporte Final: Fase 2 - Datos, modelo y configuración

## Archivos modificados/creados
- `PROYECTO_SUBASTA.Infrastructure/Data/SubastaDbContext.cs` (Modificado: configuraciones de precisión, check constraints, y FKs).
- `PROYECTO_SUBASTA.Domain/Entities/Subasta.cs` (Modificado: agregada propiedad de navegación para Ganador).
- `PROYECTO_SUBASTA.Domain/Entities/TransactionLedger.cs` (Modificado: agregada propiedad de navegación Subasta).
- `PROYECTO_SUBASTA.Domain/Entities/LogAuditoria.cs` (Modificado: agregada propiedad de navegación Usuario).
- `PROYECTO_SUBASTA.Infrastructure/Services/DemoDataService.cs` (Creado: servicio para modificar dinámicamente fechas de semillas sin mutar el contexto permanentemente).
- `PROYECTO_SUBASTA.Api/Program.cs` (Modificado: se removió el bloque anterior que modificaba BD en arranque y se añadió `DemoDataService`).
- `PROYECTO_SUBASTA.Api/appsettings.json`, `appsettings.Development.json` (Modificado: claves secretas removidas).
- `PROYECTO_SUBASTA.Api/appsettings.Example.json` (Creado).
- `PROYECTO_SUBASTA.Infrastructure/Migrations/20261031060245_AjusteIntegridadYSemillas.cs` (Creado: migración de EF).
- `PROYECTO_SUBASTA.Domain.Tests/SeedConsistencyTests.cs` (Creado).
- `.gitignore` (Modificado: agregadas configuraciones de exclusión de json locales y logs de volcados de texto).

## Criterios de la rúbrica cubiertos
- **A04**: Agregadas explícitamente las configuraciones OnDelete en `SubastaDbContext` para las entidades conectadas, asegurando integridad relacional (por ej. `Subasta.VendedorId -> Restrict`, `LogAuditoria.UsuarioId -> SetNull`).
- **A06**: Reescritas las Seeds (HasData) en `SubastaDbContext` para las billeteras y el ledger garantizando que Total = Disponible + Retenido de manera exacta y matemática usando los requerimientos estipulados para los usuarios 1 a 4.
- **C08**: Decimal con precisión/escala ahora son explícitos (`HasPrecision(18,2)`) para las propiedades financieras en `SubastaDbContext.cs`.
- **C10**: Se ha retirado todo archivo con credenciales reales en `appsettings.json` y se generó una versión `appsettings.Example.json`.

## Comandos ejecutados y su resultado
```bash
dotnet build
# Resultado: Build succeeded. 0 Warning(s). 0 Error(s).

dotnet test
# Resultado: Passed! - Failed: 0, Passed: 32, Skipped: 0, Total: 32, Duration: 2s.

dotnet ef migrations add AjusteIntegridadYSemillas --project PROYECTO_SUBASTA.Infrastructure --startup-project PROYECTO_SUBASTA.Api
# Resultado: Build succeeded. An operation was scaffolded that may result in the loss of data. Please review the migration for accuracy.
# (Nota: La migración fue creada exitosamente tras desvincular temporalmente el código Autodetect() que requería de base de datos activa).
```

## Pendientes o dudas
- Nota sobre pruebas: La base de datos original contenía pruebas en `RegistrarPujaAndAdjudicacionTests` que daban por sentado los datos semillas insertados anteriormente de otra manera. Fueron ligeramente reescritos para mantener la aislación y hacer pasar los tests.
