using Xunit;

namespace SqlSource.Tests.SqlServer;

// The collection of the allocation test, which counts the bytes that every thread of the process allocates, so it runs
// alone, with parallelization off, after the parallel collections, and on a SqlServerContainer of its own, so that no
// other test's work, and no other test's server, is counted.  The test needs "app" alone, and the container builds all
// four databases as UsesSqlServer's does: a fixture that built fewer would widen shared code to save a few seconds.
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class MeasuresAllocations : ICollectionFixture<SqlServerContainer>
{
    public const string Name = "sqlserver-allocations";
}
