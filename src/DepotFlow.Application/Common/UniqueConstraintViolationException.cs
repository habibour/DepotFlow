namespace DepotFlow.Application.Common;

/// <summary>
/// Thrown by the data layer when the database rejects a write because of a unique index.
/// Use cases catch it to turn a lost race into a friendly 409 instead of a 500.
/// </summary>
/// <param name="ConstraintName">The index or constraint that was violated, when SQL Server's message names it (see <see cref="IndexNames"/>).</param>
public sealed class UniqueConstraintViolationException(string message, string? constraintName, Exception inner)
    : Exception(message, inner)
{
    public string? ConstraintName { get; } = constraintName;
}
