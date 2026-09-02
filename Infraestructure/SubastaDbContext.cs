using Microsoft.EntityFrameworkCore;
using PROYECTO_SUBASTA.Entities;

namespace PROYECTO_SUBASTA.Infraestructure
{
    public class SubastaDbContext : DbContext
    {
        public SubastaDbContext(DbContextOptions<SubastaDbContext> options) : base(options)
        {
        }

        // Tus Entidades
        public DbSet<Usuario> Usuarios { get; set; }
        public DbSet<Billetera> Billeteras { get; set; }
        public DbSet<TransactionLedger> TransactionLedgers { get; set; }

        // TODO: Compañero, agrega aquí tus DbSets (Subastas, Categorias, Pujas, AuditLogs)

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

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
        }
    }
}