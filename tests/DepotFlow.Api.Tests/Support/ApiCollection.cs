namespace DepotFlow.Api.Tests.Support;

/// <summary>All API tests share one ApiFactory (one SQL Server container) and run one after another.</summary>
[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFactory>
{
    public const string Name = "Api";
}
