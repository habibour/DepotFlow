namespace DepotFlow.Api.Tests.Support;

/// <summary>
/// Gives every test a clean slate: no containers or visits, the clock back to real time, and the standard
/// tiered tariff for shipping lines 1 and 2 (both sizes). Other lines have no tariff.
/// </summary>
public abstract class ApiTestBase(ApiFactory factory) : IAsyncLifetime
{
    protected ApiFactory Factory { get; } = factory;

    public async Task InitializeAsync()
    {
        Factory.Clock.Reset();
        await Factory.ResetAsync();
        await Factory.SeedStandardTariffsAsync(1, 2);
    }

    public Task DisposeAsync() => Task.CompletedTask;
}
