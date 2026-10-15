using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PROYECTO_SUBASTA.Application.DTOs;
using PROYECTO_SUBASTA.Application.Mappers;
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
        private readonly ILedgerRepository _ledgerRepository;

        public ObtenerBilleteraPorUsuarioQueryHandler(
            IBilleteraRepository billeteraRepository,
            ILedgerRepository ledgerRepository)
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

            var movimientos = await _ledgerRepository.ObtenerPorBilleteraAsync(billetera.Id, cancellationToken);
            var movimientosUsuario = movimientos
                .Select(LedgerMapper.MapearADto)
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
        private readonly ILedgerRepository _ledgerRepository;

        public ObtenerMovimientosQueryHandler(
            IBilleteraRepository billeteraRepository,
            ILedgerRepository ledgerRepository)
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

            var movimientos = await _ledgerRepository.ObtenerPorBilleteraAsync(billetera.Id, cancellationToken);
            return movimientos
                .Select(LedgerMapper.MapearADto)
                .ToList();
        }
    }
}
