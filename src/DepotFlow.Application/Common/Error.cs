namespace DepotFlow.Application.Common;

public enum ErrorType
{
    Validation,      // 400: the request shape is wrong
    NotFound,        // 404
    Conflict,        // 409
    Unprocessable    // 422: well-formed but breaks a business rule
}

/// <summary>A business failure. <see cref="Code"/> is the stable machine-readable value clients switch on.</summary>
public sealed record Error(
    string Code,
    string Message,
    ErrorType Type,
    IReadOnlyDictionary<string, string[]>? ValidationErrors = null)
{
    public static Error Validation(IReadOnlyDictionary<string, string[]> errors) =>
        new("validation_failed", "One or more validation errors occurred.", ErrorType.Validation, errors);

    public static Error NotFound(string code, string message) => new(code, message, ErrorType.NotFound);
    public static Error Conflict(string code, string message) => new(code, message, ErrorType.Conflict);
    public static Error Unprocessable(string code, string message) => new(code, message, ErrorType.Unprocessable);
}
