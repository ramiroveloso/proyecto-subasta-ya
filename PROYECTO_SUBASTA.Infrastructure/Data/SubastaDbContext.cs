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

            // ==========================================
            // CONFIGURACIÓN MÓDULO SUBASTAS
            // ==========================================

            // Precisión de 18,2 para las pujas
            modelBuilder.Entity<Puja>().Property(p => p.Monto).HasPrecision(18, 2);

            // Relación 1 a N: Subasta - Pujas
            modelBuilder.Entity<Subasta>()
                .HasMany(s => s.Pujas)
                .WithOne(p => p.Subasta)
                .HasForeignKey(p => p.SubastaId)
                .OnDelete(DeleteBehavior.Cascade);

            // ==========================================
            // DATOS SEMILLA (SEED DATA OBLIGATORIOS)
            // ==========================================

            // 1. Categorías obligatorias[cite: 1]
            modelBuilder.Entity<Categoria>().HasData(
                new Categoria { Id = 1, Nombre = "Tecnología" },
                new Categoria { Id = 2, Nombre = "Coleccionables" },
                new Categoria { Id = 3, Nombre = "Indumentaria" },
                new Categoria { Id = 4, Nombre = "Vehículos" }
            );

            // 2. Usuarios obligatorios con la propiedad Nombre requerida por la entidad
            modelBuilder.Entity<Usuario>().HasData(
                new Usuario { Id = 1, Nombre = "Vendedor Test", Email = "vendedor@test.com", PasswordHash = "hash_vendedor", FechaRegistro = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                new Usuario { Id = 2, Nombre = "Comprador Líder", Email = "comprador1@test.com", PasswordHash = "hash_c1", FechaRegistro = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                new Usuario { Id = 3, Nombre = "Comprador Habilitado", Email = "comprador2@test.com", PasswordHash = "hash_c2", FechaRegistro = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                new Usuario { Id = 4, Nombre = "Usuario Sin Fondos", Email = "sinfondos@test.com", PasswordHash = "hash_sf", FechaRegistro = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) }
            );

            // 3. Billeteras asociadas según perfil financiero[cite: 1]
            modelBuilder.Entity<Billetera>().HasData(
                new Billetera { Id = 1, UsuarioId = 1, SaldoTotal = 0.00m, SaldoRetenido = 0.00m, SaldoDisponible = 0.00m },
                new Billetera { Id = 2, UsuarioId = 2, SaldoTotal = 150000.00m, SaldoRetenido = 45000.00m, SaldoDisponible = 105000.00m },
                new Billetera { Id = 3, UsuarioId = 3, SaldoTotal = 200000.00m, SaldoRetenido = 0.00m, SaldoDisponible = 200000.00m },
                new Billetera { Id = 4, UsuarioId = 4, SaldoTotal = 500.00m, SaldoRetenido = 0.00m, SaldoDisponible = 500.00m }
            );

            // 4. Subastas (Casos de Prueba)[cite: 1]
            modelBuilder.Entity<Subasta>().HasData(
                new Subasta { Id = 1, VendedorId = 1, CategoriaId = 1, Titulo = "Placa de Video RTX", Descripcion = "GPU de alta gama para diseño y gaming", UrlImagen = "https://images.unsplash.com/photo-1587202372775-e229f172b9d7?w=500", PrecioBase = 30000.00m, IncrementoMinimo = 5000.00m, FechaInicio = DateTime.UtcNow.AddMinutes(-20), FechaFin = DateTime.UtcNow.AddMinutes(25), Estado = "ACTIVA" },
                new Subasta { Id = 2, VendedorId = 1, CategoriaId = 1, Titulo = "Procesador de Alta Gama", Descripcion = "Ideal para estaciones de trabajo", UrlImagen = "https://images.unsplash.com/photo-1591799264318-7e6ef8ddb7ea?w=500", PrecioBase = 50000.00m, IncrementoMinimo = 5000.00m, FechaInicio = DateTime.UtcNow.AddMinutes(-58), FechaFin = DateTime.UtcNow.AddSeconds(90), Estado = "ACTIVA" },
                new Subasta { Id = 3, VendedorId = 1, CategoriaId = 2, Titulo = "Figura Coleccionable Edición Limitada", Descripcion = "Arte y diseño exclusivo", UrlImagen = "https://images.unsplash.com/photo-1607604276583-eef5d076aa5f?w=500", PrecioBase = 15000.00m, IncrementoMinimo = 2000.00m, FechaInicio = DateTime.UtcNow.AddHours(24), FechaFin = DateTime.UtcNow.AddHours(48), Estado = "PROGRAMADA" },
                new Subasta { Id = 4, VendedorId = 1, CategoriaId = 3, Titulo = "Campera de Cuero Vintage", Descripcion = "Indumentaria clásica", UrlImagen = "https://images.unsplash.com/photo-1551028719-00167b16eac5?w=500", PrecioBase = 20000.00m, IncrementoMinimo = 2000.00m, FechaInicio = DateTime.UtcNow.AddDays(-2), FechaFin = DateTime.UtcNow.AddHours(-2), Estado = "FINALIZADA" },
                new Subasta { Id = 5, VendedorId = 1, CategoriaId = 4, Titulo = "Repuesto Clásico de Vehículo", Descripcion = "Sin ofertas registradas", UrlImagen = "https://images.unsplash.com/photo-1486006920555-c77dce18193b?w=500", PrecioBase = 80000.00m, IncrementoMinimo = 10000.00m, FechaInicio = DateTime.UtcNow.AddDays(-3), FechaFin = DateTime.UtcNow.AddDays(-1), Estado = "DESIERTA" }
            );

            // 5. Historial de las 2 ofertas previas en la subasta activa[cite: 1]
            modelBuilder.Entity<Puja>().HasData(
                new Puja { Id = 1, SubastaId = 1, UsuarioId = 3, Monto = 35000.00m, FechaCreacion = DateTime.UtcNow.AddMinutes(-15) },
                new Puja { Id = 2, SubastaId = 1, UsuarioId = 2, Monto = 45000.00m, FechaCreacion = DateTime.UtcNow.AddMinutes(-5) }
            );

            // 6. Transacciones en el libro mayor (Ledger)[cite: 1]
            modelBuilder.Entity<TransactionLedger>().HasData(
                new TransactionLedger { Id = 1, BilleteraId = 2, Tipo = TipoTransaccion.DEPOSITO, Monto = 150000.00m, Fecha = DateTime.UtcNow.AddDays(-1), SubastaId = null },
                new TransactionLedger { Id = 2, BilleteraId = 2, Tipo = TipoTransaccion.RETENCION, Monto = 45000.00m, Fecha = DateTime.UtcNow.AddMinutes(-5), SubastaId = 1 }
            );
        }
    }
}