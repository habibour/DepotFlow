using DepotFlow.Application;
using Microsoft.EntityFrameworkCore.Storage;

namespace DepotFlow.Infrastructure.Persistence;

internal sealed class EfTransactionScope(IDbContextTransaction transaction) : ITransactionScope
{
    public Task CommitAsync(CancellationToken cancellationToken) => transaction.CommitAsync(cancellationToken);

    public ValueTask DisposeAsync() => transaction.DisposeAsync();
}
