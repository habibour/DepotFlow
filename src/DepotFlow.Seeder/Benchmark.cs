namespace DepotFlow.Seeder;

/// <summary>The report benchmark harness. Implemented in the benchmark block; a stub until then.</summary>
internal static class Benchmark
{
    public static Task<int> RunAsync(string connectionString, Action<string> log)
    {
        log("--bench is not implemented yet.");
        return Task.FromResult(2);
    }
}
