using Xunit;

namespace SqlSource.DatabaseTests.Postgres;

// The collection of the test that measures what a method allocates, by the process's total, which counts every
// thread.  It runs while no other test does, and has a PostgresContainer of its own, so that no other test's work, on
// the client or in a container's start, is counted.
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class MeasuresAllocations : ICollectionFixture<PostgresContainer>
{
    public const string Name = "allocations";
}
