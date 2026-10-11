namespace AtmCashManagement.IntegrationTests.Infrastructure;

/// <summary>Shares one API host and one database container across all integration test classes.</summary>
[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFactory>
{
    public const string Name = "api";
}
