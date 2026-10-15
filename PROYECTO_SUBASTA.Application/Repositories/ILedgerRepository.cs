using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PROYECTO_SUBASTA.Domain.Entities;

namespace PROYECTO_SUBASTA.Application.Repositories
{
    public interface ILedgerRepository : IRepository<TransactionLedger>
    {
        Task<List<TransactionLedger>> ObtenerPorSubastaAsync(int subastaId, CancellationToken cancellationToken = default);
        Task<List<TransactionLedger>> ObtenerPorBilleteraAsync(int billeteraId, CancellationToken cancellationToken = default);
        Task<bool> ExistePagoPorSubastaAsync(int subastaId, CancellationToken cancellationToken = default);
        Task RegistrarAsync(TransactionLedger ledger, CancellationToken cancellationToken = default);
    }
}
