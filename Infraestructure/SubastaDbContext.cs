using Microsoft.EntityFrameworkCore;
using PROYECTO_SUBASTA.Entities;

namespace PROYECTO_SUBASTA.Infraestructure
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

            // Aseguramos la misma precisión de 18,2 para las pujas, protegiendo la consistencia monetaria del sistema
            modelBuilder.Entity<Puja>().Property(p => p.Monto).HasPrecision(18, 2);

            // Relación 1 a N: Subasta - Pujas (Si se elimina una subasta, se eliminan sus pujas en cascada)
            modelBuilder.Entity<Subasta>()
                .HasMany(s => s.Pujas)
                .WithOne(p => p.Subasta)
                .HasForeignKey(p => p.SubastaId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}