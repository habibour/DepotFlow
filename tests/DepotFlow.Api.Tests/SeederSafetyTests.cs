using DepotFlow.Seeder;

namespace DepotFlow.Api.Tests;

// Covers spec S3-03 (safety checks): the seeder must never reset anything but a local benchmark database.
public class SeederSafetyTests
{
    [Theory]
    [InlineData("localhost")]
    [InlineData("localhost,1433")]
    [InlineData("127.0.0.1,1433")]
    [InlineData("tcp:localhost,1433")]
    [InlineData(".")]
    public void Local_servers_are_allowed(string dataSource) => ResetGuard.RequireLocalHost(dataSource);

    [Theory]
    [InlineData("db.example.com")]
    [InlineData("10.0.0.5,1433")]
    [InlineData("prod-sql")]
    [InlineData("localhost.evil.com")]
    public void Remote_servers_are_refused(string dataSource) =>
        Assert.Throws<InvalidOperationException>(() => ResetGuard.RequireLocalHost(dataSource));

    [Theory]
    [InlineData("DepotFlowBench")]
    [InlineData("depotflowbench")]
    [InlineData("MyBenchmark")]
    public void Benchmark_databases_may_be_reset(string database) => ResetGuard.RequireBenchDatabase(database);

    [Theory]
    [InlineData("DepotFlow")]
    [InlineData("master")]
    [InlineData("Production")]
    public void Other_databases_are_never_reset(string database)
    {
        var error = Assert.Throws<InvalidOperationException>(() => ResetGuard.RequireBenchDatabase(database));
        Assert.Contains("must contain 'Bench'", error.Message);
    }

    [Fact]
    public void The_documented_command_is_accepted()
    {
        // README: --connection "Server=localhost,1433;Database=DepotFlowBench;..." --reset, with the default --database.
        var options = SeederOptions.Parse(["--visits", "1000000", "--seed", "42", "--reset", "--connection", "Server=localhost,1433;Database=DepotFlowBench"]);

        Assert.True(options.Reset);
        ResetGuard.RequireBenchDatabase(options.Database);
    }
}
