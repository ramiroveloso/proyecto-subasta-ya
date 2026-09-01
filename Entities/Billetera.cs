using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PROYECTO_SUBASTA.Entities
{
    public class Billetera
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int UsuarioId { get; set; }
        public Usuario Usuario { get; set; } = null!;

        [Column(TypeName = "decimal(18,2)")]
        public decimal SaldoTotal { get; set; } = 0;

        [Column(TypeName = "decimal(18,2)")]
        public decimal SaldoRetenido { get; set; } = 0;

        [Column(TypeName = "decimal(18,2)")]
        public decimal SaldoDisponible { get; set; } = 0;

        // Control de concurrencia optimista (Mandatorio)
        [ConcurrencyCheck]
        public int Version { get; set; }

        public ICollection<TransactionLedger> Movimientos { get; set; } = new List<TransactionLedger>();

        // Lógica de negocio encapsulada
        public void RetenerSaldo(decimal monto)
        {
            if (SaldoDisponible < monto)
                throw new InvalidOperationException("Fondos insuficientes.");

            SaldoDisponible -= monto;
            SaldoRetenido += monto;
        }

        public void LiberarSaldo(decimal monto)
        {
            SaldoRetenido -= monto;
            SaldoDisponible += monto;
        }
    }
}