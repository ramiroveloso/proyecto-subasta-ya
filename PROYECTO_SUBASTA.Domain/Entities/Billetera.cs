using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PROYECTO_SUBASTA.Domain.Exceptions;

namespace PROYECTO_SUBASTA.Domain.Entities
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

        // Lógica de negocio encapsulada con control de invariantes y OCC
        public void CargarSaldo(decimal monto)
        {
            if (monto <= 0)
                throw new DominioException("El monto a cargar debe ser mayor a cero.");

            SaldoTotal += monto;
            SaldoDisponible += monto;
            Version++;
        }

        public void RetenerSaldo(decimal monto)
        {
            if (monto <= 0)
                throw new DominioException("El monto a retener debe ser mayor a cero.");

            if (SaldoDisponible < monto)
                throw new DominioException($"Fondos insuficientes. Saldo disponible actual: ${SaldoDisponible:N2}, monto requerido para retención: ${monto:N2}.");

            SaldoDisponible -= monto;
            SaldoRetenido += monto;
            Version++;
        }

        public void LiberarSaldo(decimal monto)
        {
            if (monto <= 0)
                throw new DominioException("El monto a liberar debe ser mayor a cero.");

            if (SaldoRetenido < monto)
                throw new DominioException($"Inconsistencia financiera: No existe retención previa suficiente para liberar este saldo. Saldo retenido actual: ${SaldoRetenido:N2}, monto solicitado a liberar: ${monto:N2}.");

            SaldoRetenido -= monto;
            SaldoDisponible += monto;
            Version++;
        }

        public void DebitarRetencion(decimal monto)
        {
            if (monto <= 0)
                throw new DominioException("El monto a debitar debe ser mayor a cero.");

            if (SaldoRetenido < monto)
                throw new DominioException($"Inconsistencia financiera: Saldo retenido insuficiente para debitar liquidación. Retenido actual: ${SaldoRetenido:N2}, débito solicitado: ${monto:N2}.");

            SaldoRetenido -= monto;
            SaldoTotal -= monto;
            Version++;
        }

        public void AcreditarCobro(decimal monto)
        {
            if (monto <= 0)
                throw new DominioException("El monto a acreditar debe ser mayor a cero.");

            SaldoTotal += monto;
            SaldoDisponible += monto;
            Version++;
        }
    }
}