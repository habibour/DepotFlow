namespace DepotFlow.Infrastructure.Yard;

/// <summary>Yard layout, from the "Yard" configuration section. Tests use a tiny yard.</summary>
public sealed class YardOptions
{
    public const string SectionName = "Yard";

    public string Blocks { get; set; } = "ABCD";
    public int Rows { get; set; } = 10;
    public int Bays { get; set; } = 10;
    public int Tiers { get; set; } = 4;
}
