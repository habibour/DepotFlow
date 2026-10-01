using System.Text.Json;
using DepotFlow.Domain.Entities;

namespace DepotFlow.Application.Audit;

public sealed record AuditLogDto(
    long Id,
    DateTime OccurredAtUtc,
    string? UserId,
    string? UserEmail,
    string EntityName,
    string EntityKey,
    AuditAction Action,
    JsonElement? OldValues,
    JsonElement? NewValues);
