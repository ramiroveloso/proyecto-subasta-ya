| ID | Criterio | Estado | Evidencia |
|---|---|---|---|
| A04 | Modelo EF y esquema coherentes | PARCIAL | Faltan algunas foreign keys. Reviso TransactionLedger.SubastaId, Subasta.GanadorId, etc. |
| A06 | Semillas y consistencia de billeteras | PARCIAL | Program.cs actualmente muta datos. Billeteras no coinciden con las sumas indicadas en rúbrica en `Billetera.cs` y semillas actuales. |
| A07 | Versionado y concurrencia | PARCIAL | `Subasta.Version` y `Billetera.Version` existen, pero la lógica de la rúbrica dice que hay doble guardado o no está en todos los escritores (C08). |
| A08 | Cierre y log auditoría en transacción única | PARCIAL | Revisar handler de cierres |
| B01 | Validación de negocio en API | PARCIAL | |
| B02 | Puja rechazos (saldo, etc) | PARCIAL | |
| B03 | Retención y liberación atómica | PARCIAL | |
| B04 | Tests concurrencia integracion | FALTA | No hay proyecto de test de integración. |
| B05 | Anti-sniping parametrizado | FALTA | Hardcodeado en 60s/120s en `RegistrarPujaCommandHandler.cs` |
| B06 | Liquidación worker/cierre | PARCIAL | |
| B07 | Consistencia contable estricta | PARCIAL | |
| B08 | Auditoría inmutable diferenciada | PARCIAL | |
| B09 | Convenciones API / no mutar en GET | PARCIAL | En `ObtenerSubastasActivasQueryHandler` puede haber un cambio, lo verifiqué: no lo vi mutando estado en queryHandler directamente, pero la rúbrica pide verificar. |
| B10 | OpenAPI y Swagger completo | PARCIAL | |
| B11 | Filtros y paginación en GET | PARCIAL | |
| B12 | Fallo revierte unidad de negocio | PARCIAL | |
| C01 | Controladores CQRS | PARCIAL | |
| C02 | Controladores no acceden a capa datos | PARCIAL | |
| C04 | Arquitectura (capas, Dominio, Aplicacion, Infra) | PARCIAL | |
| C05 | Nombres de negocio y cohesión | PARCIAL | |
| C06 | Operaciones sin fire and forget | PARCIAL | |
| C07 | Logs y excepciones estructuradas | PARCIAL | |
| C08 | Precisión decimal | FALTA | No tienen HasPrecision definido para algunos montos |
| C09 | README | PARCIAL | |
| C10 | Secrets y appsettings seguros | PARCIAL | appsettings tiene claves o puertos harcoded posiblemente |
| F03 | Frontend: contador y oferta libre | PARCIAL | |
| F09 | Frontend: 375px responsive | PARCIAL | |
| F10 | Frontend: 768px responsive | PARCIAL | |
| F12 | Frontend: estados claros de UI | PARCIAL | |
| F13 | Frontend: sesiones con WS o polling 3s | PARCIAL | |
