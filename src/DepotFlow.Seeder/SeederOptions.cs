using System.Globalization;

namespace DepotFlow.Seeder;

/// <summary>
/// Command line: --visits N (default 1,500,000)  --seed N (default 42)  --reset  --bench
///               --database NAME (default DepotFlowBench)  --connection "..."  --as-of yyyy-MM-dd (default today, UTC)
/// </summary>
internal sealed record SeederOptions(
    int Visits, int Seed, bool Reset, bool Bench, string Database, string? Connection, DateOnly AsOf)
{
    public const string DefaultDatabase = "DepotFlowBench";

    public static SeederOptions Parse(string[] args)
    {
        var visits = 1_500_000;
        var seed = 42;
        var reset = false;
        var bench = false;
        var database = DefaultDatabase;
        string? connection = null;
        var asOf = DateOnly.FromDateTime(DateTime.UtcNow);

        for (var i = 0; i < args.Length; i++)
        {
            string Next() => i + 1 < args.Length ? args[++i] : throw new ArgumentException($"{args[i]} needs a value.");

            switch (args[i])
            {
                case "--visits": visits = int.Parse(Next(), CultureInfo.InvariantCulture); break;
                case "--seed": seed = int.Parse(Next(), CultureInfo.InvariantCulture); break;
                case "--reset": reset = true; break;
                case "--bench": bench = true; break;
                case "--database": database = Next(); break;
                case "--connection": connection = Next(); break;
                case "--as-of": asOf = DateOnly.ParseExact(Next(), "yyyy-MM-dd", CultureInfo.InvariantCulture); break;
                default: throw new ArgumentException($"Unknown option '{args[i]}'.");
            }
        }

        if (visits < 100)
        {
            throw new ArgumentException("--visits must be at least 100.");
        }

        return new SeederOptions(visits, seed, reset, bench, database, connection, asOf);
    }
}
