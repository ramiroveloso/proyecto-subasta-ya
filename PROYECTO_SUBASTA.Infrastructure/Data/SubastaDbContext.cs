using Microsoft.EntityFrameworkCore;
using PROYECTO_SUBASTA.Domain.Entities;
using System;

namespace PROYECTO_SUBASTA.Infrastructure.Data
{
    public class SubastaDbContext : DbContext
    {
        public SubastaDbContext(DbContextOptions<SubastaDbContext> options) : base(options) { }

        public DbSet<Usuario> Usuarios { get; set; } = null!;
        public DbSet<Billetera> Billeteras { get; set; } = null!;
        public DbSet<TransactionLedger> TransactionLedgers { get; set; } = null!;
        public DbSet<Categoria> Categorias { get; set; } = null!;
        public DbSet<Subasta> Subastas { get; set; } = null!;
        public DbSet<Puja> Pujas { get; set; } = null!;
        public DbSet<LogAuditoria> LogsAuditoria { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ==========================================
            // CONFIGURACIÓN DE CONVERSIÓN DE FECHAS A UTC
            // ==========================================
            var utcConverter = new Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<DateTime, DateTime>(
                v => v.Kind == DateTimeKind.Utc ? v : v.ToUniversalTime(),
                v => DateTime.SpecifyKind(v, DateTimeKind.Utc)
            );

            var nullableUtcConverter = new Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<DateTime?, DateTime?>(
                v => !v.HasValue ? v : (v.Value.Kind == DateTimeKind.Utc ? v : v.Value.ToUniversalTime()),
                v => !v.HasValue ? v : DateTime.SpecifyKind(v.Value, DateTimeKind.Utc)
            );

            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                foreach (var property in entityType.GetProperties())
                {
                    if (property.ClrType == typeof(DateTime))
                    {
                        property.SetValueConverter(utcConverter);
                    }
                    else if (property.ClrType == typeof(DateTime?))
                    {
                        property.SetValueConverter(nullableUtcConverter);
                    }
                }
            }

            // ==========================================
            // CONFIGURACIÓN MÓDULO FINANZAS
            // ==========================================

            // Relación 1 a 1: Usuario - Billetera
            modelBuilder.Entity<Usuario>()
                .HasOne(u => u.Billetera)
                .WithOne(b => b.Usuario)
                .HasForeignKey<Billetera>(b => b.UsuarioId)
                .OnDelete(DeleteBehavior.Cascade);

            // Relación 1 a N: Billetera - Ledger
            modelBuilder.Entity<Billetera>()
                .HasMany(b => b.Movimientos)
                .WithOne(t => t.Billetera)
                .HasForeignKey(t => t.BilleteraId)
                .OnDelete(DeleteBehavior.Cascade);

            // Precisión financiera para evitar errores de redondeo en base de datos
            modelBuilder.Entity<Billetera>().Property(b => b.SaldoTotal).HasPrecision(18, 2);
            modelBuilder.Entity<Billetera>().Property(b => b.SaldoRetenido).HasPrecision(18, 2);
            modelBuilder.Entity<Billetera>().Property(b => b.SaldoDisponible).HasPrecision(18, 2);
            modelBuilder.Entity<TransactionLedger>().Property(t => t.Monto).HasPrecision(18, 2);

            // Restricciones CHECK Billetera
            modelBuilder.Entity<Billetera>()
                .ToTable(t => t.HasCheckConstraint("CK_Billetera_SaldoDisponible", "SaldoDisponible >= 0"))
                .ToTable(t => t.HasCheckConstraint("CK_Billetera_SaldoRetenido", "SaldoRetenido >= 0"))
                .ToTable(t => t.HasCheckConstraint("CK_Billetera_SaldoTotal", "SaldoTotal = SaldoRetenido + SaldoDisponible"));

            // Restricciones CHECK TransactionLedger
            modelBuilder.Entity<TransactionLedger>()
                .ToTable(t => t.HasCheckConstraint("CK_TransactionLedger_Monto", "Monto > 0"));

            // Foreign Key TransactionLedger -> Subasta
            modelBuilder.Entity<TransactionLedger>()
                .HasOne(t => t.Subasta)
                .WithMany()
                .HasForeignKey(t => t.SubastaId)
                .OnDelete(DeleteBehavior.Restrict);


            // ==========================================
            // CONFIGURACIÓN MÓDULO SUBASTAS
            // ==========================================

