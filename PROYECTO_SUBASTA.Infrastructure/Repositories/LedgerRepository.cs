using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PROYECTO_SUBASTA.Application.Repositories;
using PROYECTO_SUBASTA.Domain.Entities;
using PROYECTO_SUBASTA.Infrastructure.Data;

namespace PROYECTO_SUBASTA.Infrastructure.Repositories
{
    public class LedgerRepository : Repository<TransactionLedger>, ILedgerRepository
    {
        public LedgerRepository(SubastaDbContext context) : base(context)
        {
        }

        public async Task<List<TransactionLedger>> ObtenerPorSubastaAsync(int subastaId, CancellationToken cancellationToken = default)
        {
            return await _context.TransactionLedgers
                .Where(t => t.SubastaId == subastaId)
                .ToListAsync(cancellationToken);
        }

        public async Task<List<TransactionLedger>> ObtenerPorBilleteraAsync(int billeteraId, CancellationToken cancellationToken = default)
        {
            return await _context.TransactionLedgers
                .Where(t => t.BilleteraId == billeteraId)
                .OrderByDescending(t => t.Fecha)
                .ToListAsync(cancellationToken);
        }

        public async Task<bool> ExistePagoPorSubastaAsync(int subastaId, CancellationToken cancellationToken = default)
        {
            return await _context.TransactionLedgers
                .AnyAsync(t => t.SubastaId == subastaId && t.Tipo == TipoTransaccion.PAGO, cancellationToken);
        }

        public async Task RegistrarAsync(TransactionLedger ledger, CancellationToken cancellationToken = default)
        {
            await _context.TransactionLedgers.AddAsync(ledger, cancellationToken);
        }
    }
}
