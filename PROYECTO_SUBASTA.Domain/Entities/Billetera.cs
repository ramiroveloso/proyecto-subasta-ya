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

        private decimal _saldoTotal;
        private decimal _saldoRetenido;
        private decimal _saldoDisponible;

        [Column(TypeName = "decimal(18,2)")]
        public decimal SaldoTotal
        {
            get => _saldoTotal;
            set
            {
                ValidarPrecisionSaldo(value, "El saldo total");
                _saldoTotal = value;
            }
        }

        [Column(TypeName = "decimal(18,2)")]
        public decimal SaldoRetenido
        {
            get => _saldoRetenido;
            set
            {
                ValidarPrecisionSaldo(value, "El saldo retenido");
                _saldoRetenido = value;
            }
        }

        [Column(TypeName = "decimal(18,2)")]
        public decimal SaldoDisponible
        {
            get => _saldoDisponible;
            set
            {
                ValidarPrecisionSaldo(value, "El saldo disponible");
                _saldoDisponible = value;
            }
        }

        // Control de concurrencia optimista (Mandatorio)
        [ConcurrencyCheck]
        public int Version { get; set; }

        public ICollection<TransactionLedger> Movimientos { get; set; } = new List<TransactionLedger>();

        public static void ValidarPrecisionSaldo(decimal saldo, string nombreCampo = "El saldo")
        {
            if (saldo < 0)
                throw new DominioException($"{nombreCampo} no puede ser negativo.");

            if (decimal.Round(saldo, 2) != saldo)
                throw new DominioException($"{nombreCampo} no puede tener más de dos decimales.");
        }

        public static void ValidarMonto(decimal monto, string operacion = "monto", decimal limiteMaximo = 100_000_000m)
        {
            if (monto <= 0)
                throw new DominioException($"El {operacion} debe ser mayor a cero.");

            if (decimal.Round(monto, 2) != monto)
                throw new DominioException($"El {operacion} no puede tener más de dos decimales.");

            if (monto > limiteMaximo)
                throw new DominioException($"El {operacion} no puede superar el límite permitido de ${limiteMaximo:N2}.");
        }

        // Lógica de negocio encapsulada con control de invariantes y OCC
        public void CargarSaldo(decimal monto)
        {
            ValidarMonto(monto, "monto a cargar");

            SaldoTotal += monto;
            SaldoDisponible += monto;
            Version++;
        }

        public void RetenerSaldo(decimal monto)
        {
            ValidarMonto(monto, "monto a retener");

            if (SaldoDisponible < monto)
                throw new DominioException($"Fondos insuficientes. Saldo disponible actual: ${SaldoDisponible:N2}, monto requerido para retención: ${monto:N2}.");

            SaldoDisponible -= monto;
            SaldoRetenido += monto;
            Version++;
        }

        public void LiberarSaldo(decimal monto)
        {
            ValidarMonto(monto, "monto a liberar");

            if (SaldoRetenido < monto)
                throw new DominioException($"Inconsistencia financiera: No existe retención previa suficiente para liberar este saldo. Saldo retenido actual: ${SaldoRetenido:N2}, monto solicitado a liberar: ${monto:N2}.");

            SaldoRetenido -= monto;
            SaldoDisponible += monto;
            Version++;
        }

        public void DebitarRetencion(decimal monto)
        {
            ValidarMonto(monto, "monto a debitar");

            if (SaldoRetenido < monto)
                throw new DominioException($"Inconsistencia financiera: Saldo retenido insuficiente para debitar liquidación. Retenido actual: ${SaldoRetenido:N2}, débito solicitado: ${monto:N2}.");

            SaldoRetenido -= monto;
            SaldoTotal -= monto;
            Version++;
        }

        public void AcreditarCobro(decimal monto)
        {
            ValidarMonto(monto, "monto a acreditar");

            SaldoTotal += monto;
            SaldoDisponible += monto;
            Version++;
        }
    }
}