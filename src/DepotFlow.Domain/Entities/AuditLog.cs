namespace DepotFlow.Domain.Entities;

/// <summary>One recorded change to business data. Append-only: the database refuses updates and deletes.</summary>
public class AuditLog
{
    private AuditLog() { }   // for EF Core

    public AuditLog(
        DateTime occurredAtUtc, string? userId, string? userEmail, string entityName, string entityKey,
        AuditAction action, string? oldValues, string? newValues)
    {
        OccurredAtUtc = occurredAtUtc;
        UserId = userId;
        UserEmail = userEmail;
        EntityName = entityName;
        EntityKey = entityKey;
        Action = action;
        OldValues = oldValues;
        NewValues = newValues;
    }

    public long Id { get; private set; }
    public DateTime OccurredAtUtc { get; private set; }

    /// <summary>Null for system actions such as startup seeding.</summary>
    public string? UserId { get; private set; }

    public string? UserEmail { get; private set; }
    public string EntityName { get; private set; } = null!;
    public string EntityKey { get; private set; } = null!;
    public AuditAction Action { get; private set; }

    /// <summary>JSON of the changed properties, old values (updates and deletes).</summary>
    public string? OldValues { get; private set; }

    /// <summary>JSON of the properties, new values (inserts and updates).</summary>
    public string? NewValues { get; private set; }
}
