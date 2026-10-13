using Microsoft.EntityFrameworkCore;
using PROYECTO_SUBASTA.Domain.Entities;
using System.Reflection.Emit;

namespace PROYECTO_SUBASTA.Infrastructure.Data
{
    public class SubastaDbContext : DbContext
    {
        public SubastaDbContext(DbContextOptions<SubastaDbContext> options) : base(options)
        {
        }

        // --- Módulo Finanzas y Usuarios ---
        public DbSet<Usuario> Usuarios { get; set; }
        public DbSet<Billetera> Billeteras { get; set; }
        public DbSet<TransactionLedger> TransactionLedgers { get; set; }
        public DbSet<Subasta> Subastas { get; set; }
        public DbSet<Categoria> Categorias { get; set; }
        public DbSet<Puja> Pujas { get; set; }
        public DbSet<LogAuditoria> LogsAuditoria { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ==========================================
            // CONFIGURACIÓN GLOBAL DE FECHAS EN UTC
            // ==========================================
            // Garantiza que cualquier DateTime se guarde en UTC en la Base de Datos (MySQL)
            // y al leerse tenga siempre DateTimeKind.Utc para que la serialización JSON añada 'Z'.
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

            // Relación N a 1 opcional: Ledger - Subasta
            modelBuilder.Entity<TransactionLedger>()
                .HasOne(t => t.Subasta)
                .WithMany()
                .HasForeignKey(t => t.SubastaId)
                .OnDelete(DeleteBehavior.Restrict);

            // Precisión financiera y restricciones CHECK de Billetera
            modelBuilder.Entity<Billetera>(entity =>
            {
                entity.Property(b => b.SaldoTotal).HasPrecision(18, 2);
                entity.Property(b => b.SaldoRetenido).HasPrecision(18, 2);
                entity.Property(b => b.SaldoDisponible).HasPrecision(18, 2);
                entity.Property(b => b.Version).IsConcurrencyToken();

                entity.ToTable(t =>
                {
                    t.HasCheckConstraint("CK_Billeteras_SaldoDisponible_NoNegativo", "SaldoDisponible >= 0");
                    t.HasCheckConstraint("CK_Billeteras_SaldoRetenido_NoNegativo", "SaldoRetenido >= 0");
                    t.HasCheckConstraint("CK_Billeteras_SaldoTotal_Consistencia", "SaldoTotal = SaldoRetenido + SaldoDisponible");
                });
            });

            // Precisión y restricciones CHECK de TransactionLedger
            modelBuilder.Entity<TransactionLedger>(entity =>
            {
                entity.Property(t => t.Monto).HasPrecision(18, 2);

                entity.ToTable(t =>
                {
                    t.HasCheckConstraint("CK_TransactionLedgers_Monto_Positivo", "Monto > 0");
                });
            });

            // ==========================================
            // CONFIGURACIÓN MÓDULO SUBASTAS Y AUDITORÍA
            // ==========================================

            // Relación 1 a N: Subasta - Pujas
            modelBuilder.Entity<Subasta>()
                .HasMany(s => s.Pujas)
                .WithOne(p => p.Subasta)
                .HasForeignKey(p => p.SubastaId)
                .OnDelete(DeleteBehavior.Cascade);

            // Relaciones de Subasta con Vendedor, Ganador y Categoría (Restrict)
            modelBuilder.Entity<Subasta>(entity =>
            {
                entity.HasOne(s => s.Vendedor)
                    .WithMany()
                    .HasForeignKey(s => s.VendedorId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(s => s.Ganador)
                    .WithMany()
                    .HasForeignKey(s => s.GanadorId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(s => s.Categoria)
                    .WithMany()
                    .HasForeignKey(s => s.CategoriaId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.Property(s => s.PrecioBase).HasPrecision(18, 2);
                entity.Property(s => s.IncrementoMinimo).HasPrecision(18, 2);
                entity.Property(s => s.PrecioFinal).HasPrecision(18, 2);
                entity.Property(s => s.Version).IsConcurrencyToken();

                entity.ToTable(t =>
                {
                    t.HasCheckConstraint("CK_Subastas_PrecioBase_Positivo", "PrecioBase > 0");
                    t.HasCheckConstraint("CK_Subastas_IncrementoMinimo_Positivo", "IncrementoMinimo > 0");
                    t.HasCheckConstraint("CK_Subastas_FechaFin_Mayor_FechaInicio", "FechaFin > FechaInicio");
                });
            });

            // Relaciones y restricciones de Puja
            modelBuilder.Entity<Puja>(entity =>
            {
                entity.HasOne(p => p.Usuario)
                    .WithMany()
                    .HasForeignKey(p => p.UsuarioId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.Property(p => p.Monto).HasPrecision(18, 2);

                entity.ToTable(t =>
                {
                    t.HasCheckConstraint("CK_Pujas_Monto_Positivo", "Monto > 0");
                });
            });

            // Relaciones de LogAuditoria (SetNull en UsuarioId)
            modelBuilder.Entity<LogAuditoria>(entity =>
            {
                entity.HasOne(l => l.Usuario)
                    .WithMany()
                    .HasForeignKey(l => l.UsuarioId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // ==========================================
            // DATOS SEMILLA (SEED DATA DETERMINISTA)
            // ==========================================

            // 1. Categorías obligatorias
            modelBuilder.Entity<Categoria>().HasData(
                new Categoria { Id = 1, Nombre = "Tecnología" },
                new Categoria { Id = 2, Nombre = "Coleccionables" },
                new Categoria { Id = 3, Nombre = "Indumentaria" },
                new Categoria { Id = 4, Nombre = "Vehículos" }
            );

            // 2. Usuarios obligatorios (Fechas UTC estáticas)
            modelBuilder.Entity<Usuario>().HasData(
                new Usuario { Id = 1, Nombre = "Vendedor Test", Email = "vendedor@test.com", PasswordHash = "hash_vendedor", FechaRegistro = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                new Usuario { Id = 2, Nombre = "Comprador Líder", Email = "comprador1@test.com", PasswordHash = "hash_c1", FechaRegistro = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                new Usuario { Id = 3, Nombre = "Comprador Habilitado", Email = "comprador2@test.com", PasswordHash = "hash_c2", FechaRegistro = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                new Usuario { Id = 4, Nombre = "Usuario Sin Fondos", Email = "sinfondos@test.com", PasswordHash = "hash_sf", FechaRegistro = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) }
            );

            // 3. Billeteras asociadas respetando fórmulas de saldo y conciliación
            modelBuilder.Entity<Billetera>().HasData(
                new Billetera { Id = 1, UsuarioId = 1, SaldoTotal = 25000.00m, SaldoRetenido = 0.00m, SaldoDisponible = 25000.00m, Version = 1 },
                new Billetera { Id = 2, UsuarioId = 2, SaldoTotal = 150000.00m, SaldoRetenido = 45000.00m, SaldoDisponible = 105000.00m, Version = 1 },
                new Billetera { Id = 3, UsuarioId = 3, SaldoTotal = 200000.00m, SaldoRetenido = 0.00m, SaldoDisponible = 200000.00m, Version = 1 },
                new Billetera { Id = 4, UsuarioId = 4, SaldoTotal = 0.00m, SaldoRetenido = 0.00m, SaldoDisponible = 0.00m, Version = 1 }
            );

            // 4. Subastas con escenarios 1 a 5 y fechas estáticas
            modelBuilder.Entity<Subasta>().HasData(
                // Subasta 1 (ACTIVA - Estándar con 2 ofertas previas)
                new Subasta { Id = 1, VendedorId = 1, CategoriaId = 1, Titulo = "Placa de Video RTX", Descripcion = "GPU de alta gama para diseño y gaming", UrlImagen = "https://images.unsplash.com/photo-1587202372775-e229f172b9d7?w=500", PrecioBase = 30000.00m, IncrementoMinimo = 5000.00m, FechaInicio = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc), FechaFin = new DateTime(2030, 1, 1, 12, 0, 0, DateTimeKind.Utc), Estado = "ACTIVA", GanadorId = null, PrecioFinal = null, Version = 3 },
                // Subasta 2 (ACTIVA - Crítica)
                new Subasta { Id = 2, VendedorId = 1, CategoriaId = 1, Titulo = "Procesador de Alta Gama", Descripcion = "Ideal para estaciones de trabajo", UrlImagen = "https://images.unsplash.com/photo-1591799264318-7e6ef8ddb7ea?w=500", PrecioBase = 50000.00m, IncrementoMinimo = 5000.00m, FechaInicio = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc), FechaFin = new DateTime(2030, 1, 1, 10, 5, 0, DateTimeKind.Utc), Estado = "ACTIVA", GanadorId = null, PrecioFinal = null, Version = 1 },
                // Subasta 3 (PROGRAMADA)
                new Subasta { Id = 3, VendedorId = 1, CategoriaId = 2, Titulo = "Figura Coleccionable Edición Limitada", Descripcion = "Arte y diseño exclusivo", UrlImagen = "https://images.unsplash.com/photo-1607604276583-eef5d076aa5f?w=500", PrecioBase = 15000.00m, IncrementoMinimo = 2000.00m, FechaInicio = new DateTime(2030, 1, 10, 10, 0, 0, DateTimeKind.Utc), FechaFin = new DateTime(2030, 1, 12, 10, 0, 0, DateTimeKind.Utc), Estado = "PROGRAMADA", GanadorId = null, PrecioFinal = null, Version = 1 },
                // Subasta 4 (FINALIZADA - Ganador Usuario 2 por $25.000)
                new Subasta { Id = 4, VendedorId = 1, CategoriaId = 3, Titulo = "Campera de Cuero Vintage", Descripcion = "Indumentaria clásica", UrlImagen = "https://images.unsplash.com/photo-1551028719-00167b16eac5?w=500", PrecioBase = 20000.00m, IncrementoMinimo = 2000.00m, FechaInicio = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc), FechaFin = new DateTime(2026, 1, 5, 12, 0, 0, DateTimeKind.Utc), Estado = "FINALIZADA", GanadorId = 2, PrecioFinal = 25000.00m, Version = 2 },
                // Subasta 5 (DESIERTA - Sin ofertas)
                new Subasta { Id = 5, VendedorId = 1, CategoriaId = 4, Titulo = "Repuesto Clásico de Vehículo", Descripcion = "Sin ofertas registradas", UrlImagen = "https://images.unsplash.com/photo-1486006920555-c77dce18193b?w=500", PrecioBase = 80000.00m, IncrementoMinimo = 10000.00m, FechaInicio = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc), FechaFin = new DateTime(2026, 1, 5, 12, 0, 0, DateTimeKind.Utc), Estado = "DESIERTA", GanadorId = null, PrecioFinal = null, Version = 1 }
            );

            // 5. Historial de Pujas semilla
            modelBuilder.Entity<Puja>().HasData(
                new Puja { Id = 1, SubastaId = 1, UsuarioId = 3, Monto = 35000.00m, FechaCreacion = new DateTime(2026, 9, 1, 10, 15, 0, DateTimeKind.Utc) },
                new Puja { Id = 2, SubastaId = 1, UsuarioId = 2, Monto = 45000.00m, FechaCreacion = new DateTime(2026, 9, 1, 10, 30, 0, DateTimeKind.Utc) },
                new Puja { Id = 3, SubastaId = 4, UsuarioId = 2, Monto = 25000.00m, FechaCreacion = new DateTime(2026, 8, 22, 15, 0, 0, DateTimeKind.Utc) }
            );

            // 6. Transacciones del Libro Mayor (Ledger) con trazabilidad 100% conciliada
            modelBuilder.Entity<TransactionLedger>().HasData(
                // Billetera 1 (Vendedor): Cobro de subasta 4
                new TransactionLedger { Id = 1, BilleteraId = 1, Tipo = TipoTransaccion.COBRO, Monto = 25000.00m, Fecha = new DateTime(2026, 8, 25, 12, 0, 0, DateTimeKind.Utc), SubastaId = 4 },

                // Billetera 2 (Comprador Líder): Depósito inicial, garantía subasta 4, pago subasta 4 y garantía subasta 1
                new TransactionLedger { Id = 2, BilleteraId = 2, Tipo = TipoTransaccion.DEPOSITO, Monto = 175000.00m, Fecha = new DateTime(2026, 8, 20, 10, 0, 0, DateTimeKind.Utc), SubastaId = null },
                new TransactionLedger { Id = 3, BilleteraId = 2, Tipo = TipoTransaccion.RETENCION, Monto = 25000.00m, Fecha = new DateTime(2026, 8, 22, 15, 0, 0, DateTimeKind.Utc), SubastaId = 4 },
                new TransactionLedger { Id = 4, BilleteraId = 2, Tipo = TipoTransaccion.PAGO, Monto = 25000.00m, Fecha = new DateTime(2026, 8, 25, 12, 0, 0, DateTimeKind.Utc), SubastaId = 4 },
                new TransactionLedger { Id = 5, BilleteraId = 2, Tipo = TipoTransaccion.RETENCION, Monto = 45000.00m, Fecha = new DateTime(2026, 9, 1, 10, 30, 0, DateTimeKind.Utc), SubastaId = 1 },

                // Billetera 3 (Comprador Habilitado): Depósito inicial, retención y liberación en subasta 1
                new TransactionLedger { Id = 6, BilleteraId = 3, Tipo = TipoTransaccion.DEPOSITO, Monto = 200000.00m, Fecha = new DateTime(2026, 8, 30, 9, 0, 0, DateTimeKind.Utc), SubastaId = null },
                new TransactionLedger { Id = 7, BilleteraId = 3, Tipo = TipoTransaccion.RETENCION, Monto = 35000.00m, Fecha = new DateTime(2026, 9, 1, 10, 15, 0, DateTimeKind.Utc), SubastaId = 1 },
                new TransactionLedger { Id = 8, BilleteraId = 3, Tipo = TipoTransaccion.LIBERACION, Monto = 35000.00m, Fecha = new DateTime(2026, 9, 1, 10, 30, 0, DateTimeKind.Utc), SubastaId = 1 }
            );

            // 7. Auditoría semilla obligatoria
            modelBuilder.Entity<LogAuditoria>().HasData(
                new LogAuditoria
                {
                    Id = 1,
                    Accion = "CAMBIO_ESTADO_SUBASTA",
                    Detalle = "Subasta #4 ('Campera de Cuero Vintage') finalizada y adjudicada por Background Worker. Ganador: Usuario #2 (Comprador Líder) con oferta de $25.000,00.",
                    UsuarioId = 2,
                    FechaRegistro = new DateTime(2026, 8, 25, 12, 0, 0, DateTimeKind.Utc)
                },
                new LogAuditoria
                {
                    Id = 2,
                    Accion = "VENTA_REGISTRADA",
                    Detalle = "Liquidación final de Subasta #4: Saldo retenido ($25.000,00) de Comprador Líder transferido a Billetera de Vendedor Test.",
                    UsuarioId = 1,
                    FechaRegistro = new DateTime(2026, 8, 25, 12, 0, 0, DateTimeKind.Utc)
                },
                new LogAuditoria
                {
                    Id = 3,
                    Accion = "CAMBIO_ESTADO_SUBASTA",
                    Detalle = "Subasta #5 ('Repuesto Clásico de Vehículo') declarada DESIERTA por el Background Worker al vencer el tiempo sin ofertas.",
                    UsuarioId = null,
                    FechaRegistro = new DateTime(2026, 8, 15, 10, 0, 0, DateTimeKind.Utc)
                }
            );
        }

        public override int SaveChanges()
        {
            ValidarInmutabilidadAuditoria();
            return base.SaveChanges();
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            ValidarInmutabilidadAuditoria();
            return base.SaveChangesAsync(cancellationToken);
        }

        private void ValidarInmutabilidadAuditoria()
        {
            var entradasAuditoriaMutadas = ChangeTracker.Entries<LogAuditoria>()
                .Where(e => e.State == EntityState.Modified || e.State == EntityState.Deleted)
                .ToList();

            if (entradasAuditoriaMutadas.Count > 0)
            {
                throw new InvalidOperationException("Los registros de LogAuditoria son estrictamente inmutables (append-only) y no pueden ser modificados ni eliminados.");
            }
        }
    }
}