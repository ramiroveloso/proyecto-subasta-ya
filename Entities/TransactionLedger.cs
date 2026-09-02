using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PROYECTO_SUBASTA.Entities
{
    public enum TipoTransaccion
    {
        DEPOSITO,
        RETENCION,
        LIBERACION,
        PAGO,
        COBRO
    }

    public class TransactionLedger
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int BilleteraId { get; set; }
        public Billetera Billetera { get; set; } = null!;

        [Required]
        public TipoTransaccion Tipo { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Monto { get; set; }

        public DateTime Fecha { get; set; } = DateTime.UtcNow;

        public int? SubastaId { get; set; }
    }
}