namespace DepotFlow.Domain;

/// <summary>
/// Raised when code tries to break a domain invariant (for example releasing a visit twice).
/// The application layer checks these conditions first and returns friendly errors;
/// this exception is the last line of defence, not the normal path.
/// </summary>
public sealed class DomainException(string message) : Exception(message);
