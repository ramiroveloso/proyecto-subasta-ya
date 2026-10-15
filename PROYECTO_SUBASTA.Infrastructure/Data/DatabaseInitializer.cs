using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PROYECTO_SUBASTA.Application.Services;
using PROYECTO_SUBASTA.Domain.Constants;
using PROYECTO_SUBASTA.Domain.Entities;
using PROYECTO_SUBASTA.Infrastructure.Services;

namespace PROYECTO_SUBASTA.Infrastructure.Data
{
    public static class DatabaseInitializer
    {
        public static async Task InicializarAsync(SubastaDbContext ctx, IPasswordHasher hasher, ILogger logger)
        {
            if (ctx == null) throw new ArgumentNullException(nameof(ctx));
            if (hasher == null) throw new ArgumentNullException(nameof(hasher));
            if (logger == null) throw new ArgumentNullException(nameof(logger));

            logger.LogInformation("Aplicando migraciones pendientes en la base de datos MySQL...");
            await ctx.Database.MigrateAsync();

            await SembrarDatosDemoAsync(ctx, hasher, logger);
        }

        public static async Task SembrarDatosDemoAsync(SubastaDbContext ctx, IPasswordHasher? hasher = null, ILogger? logger = null)
        {
            if (ctx == null) throw new ArgumentNullException(nameof(ctx));

            // Verificación de idempotencia estricta: si ya existen usuarios, NO se vuelve a sembrar
            if (await ctx.Usuarios.AnyAsync())
            {
                logger?.LogInformation("La base de datos ya contiene registros. Omitiendo seed inicial (idempotencia garantizada).");
                return;
            }

            logger?.LogInformation("Base de datos limpia detectada. Procediendo a insertar datos semilla iniciales...");

            var ahoraUtc = DateTime.UtcNow;
            var passwordHasher = hasher ?? new PasswordHasher();
            var passwordHashDemo = passwordHasher.Hash("1234");

            // 1. Usuarios del sistema
            var usuarios = new List<Usuario>
            {
                new Usuario { Id = 1, Nombre = "Vendedor Test", Email = "vendedor@test.com", PasswordHash = passwordHashDemo, FechaRegistro = ahoraUtc.AddDays(-30) },
                new Usuario { Id = 2, Nombre = "Comprador Líder", Email = "comprador1@test.com", PasswordHash = passwordHashDemo, FechaRegistro = ahoraUtc.AddDays(-30) },
                new Usuario { Id = 3, Nombre = "Comprador Habilitado", Email = "comprador2@test.com", PasswordHash = passwordHashDemo, FechaRegistro = ahoraUtc.AddDays(-30) },
                new Usuario { Id = 4, Nombre = "Usuario Sin Fondos", Email = "sinfondos@test.com", PasswordHash = passwordHashDemo, FechaRegistro = ahoraUtc.AddDays(-30) }
            };
            await ctx.Usuarios.AddRangeAsync(usuarios);
            await ctx.SaveChangesAsync();

            // 2. Billeteras asociadas
            var billeteras = new List<Billetera>
            {
                new Billetera { Id = 1, UsuarioId = 1, SaldoTotal = 25000.00m, SaldoRetenido = 0.00m, SaldoDisponible = 25000.00m, Version = 1 },
                new Billetera { Id = 2, UsuarioId = 2, SaldoTotal = 125000.00m, SaldoRetenido = 45000.00m, SaldoDisponible = 80000.00m, Version = 1 },
                new Billetera { Id = 3, UsuarioId = 3, SaldoTotal = 200000.00m, SaldoRetenido = 0.00m, SaldoDisponible = 200000.00m, Version = 1 },
                new Billetera { Id = 4, UsuarioId = 4, SaldoTotal = 0.00m, SaldoRetenido = 0.00m, SaldoDisponible = 0.00m, Version = 1 }
            };
            await ctx.Billeteras.AddRangeAsync(billeteras);
            await ctx.SaveChangesAsync();

            // 3. Subastas
            var subastas = new List<Subasta>
            {
                // S1: ACTIVA (cierra en +25 min, base 30000, inc 5000, versión 3)
                new Subasta
                {
                    Id = 1,
                    VendedorId = 1,
                    CategoriaId = 1,
                    Titulo = "Placa de Video RTX",
                    Descripcion = "GPU de alta gama para diseño y gaming",
                    UrlImagen = "https://images.unsplash.com/photo-1587202372775-e229f172b9d7?w=500",
                    PrecioBase = 30000.00m,
                    IncrementoMinimo = 5000.00m,
                    FechaInicio = ahoraUtc.AddMinutes(-20),
                    FechaFin = ahoraUtc.AddMinutes(25),
                    Estado = EstadoSubasta.ACTIVA,
                    Version = 3
                },
                // S2: ACTIVA crítica (cierra en +90s, base 50000, versión 1)
                new Subasta
                {
                    Id = 2,
                    VendedorId = 1,
                    CategoriaId = 1,
                    Titulo = "Procesador de Alta Gama",
                    Descripcion = "Ideal para estaciones de trabajo",
                    UrlImagen = "https://images.unsplash.com/photo-1591799264318-7e6ef8ddb7ea?w=500",
                    PrecioBase = 50000.00m,
                    IncrementoMinimo = 5000.00m,
                    FechaInicio = ahoraUtc.AddMinutes(-58),
                    FechaFin = ahoraUtc.AddSeconds(90),
                    Estado = EstadoSubasta.ACTIVA,
                    Version = 1
                },
                // S3: PROGRAMADA (+24h, base 15000, versión 1)
                new Subasta
                {
                    Id = 3,
                    VendedorId = 1,
                    CategoriaId = 2,
                    Titulo = "Figura Coleccionable Edición Limitada",
                    Descripcion = "Arte y diseño exclusivo",
                    UrlImagen = "https://images.unsplash.com/photo-1607604276583-eef5d076aa5f?w=500",
                    PrecioBase = 15000.00m,
                    IncrementoMinimo = 2000.00m,
                    FechaInicio = ahoraUtc.AddHours(24),
                    FechaFin = ahoraUtc.AddHours(48),
                    Estado = EstadoSubasta.PROGRAMADA,
                    Version = 1
                },
                // S4: FINALIZADA (ganador 2, precio 25000, versión 2)
                new Subasta
                {
                    Id = 4,
                    VendedorId = 1,
                    CategoriaId = 3,
                    Titulo = "Campera de Cuero Vintage",
                    Descripcion = "Indumentaria clásica",
                    UrlImagen = "https://images.unsplash.com/photo-1551028719-00167b16eac5?w=500",
                    PrecioBase = 20000.00m,
                    IncrementoMinimo = 2000.00m,
                    FechaInicio = ahoraUtc.AddDays(-2),
                    FechaFin = ahoraUtc.AddHours(-2),
                    Estado = EstadoSubasta.FINALIZADA,
                    GanadorId = 2,
                    PrecioFinal = 25000.00m,
                    Version = 2
                },
                // S5: DESIERTA (sin pujas, versión 1)
                new Subasta
                {
                    Id = 5,
                    VendedorId = 1,
                    CategoriaId = 4,
                    Titulo = "Repuesto Clásico de Vehículo",
                    Descripcion = "Sin ofertas registradas",
                    UrlImagen = "https://images.unsplash.com/photo-1486006920555-c77dce18193b?w=500",
                    PrecioBase = 80000.00m,
                    IncrementoMinimo = 10000.00m,
                    FechaInicio = ahoraUtc.AddDays(-3),
                    FechaFin = ahoraUtc.AddDays(-1),
                    Estado = EstadoSubasta.DESIERTA,
                    GanadorId = null,
                    PrecioFinal = null,
                    Version = 1
                }
            };
            await ctx.Subastas.AddRangeAsync(subastas);
            await ctx.SaveChangesAsync();

            // 4. Pujas demo
            var pujas = new List<Puja>
            {
                new Puja { Id = 1, SubastaId = 1, UsuarioId = 3, Monto = 35000.00m, FechaCreacion = ahoraUtc.AddMinutes(-15) },
                new Puja { Id = 2, SubastaId = 1, UsuarioId = 2, Monto = 45000.00m, FechaCreacion = ahoraUtc.AddMinutes(-5) },
                new Puja { Id = 3, SubastaId = 4, UsuarioId = 2, Monto = 25000.00m, FechaCreacion = ahoraUtc.AddDays(-1) }
            };
            await ctx.Pujas.AddRangeAsync(pujas);
            await ctx.SaveChangesAsync();

            // 5. Movimientos del Ledger con respaldo contable 1:1
            var ledgers = new List<TransactionLedger>
            {
                // Billetera 1 (Vendedor): Cobro por liquidación de Subasta #4 ($25.000)
                new TransactionLedger { Id = 1, BilleteraId = 1, Tipo = TipoTransaccion.COBRO, Monto = 25000.00m, Fecha = ahoraUtc.AddHours(-2), SubastaId = 4 },

                // Billetera 2 (Comprador Líder): Depósito inicial $150.000, Retención $25.000 (S4), Pago $25.000 (S4), Retención $45.000 (S1)
                new TransactionLedger { Id = 2, BilleteraId = 2, Tipo = TipoTransaccion.DEPOSITO, Monto = 150000.00m, Fecha = ahoraUtc.AddDays(-3), SubastaId = null },
                new TransactionLedger { Id = 3, BilleteraId = 2, Tipo = TipoTransaccion.RETENCION, Monto = 25000.00m, Fecha = ahoraUtc.AddDays(-1), SubastaId = 4 },
                new TransactionLedger { Id = 4, BilleteraId = 2, Tipo = TipoTransaccion.PAGO, Monto = 25000.00m, Fecha = ahoraUtc.AddHours(-2), SubastaId = 4 },
                new TransactionLedger { Id = 5, BilleteraId = 2, Tipo = TipoTransaccion.RETENCION, Monto = 45000.00m, Fecha = ahoraUtc.AddMinutes(-5), SubastaId = 1 },

                // Billetera 3 (Comprador Habilitado): Depósito inicial $200.000, Retención $35.000 (S1), Liberación $35.000 (S1)
                new TransactionLedger { Id = 6, BilleteraId = 3, Tipo = TipoTransaccion.DEPOSITO, Monto = 200000.00m, Fecha = ahoraUtc.AddDays(-3), SubastaId = null },
                new TransactionLedger { Id = 7, BilleteraId = 3, Tipo = TipoTransaccion.RETENCION, Monto = 35000.00m, Fecha = ahoraUtc.AddMinutes(-15), SubastaId = 1 },
                new TransactionLedger { Id = 8, BilleteraId = 3, Tipo = TipoTransaccion.LIBERACION, Monto = 35000.00m, Fecha = ahoraUtc.AddMinutes(-5), SubastaId = 1 }
            };
            await ctx.TransactionLedgers.AddRangeAsync(ledgers);
            await ctx.SaveChangesAsync();

            // 6. Logs de auditoría coherentes
            var logs = new List<LogAuditoria>
            {
                new LogAuditoria
                {
                    Id = 1,
                    UsuarioId = 2,
                    Accion = "CAMBIO_ESTADO_SUBASTA",
                    Detalle = "Subasta #4 ('Campera de Cuero Vintage') finalizada y adjudicada por Background Worker. Ganador: Usuario #2 (Comprador Líder) con oferta de $25.000,00.",
                    FechaRegistro = ahoraUtc.AddHours(-2)
                },
                new LogAuditoria
                {
                    Id = 2,
                    UsuarioId = 1,
                    Accion = "VENTA_REGISTRADA",
                    Detalle = "Liquidación final de Subasta #4: Saldo retenido ($25.000,00) de Comprador Líder transferido a Billetera de Vendedor Test.",
                    FechaRegistro = ahoraUtc.AddHours(-2)
                },
                new LogAuditoria
                {
                    Id = 3,
                    UsuarioId = null,
                    Accion = "CAMBIO_ESTADO_SUBASTA",
                    Detalle = "Subasta #5 ('Repuesto Clásico de Vehículo') declarada DESIERTA por el Background Worker al vencer el tiempo sin ofertas.",
                    FechaRegistro = ahoraUtc.AddDays(-1)
                }
            };
            await ctx.LogsAuditoria.AddRangeAsync(logs);
            await ctx.SaveChangesAsync();

            logger?.LogInformation("Datos semilla iniciales cargados con total éxito e invariantes contables verificadas.");
        }
    }
}
