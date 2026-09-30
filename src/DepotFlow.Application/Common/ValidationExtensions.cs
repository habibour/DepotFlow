using FluentValidation;
using FluentValidation.Results;

namespace DepotFlow.Application.Common;

public static class ValidationExtensions
{
    /// <summary>Validates and returns null when valid, otherwise a validation <see cref="Error"/> keyed by camelCase field name.</summary>
    public static async Task<Error?> CheckAsync<T>(this IValidator<T> validator, T instance, CancellationToken cancellationToken)
    {
        var result = await validator.ValidateAsync(instance, cancellationToken);
        return result.IsValid ? null : result.ToError();
    }

    private static Error ToError(this ValidationResult result) =>
        Error.Validation(result.Errors
            .GroupBy(e => ToCamelCase(e.PropertyName))
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()));

    private static string ToCamelCase(string name) =>
        name.Length == 0 ? name : char.ToLowerInvariant(name[0]) + name[1..];
}
