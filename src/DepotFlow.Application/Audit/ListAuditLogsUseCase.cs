using System.Text.Json;
using DepotFlow.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace DepotFlow.Application.Audit;

public sealed class ListAuditLogsUseCase(IDepotFlowDbContext db)
{
    public async Task<PagedResult<AuditLogDto>> ExecuteAsync(
        string? entityName, string? entityKey, string? userId, DateTime? from, DateTime? to,
        int? page, int? pageSize, CancellationToken cancellationToken)
    {
        var (p, size) = Paging.Normalize(page, pageSize);

        var query = db.AuditLogs.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(entityName))
        {
            var name = entityName.Trim();
            query = query.Where(a => a.EntityName == name);
        }

        if (!string.IsNullOrWhiteSpace(entityKey))
        {
            var key = entityKey.Trim();
            query = query.Where(a => a.EntityKey == key);
        }

        if (!string.IsNullOrWhiteSpace(userId))
        {
            var user = userId.Trim();
            query = query.Where(a => a.UserId == user);
        }

        if (from is not null)
        {
            query = query.Where(a => a.OccurredAtUtc >= from);
        }

        if (to is not null)
        {
            query = query.Where(a => a.OccurredAtUtc <= to);
        }

        var total = await query.CountAsync(cancellationToken);
        var rows = await query
            .OrderByDescending(a => a.OccurredAtUtc).ThenByDescending(a => a.Id)   // newest first
            .Skip((p - 1) * size)
            .Take(size)
            .ToListAsync(cancellationToken);

        // Old and new values are stored as JSON text; hand them back as real JSON, not as escaped strings.
        var items = rows
            .Select(a => new AuditLogDto(
                a.Id, a.OccurredAtUtc, a.UserId, a.UserEmail, a.EntityName, a.EntityKey, a.Action,
                a.OldValues is null ? null : JsonDocument.Parse(a.OldValues).RootElement.Clone(),
                a.NewValues is null ? null : JsonDocument.Parse(a.NewValues).RootElement.Clone()))
            .ToList();

        return new PagedResult<AuditLogDto>(items, p, size, total);
    }
}
