namespace DepotFlow.Application;

/// <summary>
/// A database transaction. Everything saved while it is open commits together on <see cref="CommitAsync"/>;
/// disposing it without committing rolls everything back.
/// </summary>
public interface ITransactionScope : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken);
}
