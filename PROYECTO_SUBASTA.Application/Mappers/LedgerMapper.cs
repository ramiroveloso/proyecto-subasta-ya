using PROYECTO_SUBASTA.Application.DTOs;
using PROYECTO_SUBASTA.Domain.Entities;

namespace PROYECTO_SUBASTA.Application.Mappers
{
    public static class LedgerMapper
    {
        public static MovimientoLedgerDto MapearADto(TransactionLedger ledger)
        {
            if (ledger == null) return null!;

            var concepto = ledger.SubastaId.HasValue
                ? $"{ledger.Tipo} en Subasta #{ledger.SubastaId} por ${ledger.Monto:N2}"
                : $"{ledger.Tipo} de fondos por ${ledger.Monto:N2}";

            return new MovimientoLedgerDto
            {
                Id = ledger.Id,
                BilleteraId = ledger.BilleteraId,
                TipoTransaccion = ledger.Tipo.ToString(),
                Monto = ledger.Monto,
                Fecha = ledger.Fecha,
                SubastaId = ledger.SubastaId,
                Concepto = concepto
            };
        }
    }
}
