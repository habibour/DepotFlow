using DepotFlow.Domain;

namespace DepotFlow.Domain.Tests;

// Covers spec S1-05 (container number validation).
public class ContainerNumberTests
{
    [Theory]
    [InlineData("CSQU3054383", "CSQU3054383")]
    [InlineData("csqu3054383", "CSQU3054383")]
    [InlineData("  MSCU1234566 ", "MSCU1234566")]
    [InlineData("MAEU1234567", "MAEU1234567")]
    [InlineData("TEMU6543211", "TEMU6543211")]
    [InlineData("HLXU2000001", "HLXU2000001")]
    [InlineData("MSKU0000006", "MSKU0000006")]
    [InlineData("CMAU7654327", "CMAU7654327")]
    [InlineData("ONEU9876541", "ONEU9876541")]
    public void Valid_numbers_are_accepted_and_normalised(string input, string expected)
    {
        var ok = ContainerNumber.TryCreate(input, out var number, out var error);

        Assert.True(ok, error);
        Assert.Equal(expected, number!.Value);
    }

    [Theory]
    [InlineData("CSQU3054384")]    // wrong check digit
    [InlineData("CSQU305438")]     // too short
    [InlineData("CSQU30543831")]   // too long
    [InlineData("CSQ13054383")]    // fourth character must be a letter
    [InlineData("CSQU-3054383")]   // hyphen
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Invalid_numbers_are_rejected(string? input)
    {
        var ok = ContainerNumber.TryCreate(input, out var number, out var error);

        Assert.False(ok);
        Assert.Null(number);
        Assert.False(string.IsNullOrEmpty(error));
    }

    [Fact]
    public void Wrong_check_digit_error_states_the_expected_digit()
    {
        ContainerNumber.TryCreate("CSQU3054384", out _, out var error);

        Assert.Contains("3", error);
    }

    [Fact]
    public void Check_digit_is_computed_from_first_ten_characters()
    {
        Assert.Equal(3, ContainerNumber.ComputeCheckDigit("CSQU305438"));
    }
}
