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

            // Precisión financiera para evitar errores de redondeo en base de datos
            modelBuilder.Entity<Billetera>().Property(b => b.SaldoTotal).HasPrecision(18, 2);
            modelBuilder.Entity<Billetera>().Property(b => b.SaldoRetenido).HasPrecision(18, 2);
            modelBuilder.Entity<Billetera>().Property(b => b.SaldoDisponible).HasPrecision(18, 2);
            modelBuilder.Entity<TransactionLedger>().Property(t => t.Monto).HasPrecision(18, 2);

            // Relación opcional Ledger -> Subasta (Restrict)
            modelBuilder.Entity<TransactionLedger>()
                .HasOne(t => t.Subasta)
                .WithMany()
                .HasForeignKey(t => t.SubastaId)
                .OnDelete(DeleteBehavior.Restrict);

            // Índice único en Usuario.Email
            modelBuilder.Entity<Usuario>()
                .HasIndex(u => u.Email)
                .IsUnique();

            // Relación LogAuditoria -> Usuario (SetNull)
            modelBuilder.Entity<LogAuditoria>()
                .HasOne(l => l.Usuario)
                .WithMany()
                .HasForeignKey(l => l.UsuarioId)
                .OnDelete(DeleteBehavior.SetNull);

            // ==========================================
            // CONFIGURACIÓN MÓDULO SUBASTAS
            // ==========================================

            // Precisión de 18,2 para las subastas y pujas
            modelBuilder.Entity<Puja>().Property(p => p.Monto).HasPrecision(18, 2);
            modelBuilder.Entity<Subasta>().Property(s => s.PrecioBase).HasPrecision(18, 2);
            modelBuilder.Entity<Subasta>().Property(s => s.IncrementoMinimo).HasPrecision(18, 2);
            modelBuilder.Entity<Subasta>().Property(s => s.PrecioFinal).HasPrecision(18, 2);

            // Relación 1 a N: Subasta - Pujas
            modelBuilder.Entity<Subasta>()
                .HasMany(s => s.Pujas)
                .WithOne(p => p.Subasta)
                .HasForeignKey(p => p.SubastaId)
                .OnDelete(DeleteBehavior.Cascade);

            // Relaciones Subasta -> Vendedor y Ganador (Restrict)
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

            // Configuración del Token de Concurrencia Optimista
            modelBuilder.Entity<Subasta>()
                .Property(s => s.Version)
                .IsConcurrencyToken();

            modelBuilder.Entity<Billetera>()
                .Property(b => b.Version)
                .IsConcurrencyToken();

            // ==========================================
            // DATOS SEMILLA CONSTANTES
            // ==========================================

            // Categorías obligatorias (valores constantes estáticos)
            modelBuilder.Entity<Categoria>().HasData(
                new Categoria { Id = 1, Nombre = "Tecnología" },
                new Categoria { Id = 2, Nombre = "Coleccionables" },
                new Categoria { Id = 3, Nombre = "Indumentaria" },
                new Categoria { Id = 4, Nombre = "Vehículos" }
            );
        }

        private void ValidarInmutabilidadAuditoria()
        {
            var hayModificacionesOEliminaciones = ChangeTracker.Entries<LogAuditoria>()
                .Any(e => e.State == EntityState.Modified || e.State == EntityState.Deleted);

            if (hayModificacionesOEliminaciones)
            {
                throw new InvalidOperationException("Los registros de auditoría son inmutables (append-only) y no pueden ser modificados ni eliminados.");
            }
        }

        public override int SaveChanges()
        {
            ValidarInmutabilidadAuditoria();
            return base.SaveChanges();
        }

        public override int SaveChanges(bool acceptAllChangesOnSuccess)
        {
            ValidarInmutabilidadAuditoria();
            return base.SaveChanges(acceptAllChangesOnSuccess);
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            ValidarInmutabilidadAuditoria();
            return base.SaveChangesAsync(cancellationToken);
        }

        public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
        {
            ValidarInmutabilidadAuditoria();
            return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }
    }
}