            // Precisión financiera
            modelBuilder.Entity<Puja>().Property(p => p.Monto).HasPrecision(18, 2);
            modelBuilder.Entity<Subasta>().Property(s => s.PrecioBase).HasPrecision(18, 2);
            modelBuilder.Entity<Subasta>().Property(s => s.IncrementoMinimo).HasPrecision(18, 2);
            modelBuilder.Entity<Subasta>().Property(s => s.PrecioFinal).HasPrecision(18, 2);

            // Restricciones CHECK Puja
            modelBuilder.Entity<Puja>()
                .ToTable(t => t.HasCheckConstraint("CK_Puja_Monto", "Monto > 0"));

            // Restricciones CHECK Subasta
            modelBuilder.Entity<Subasta>()
                .ToTable(t => t.HasCheckConstraint("CK_Subasta_PrecioBase", "PrecioBase > 0"))
                .ToTable(t => t.HasCheckConstraint("CK_Subasta_IncrementoMinimo", "IncrementoMinimo > 0"))
                .ToTable(t => t.HasCheckConstraint("CK_Subasta_Fechas", "FechaFin > FechaInicio"));

            // Relaciones Subasta
            modelBuilder.Entity<Subasta>()
                .HasMany(s => s.Pujas)
                .WithOne(p => p.Subasta)
                .HasForeignKey(p => p.SubastaId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Subasta>()
                .HasOne(s => s.Vendedor)
                .WithMany()
                .HasForeignKey(s => s.VendedorId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Subasta>()
                .HasOne(s => s.Ganador)
                .WithMany()
                .HasForeignKey(s => s.GanadorId)
                .OnDelete(DeleteBehavior.Restrict);

            // Relación Puja -> Usuario
            modelBuilder.Entity<Puja>()
                .HasOne(p => p.Usuario)
                .WithMany()
                .HasForeignKey(p => p.UsuarioId)
                .OnDelete(DeleteBehavior.Restrict);

            // Relación LogAuditoria -> Usuario
            modelBuilder.Entity<LogAuditoria>()
                .HasOne(l => l.Usuario)
                .WithMany()
                .HasForeignKey(l => l.UsuarioId)
                .OnDelete(DeleteBehavior.SetNull);


            // Configuración del Token de Concurrencia Optimista
            modelBuilder.Entity<Subasta>()
                .Property(s => s.Version)
                .IsConcurrencyToken();

            modelBuilder.Entity<Billetera>()
                .Property(b => b.Version)
                .IsConcurrencyToken();

            // ==========================================
            // DATOS SEMILLA (SEED DATA OBLIGATORIOS)
            // ==========================================

            // Fechas base fijas UTC para los datos semilla
            var baseDate = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

            // 1. Categorías obligatorias
            modelBuilder.Entity<Categoria>().HasData(
                new Categoria { Id = 1, Nombre = "Tecnología" },
                new Categoria { Id = 2, Nombre = "Coleccionables" },
                new Categoria { Id = 3, Nombre = "Indumentaria" },
                new Categoria { Id = 4, Nombre = "Vehículos" }
            );

            // 2. Usuarios obligatorios con la propiedad Nombre requerida por la entidad
            modelBuilder.Entity<Usuario>().HasData(
                new Usuario { Id = 1, Nombre = "Vendedor Test", Email = "vendedor@test.com", PasswordHash = "hash_vendedor", FechaRegistro = baseDate },
                new Usuario { Id = 2, Nombre = "Comprador Líder", Email = "comprador1@test.com", PasswordHash = "hash_c1", FechaRegistro = baseDate },
                new Usuario { Id = 3, Nombre = "Comprador Habilitado", Email = "comprador2@test.com", PasswordHash = "hash_c2", FechaRegistro = baseDate },
                new Usuario { Id = 4, Nombre = "Usuario Sin Fondos", Email = "sinfondos@test.com", PasswordHash = "hash_sf", FechaRegistro = baseDate }
            );

            // 3. Billeteras asociadas según perfil financiero
            modelBuilder.Entity<Billetera>().HasData(
                // Billetera 1 (Vendedor): ledger COBRO 25000 (subasta 4) -> Total 25000, Retenido 0, Disponible 25000.
                new Billetera { Id = 1, UsuarioId = 1, SaldoTotal = 25000.00m, SaldoRetenido = 0.00m, SaldoDisponible = 25000.00m, Version = 1 },
                // Billetera 2 (Comprador Líder): DEPOSITO 175000; RETENCION 25000 (subasta 4); PAGO 25000 (subasta 4); RETENCION 45000 (subasta 1) -> Total 150000, Retenido 45000, Disponible 105000.
                new Billetera { Id = 2, UsuarioId = 2, SaldoTotal = 150000.00m, SaldoRetenido = 45000.00m, SaldoDisponible = 105000.00m, Version = 1 },
                // Billetera 3 (Comprador Habilitado): DEPOSITO 200000; RETENCION 35000 y LIBERACION 35000 (subasta 1) -> Total 200000, Retenido 0, Disponible 200000.
                new Billetera { Id = 3, UsuarioId = 3, SaldoTotal = 200000.00m, SaldoRetenido = 0.00m, SaldoDisponible = 200000.00m, Version = 1 },
                // Billetera 4 (Sin Fondos): sin movimientos, todo en 0.
                new Billetera { Id = 4, UsuarioId = 4, SaldoTotal = 0.00m, SaldoRetenido = 0.00m, SaldoDisponible = 0.00m, Version = 1 }
            );

            // 4. Subastas (Casos de Prueba obligatorios con VendedorId = 1)
            modelBuilder.Entity<Subasta>().HasData(
                // Subasta 1 (ACTIVA - Estándar): Cierra en 20-30 min, Precio Base: $30.000, Incremento: $5.000. Con 2 pujas previas cargadas (Líder: $45.000 del Comprador Líder).
                new Subasta { Id = 1, VendedorId = 1, CategoriaId = 1, Titulo = "Placa de Video RTX", Descripcion = "GPU de alta gama para diseño y gaming", UrlImagen = "https://images.unsplash.com/photo-1587202372775-e229f172b9d7?w=500", PrecioBase = 30000.00m, IncrementoMinimo = 5000.00m, FechaInicio = baseDate.AddDays(1).AddMinutes(-20), FechaFin = baseDate.AddDays(1).AddMinutes(25), Estado = "ACTIVA", Version = 3 },
                // Subasta 2 (ACTIVA - Crítica): Cierra en menos de 2 min (para probar alerta visual y extensión anti-sniping), Precio Base: $50.000.
                new Subasta { Id = 2, VendedorId = 1, CategoriaId = 1, Titulo = "Procesador de Alta Gama", Descripcion = "Ideal para estaciones de trabajo", UrlImagen = "https://images.unsplash.com/photo-1591799264318-7e6ef8ddb7ea?w=500", PrecioBase = 50000.00m, IncrementoMinimo = 5000.00m, FechaInicio = baseDate.AddDays(1).AddMinutes(-58), FechaFin = baseDate.AddDays(1).AddSeconds(90), Estado = "ACTIVA", Version = 1 },
                // Subasta 3 (PROGRAMADA): Inicio programado a +24 hs (pujas bloqueadas), Precio Base: $15.000.
                new Subasta { Id = 3, VendedorId = 1, CategoriaId = 2, Titulo = "Figura Coleccionable Edición Limitada", Descripcion = "Arte y diseño exclusivo", UrlImagen = "https://images.unsplash.com/photo-1607604276583-eef5d076aa5f?w=500", PrecioBase = 15000.00m, IncrementoMinimo = 2000.00m, FechaInicio = baseDate.AddDays(2), FechaFin = baseDate.AddDays(3), Estado = "PROGRAMADA", Version = 1 },
                // Subasta 4 (FINALIZADA): Fecha de fin pasada con puja ganadora (para cierre y liquidación del worker).
                new Subasta { Id = 4, VendedorId = 1, CategoriaId = 3, Titulo = "Campera de Cuero Vintage", Descripcion = "Indumentaria clásica", UrlImagen = "https://images.unsplash.com/photo-1551028719-00167b16eac5?w=500", PrecioBase = 20000.00m, IncrementoMinimo = 2000.00m, FechaInicio = baseDate.AddDays(-2), FechaFin = baseDate.AddHours(-2), Estado = "FINALIZADA", GanadorId = 2, PrecioFinal = 25000.00m, Version = 2 },
                // Subasta 5 (DESIERTA): Fecha de fin pasada sin pujas (para pase a estado DESIERTA).
                new Subasta { Id = 5, VendedorId = 1, CategoriaId = 4, Titulo = "Repuesto Clásico de Vehículo", Descripcion = "Sin ofertas registradas", UrlImagen = "https://images.unsplash.com/photo-1486006920555-c77dce18193b?w=500", PrecioBase = 80000.00m, IncrementoMinimo = 10000.00m, FechaInicio = baseDate.AddDays(-3), FechaFin = baseDate.AddDays(-1), Estado = "DESIERTA", Version = 1 }
            );

            // 5. Historial de las 2 ofertas previas en la subasta activa y puja ganadora en la finalizada
            modelBuilder.Entity<Puja>().HasData(
                new Puja { Id = 1, SubastaId = 1, UsuarioId = 3, Monto = 35000.00m, FechaCreacion = baseDate.AddDays(1).AddMinutes(-15) },
                new Puja { Id = 2, SubastaId = 1, UsuarioId = 2, Monto = 45000.00m, FechaCreacion = baseDate.AddDays(1).AddMinutes(-5) },
                new Puja { Id = 3, SubastaId = 4, UsuarioId = 2, Monto = 25000.00m, FechaCreacion = baseDate.AddDays(-1) }
            );

            // 6. Transacciones en el libro mayor (Ledger) que respaldan depósitos y retención Escrow
            modelBuilder.Entity<TransactionLedger>().HasData(
                // Billetera 1: COBRO 25000 (subasta 4)
                new TransactionLedger { Id = 1, BilleteraId = 1, Tipo = TipoTransaccion.COBRO, Monto = 25000.00m, Fecha = baseDate.AddHours(-1), SubastaId = 4 },

                // Billetera 2: DEPOSITO 175000; RETENCION 25000 (subasta 4); PAGO 25000 (subasta 4); RETENCION 45000 (subasta 1)
                new TransactionLedger { Id = 2, BilleteraId = 2, Tipo = TipoTransaccion.DEPOSITO, Monto = 175000.00m, Fecha = baseDate.AddDays(-1), SubastaId = null },
                new TransactionLedger { Id = 3, BilleteraId = 2, Tipo = TipoTransaccion.RETENCION, Monto = 25000.00m, Fecha = baseDate.AddDays(-1).AddMinutes(5), SubastaId = 4 },
                new TransactionLedger { Id = 4, BilleteraId = 2, Tipo = TipoTransaccion.PAGO, Monto = 25000.00m, Fecha = baseDate.AddHours(-1), SubastaId = 4 },
                new TransactionLedger { Id = 5, BilleteraId = 2, Tipo = TipoTransaccion.RETENCION, Monto = 45000.00m, Fecha = baseDate.AddDays(1).AddMinutes(-5), SubastaId = 1 },

                // Billetera 3: DEPOSITO 200000; RETENCION 35000 y LIBERACION 35000 (subasta 1)
                new TransactionLedger { Id = 6, BilleteraId = 3, Tipo = TipoTransaccion.DEPOSITO, Monto = 200000.00m, Fecha = baseDate.AddDays(-1), SubastaId = null },
                new TransactionLedger { Id = 7, BilleteraId = 3, Tipo = TipoTransaccion.RETENCION, Monto = 35000.00m, Fecha = baseDate.AddDays(1).AddMinutes(-15), SubastaId = 1 },
                new TransactionLedger { Id = 8, BilleteraId = 3, Tipo = TipoTransaccion.LIBERACION, Monto = 35000.00m, Fecha = baseDate.AddDays(1).AddMinutes(-5), SubastaId = 1 }
            );

            // 7. Logs de auditoría
            modelBuilder.Entity<LogAuditoria>().HasData(
                new LogAuditoria { Id = 1, Accion = "CAMBIO_ESTADO_SUBASTA", Detalle = "Subasta #4 ('Campera de Cuero Vintage') finalizada y adjudicada por Background Worker. Ganador: Usuario #2 (Comprador Líder) con oferta de $25.000,00.", UsuarioId = 2, FechaRegistro = baseDate.AddHours(-2) },
                new LogAuditoria { Id = 2, Accion = "VENTA_REGISTRADA", Detalle = "Liquidación final de Subasta #4: Saldo retenido ($25.000,00) de Comprador Líder transferido a Billetera de Vendedor Test.", UsuarioId = 1, FechaRegistro = baseDate.AddHours(-2) },
                new LogAuditoria { Id = 3, Accion = "CAMBIO_ESTADO_SUBASTA", Detalle = "Subasta #5 ('Repuesto Clásico de Vehículo') declarada DESIERTA por el Background Worker al vencer el tiempo sin ofertas.", UsuarioId = null, FechaRegistro = baseDate.AddDays(-1) }
            );
        }
    }
}
