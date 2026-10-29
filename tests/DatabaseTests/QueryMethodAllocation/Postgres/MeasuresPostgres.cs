using Xunit;

namespace SqlSource.DatabaseTests.QueryMethodAllocation.Postgres;

// The tests that measure the PostgreSQL methods against the database "app" of PostgresApp, which the collection starts
// once for the run.  The collection runs alone: a measure counts what every thread of the process allocates.
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class MeasuresPostgres : ICollectionFixture<PostgresApp>
{
    public const string Name = "postgres";
}
