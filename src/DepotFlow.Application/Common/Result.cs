using System.Diagnostics.CodeAnalysis;

namespace DepotFlow.Application.Common;

/// <summary>Outcome of a use case: either a value or a business <see cref="Error"/>. Exceptions stay for the unexpected.</summary>
public sealed class Result<T>
{
    private Result(T? value, Error? error)
    {
        Value = value;
        Error = error;
    }

    [MemberNotNullWhen(true, nameof(Value))]
    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => Error is null;

    public T? Value { get; }
    public Error? Error { get; }

    public static Result<T> Success(T value) => new(value, null);
    public static Result<T> Failure(Error error) => new(default, error);

    public static implicit operator Result<T>(T value) => Success(value);
    public static implicit operator Result<T>(Error error) => Failure(error);
}
