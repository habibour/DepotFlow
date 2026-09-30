using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace DepotFlow.Domain;

/// <summary>
/// An ISO 6346 container number: 3 owner letters, a category letter (U, J or Z),
/// 6 serial digits and a check digit. Can only be created through <see cref="TryCreate"/>,
/// so any instance is known to be valid.
/// </summary>
public sealed partial record ContainerNumber
{
    [GeneratedRegex("^[A-Z]{3}[UJZ][0-9]{6}[0-9]$")]
    private static partial Regex Pattern();

    private ContainerNumber(string value) => Value = value;

    /// <summary>The normalised 11 character number, for example CSQU3054383.</summary>
    public string Value { get; }

    public override string ToString() => Value;

    public static bool TryCreate(
        string? input,
        [NotNullWhen(true)] out ContainerNumber? value,
        [NotNullWhen(false)] out string? error)
    {
        value = null;

        if (string.IsNullOrWhiteSpace(input))
        {
            error = "Container number is required.";
            return false;
        }

        var normalised = input.Trim().ToUpperInvariant();

        if (!Pattern().IsMatch(normalised))
        {
            error = "Container number must be 3 letters, U/J/Z, 6 digits and a check digit (for example CSQU3054383).";
            return false;
        }

        var expected = ComputeCheckDigit(normalised[..10]);
        if (normalised[10] - '0' != expected)
        {
            error = $"Check digit is wrong; expected {expected}.";
            return false;
        }

        value = new ContainerNumber(normalised);
        error = null;
        return true;
    }

    /// <summary>
    /// ISO 6346 check digit of the first 10 characters. Each character has a value
    /// (digits are themselves; letters start at A=10 and skip multiples of 11),
    /// is multiplied by 2^position, and the sum is taken modulo 11 (10 becomes 0).
    /// </summary>
    public static int ComputeCheckDigit(string first10Characters)
    {
        if (first10Characters.Length != 10)
        {
            throw new ArgumentException("Exactly 10 characters are required.", nameof(first10Characters));
        }

        var sum = 0;
        for (var i = 0; i < 10; i++)
        {
            sum += CharacterValue(first10Characters[i]) * (1 << i);
        }

        return sum % 11 % 10;
    }

    private static int CharacterValue(char c)
    {
        if (c is >= '0' and <= '9')
        {
            return c - '0';
        }

        if (c is < 'A' or > 'Z')
        {
            throw new ArgumentException($"Unexpected character '{c}'.");
        }

        // A=10, B=12, C=13 ... : 11, 22 and 33 are skipped.
        var value = 10 + (c - 'A');
        if (value > 10) value++;   // after A
        if (value > 21) value++;   // after K (value 21 -> L would be 22)
        if (value > 32) value++;   // after U (value 32 -> V would be 33)
        return value;
    }
}
