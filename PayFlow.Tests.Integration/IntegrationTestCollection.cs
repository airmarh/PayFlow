using Xunit;

namespace PayFlow.Tests.Integration;

/// <summary>
/// Shares one <see cref="PayFlowWebApplicationFactory"/> (and therefore one PostgreSQL
/// container) across all tests in the "Integration" collection.
/// </summary>
[CollectionDefinition("Integration")]
public sealed class IntegrationTestCollection : ICollectionFixture<PayFlowWebApplicationFactory>;
