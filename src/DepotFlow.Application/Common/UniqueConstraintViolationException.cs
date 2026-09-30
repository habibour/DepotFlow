namespace DepotFlow.Application.Common;

/// <summary>
/// Thrown by the data layer when the database rejects a write because of a unique index.
/// Use cases catch it to turn a lost race into a friendly 409 instead of a 500.
/// </summary>
public sealed class UniqueConstraintViolationException(string message, Exception inner) : Exception(message, inner);
