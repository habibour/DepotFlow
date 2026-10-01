namespace DepotFlow.Seeder;

/// <summary>
/// The safety rules that stop the seeder from touching anything it should not: it only talks to a SQL Server on this
/// machine, and --reset (which drops the whole database) only works on a database whose name contains "Bench".
/// </summary>
internal static class ResetGuard
{
    /// <param name="dataSource">The Server / Data Source part of a connection string, for example "localhost,1433".</param>
    public static void RequireLocalHost(string dataSource)
    {
        var host = dataSource.Split(',')[0].Replace("tcp:", "", StringComparison.OrdinalIgnoreCase).Trim();
        if (host is not ("localhost" or "127.0.0.1" or "."))
        {
            throw new InvalidOperationException($"The seeder only runs against a local SQL Server, not '{host}'.");
        }
    }

    public static void RequireBenchDatabase(string database)
    {
        if (!database.Contains("Bench", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"--reset drops the database. Refusing: '{database}' must contain 'Bench' (the default is '{SeederOptions.DefaultDatabase}').");
        }
    }
}
