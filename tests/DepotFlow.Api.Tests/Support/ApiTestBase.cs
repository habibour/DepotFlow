namespace DepotFlow.Api.Tests.Support;

/// <summary>Gives every test an empty set of containers and visits.</summary>
public abstract class ApiTestBase(ApiFactory factory) : IAsyncLifetime
{
    protected ApiFactory Factory { get; } = factory;

    public Task InitializeAsync() => Factory.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;
}
