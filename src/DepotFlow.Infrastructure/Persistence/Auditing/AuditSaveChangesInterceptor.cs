using System.Text.Json;
using System.Text.Json.Serialization;
using DepotFlow.Application;
using DepotFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;

namespace DepotFlow.Infrastructure.Persistence.Auditing;

/// <summary>
/// Writes an audit row for every insert, update and delete of the business entities, in the same transaction
/// as the change itself. Registered as a scoped service, so there is one instance per DbContext and it can
/// keep per-save state in fields.
///
/// How a save flows through here:
///   SavingChanges      look at what is about to be written; open a transaction if none is open
///   (EF runs the real SQL)
///   SavedChanges       now generated keys exist: write the audit rows, then commit our transaction
///   SaveChangesFailed  roll back, so there are no audit rows for changes that never happened
/// </summary>
public sealed class AuditSaveChangesInterceptor(IClock clock, ICurrentUser? currentUser = null) : SaveChangesInterceptor
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,   // the property names are dictionary keys
        Converters = { new JsonStringEnumConverter() }
    };

    // Not audited: YardSlots (static), Identity tables, InvoiceLines (covered by the invoice) and AuditLogs itself.
    private static bool IsAudited(object entity) =>
        entity is ShippingLine or Container or Visit or Tariff or TariffTier or Invoice;

    private sealed record Pending(EntityEntry Entry, AuditAction Action, string? OldValues, string? NewValues, string? Key);

    private List<Pending> _pending = [];
    private IDbContextTransaction? _ownedTransaction;
    private bool _writingAudit;

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (_writingAudit || eventData.Context is not DepotFlowDbContext db)
        {
            return result;   // this is our own save of the audit rows
        }

        db.ChangeTracker.DetectChanges();
        _pending = Capture(db);

        // If nobody opened a transaction, open one, so the change and its audit rows commit together.
        // (When a use case already has one, we simply join it.)
        if (_pending.Count > 0 && db.Database.CurrentTransaction is null)
        {
            _ownedTransaction = await db.Database.BeginTransactionAsync(cancellationToken);
        }

        return result;
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        if (_writingAudit || _pending.Count == 0 || eventData.Context is not DepotFlowDbContext db)
        {
            return result;
        }

        var pending = _pending;
        _pending = [];
        try
        {
            _writingAudit = true;
            var now = clock.UtcNow;
            var userId = currentUser?.UserIdOrNull;
            var email = currentUser?.Email;

            foreach (var item in pending)
            {
                // Inserts are described now, not before the save: generated values (identity keys, the
                // invoice number) only exist after the database has produced them.
                var (oldValues, newValues, key) = item.Action == AuditAction.Insert
                    ? (null, Serialize(item.Entry.Properties.Where(p => !p.IsTemporary).ToDictionary(p => p.Metadata.Name, p => p.CurrentValue)), KeyOf(item.Entry))
                    : (item.OldValues, item.NewValues, item.Key!);

                db.AuditLogs.Add(new AuditLog(now, userId, email, item.Entry.Metadata.ClrType.Name, key!, item.Action, oldValues, newValues));
            }

            await db.SaveChangesAsync(cancellationToken);   // re-enters the interceptor, which ignores it (_writingAudit)

            if (_ownedTransaction is not null)
            {
                await _ownedTransaction.CommitAsync(cancellationToken);
            }
        }
        catch
        {
            if (_ownedTransaction is not null)
            {
                await _ownedTransaction.RollbackAsync(CancellationToken.None);
            }

            throw;
        }
        finally
        {
            _writingAudit = false;
            await DisposeTransactionAsync();
        }

        return result;
    }

    public override async Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        if (_writingAudit)
        {
            return;
        }

        _pending = [];
        if (_ownedTransaction is not null)
        {
            await _ownedTransaction.RollbackAsync(CancellationToken.None);
            await DisposeTransactionAsync();
        }
    }

    // The synchronous overload cannot do the post-save work, so fail loudly instead of silently skipping the audit.
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result) =>
        _writingAudit ? result : throw new NotSupportedException("Use SaveChangesAsync: auditing needs the asynchronous path.");

    private async Task DisposeTransactionAsync()
    {
        if (_ownedTransaction is not null)
        {
            await _ownedTransaction.DisposeAsync();
            _ownedTransaction = null;
        }
    }

    private static List<Pending> Capture(DepotFlowDbContext db)
    {
        var pending = new List<Pending>();

        foreach (var entry in db.ChangeTracker.Entries().Where(e => IsAudited(e.Entity)))
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    pending.Add(new Pending(entry, AuditAction.Insert, null, null, null));   // described after the save
                    break;

                case EntityState.Modified:
                    var changed = entry.Properties
                        .Where(p => p.IsModified && !Equals(p.OriginalValue, p.CurrentValue))
                        .ToList();
                    if (changed.Count > 0)   // only real changes are audited
                    {
                        pending.Add(new Pending(
                            entry, AuditAction.Update,
                            Serialize(changed.ToDictionary(p => p.Metadata.Name, p => p.OriginalValue)),
                            Serialize(changed.ToDictionary(p => p.Metadata.Name, p => p.CurrentValue)),
                            KeyOf(entry)));
                    }

                    break;

                case EntityState.Deleted:
                    pending.Add(new Pending(
                        entry, AuditAction.Delete,
                        Serialize(entry.Properties.ToDictionary(p => p.Metadata.Name, p => p.OriginalValue)),
                        null, KeyOf(entry)));
                    break;
            }
        }

        return pending;
    }

    private static string KeyOf(EntityEntry entry) =>
        string.Join(",", entry.Metadata.FindPrimaryKey()!.Properties.Select(p => entry.Property(p.Name).CurrentValue));

    private static string Serialize(Dictionary<string, object?> values) => JsonSerializer.Serialize(values, Json);
}
