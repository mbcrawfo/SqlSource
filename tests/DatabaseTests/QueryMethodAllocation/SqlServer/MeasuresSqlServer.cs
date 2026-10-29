using Xunit;

namespace SqlSource.DatabaseTests.QueryMethodAllocation.SqlServer;

// The tests that measure the SQL Server methods against the database "app" of SqlServerApp, which the collection starts
// once for the run.  The collection runs alone: a measure counts what every thread of the process allocates.
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class MeasuresSqlServer : ICollectionFixture<SqlServerApp>
{
    public const string Name = "sqlserver";
}
