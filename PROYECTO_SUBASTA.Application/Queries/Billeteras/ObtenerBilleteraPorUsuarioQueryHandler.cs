using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PROYECTO_SUBASTA.Application.DTOs;
using PROYECTO_SUBASTA.Application.Repositories;
using PROYECTO_SUBASTA.Domain.Entities;

namespace PROYECTO_SUBASTA.Application.Queries.Billeteras
{
    public class ObtenerBilleteraPorUsuarioQuery
    {
        public int UsuarioId { get; set; }
    }

    public class ObtenerBilleteraPorUsuarioQueryHandler
    {
        private readonly IBilleteraRepository _billeteraRepository;
        private readonly IRepository<TransactionLedger> _ledgerRepository;

        public ObtenerBilleteraPorUsuarioQueryHandler(
            IBilleteraRepository billeteraRepository,
            IRepository<TransactionLedger> ledgerRepository)
        {
            _billeteraRepository = billeteraRepository ?? throw new ArgumentNullException(nameof(billeteraRepository));
            _ledgerRepository = ledgerRepository ?? throw new ArgumentNullException(nameof(ledgerRepository));
        }

        public async Task<BilleteraResponseDto?> HandleAsync(ObtenerBilleteraPorUsuarioQuery query, CancellationToken cancellationToken = default)
        {
            if (query == null || query.UsuarioId <= 0)
                return null;

            var billetera = await _billeteraRepository.ObtenerPorUsuarioIdAsync(query.UsuarioId);
            if (billetera == null)
                return null;

            var movimientos = await _ledgerRepository.GetAllAsync();
            var movimientosUsuario = movimientos
                .Where(m => m.BilleteraId == billetera.Id)
                .OrderByDescending(m => m.Fecha)
                .Select(m => new MovimientoLedgerDto
                {
                    Id = m.Id,
                    BilleteraId = m.BilleteraId,
                    TipoTransaccion = m.Tipo.ToString(),
                    Monto = m.Monto,
                    Fecha = m.Fecha,
                    SubastaId = m.SubastaId,
                    Concepto = $"{m.Tipo} {(m.SubastaId.HasValue ? $"en Subasta #{m.SubastaId}" : "de fondos")} por ${m.Monto:N2}"
                })
                .ToList();

            return new BilleteraResponseDto
            {
                Id = billetera.Id,
                UsuarioId = billetera.UsuarioId,
                SaldoTotal = billetera.SaldoTotal,
                SaldoRetenido = billetera.SaldoRetenido,
                SaldoDisponible = billetera.SaldoDisponible,
                Version = billetera.Version,
                Movimientos = movimientosUsuario
            };
        }
    }

    public class ObtenerMovimientosQuery
    {
        public int UsuarioId { get; set; }
    }

    public class ObtenerMovimientosQueryHandler
    {
        private readonly IBilleteraRepository _billeteraRepository;
        private readonly IRepository<TransactionLedger> _ledgerRepository;

        public ObtenerMovimientosQueryHandler(
            IBilleteraRepository billeteraRepository,
            IRepository<TransactionLedger> ledgerRepository)
        {
            _billeteraRepository = billeteraRepository ?? throw new ArgumentNullException(nameof(billeteraRepository));
            _ledgerRepository = ledgerRepository ?? throw new ArgumentNullException(nameof(ledgerRepository));
        }

        public async Task<IEnumerable<MovimientoLedgerDto>> HandleAsync(ObtenerMovimientosQuery query, CancellationToken cancellationToken = default)
        {
            if (query == null || query.UsuarioId <= 0)
                return Enumerable.Empty<MovimientoLedgerDto>();

            var billetera = await _billeteraRepository.ObtenerPorUsuarioIdAsync(query.UsuarioId);
            if (billetera == null)
                return Enumerable.Empty<MovimientoLedgerDto>();

            var movimientos = await _ledgerRepository.GetAllAsync();
            return movimientos
                .Where(m => m.BilleteraId == billetera.Id)
                .OrderByDescending(m => m.Fecha)
                .Select(m => new MovimientoLedgerDto
                {
                    Id = m.Id,
                    BilleteraId = m.BilleteraId,
                    TipoTransaccion = m.Tipo.ToString(),
                    Monto = m.Monto,
                    Fecha = m.Fecha,
                    SubastaId = m.SubastaId,
                    Concepto = $"{m.Tipo} {(m.SubastaId.HasValue ? $"en Subasta #{m.SubastaId}" : "de fondos")} por ${m.Monto:N2}"
                })
                .ToList();
        }
    }
}
