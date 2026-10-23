using System;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Tool.Describing.Postgres;
using SqlSource.Tool.Testing;
using Xunit;

namespace SqlSource.Tests.Postgres.EndToEnd;

// The tool on a scratch project, with the real MSBuild: a sidecar that no longer matches the query or the server is
// out of date under "--check", and a query that the server refuses is reported with no sidecar written for it.  Each
// test makes the table of the query on the database "fixtures" and drops it again.  ErrorPaths of
// tests/SqlSource.Tool.Testing holds the steps, and this class the statements of PostgreSQL.
[Collection(UsesPostgres.Name)]
public sealed class ErrorPathTests(PostgresContainer postgres) : IDisposable
{
    private const string Table = "error_path_widgets";

    private readonly ErrorPaths _paths = new(
        new ErrorPathEngine(
            Table,
            $"CREATE TABLE {Table} (id integer PRIMARY KEY, name text NOT NULL)",
            $"ALTER TABLE {Table} ADD COLUMN note text",
            "error_path_no_such_table",
            postgres.ExecuteAsync,
            static run => run.Describers.Add(new PostgresDescriber())
        ),
        postgres.ConnectionString
    );

    public void Dispose() => _paths.Dispose();

    [Fact]
    public async Task Check_QueryEditedAfterDescribe_IsOutOfDate() =>
        await Should.NotThrowAsync(_paths.QueryEditedAfterDescribeAsync);

    [Fact]
    public async Task Check_ColumnAddedAfterDescribe_IsOutOfDate() =>
        await Should.NotThrowAsync(_paths.ColumnAddedAfterDescribeAsync);

    [Fact]
    public async Task Describe_QueryTheServerRefuses_ReportsItAndWritesNoSidecar() =>
        await Should.NotThrowAsync(_paths.QueryTheServerRefusesAsync);
}
