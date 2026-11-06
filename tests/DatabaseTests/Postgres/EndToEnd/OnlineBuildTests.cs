using System;
using System.Threading.Tasks;
using SqlSource.Tool.Testing;
using Xunit;

namespace SqlSource.DatabaseTests.Postgres.EndToEnd;

// "dotnet build" of a copy of ErrorPaths that goes online against the container: the build runs the tool beside the
// tests on the database "fixtures" and compiles the sidecar that the run created, a query that the server refuses
// fails the build at its file, and a broken settings file is listed once.  Each test makes the table of the query and
// drops it again.  OnlineBuilds of tests/SqlSource.Tool.Testing holds the steps, and ErrorPathTests the statements of
// PostgreSQL.
[Collection(UsesPostgres.Name)]
public sealed class OnlineBuildTests(PostgresDatabases postgres) : IDisposable
{
    private readonly OnlineBuilds _builds = new(ErrorPathTests.Engine(postgres), postgres.ConnectionString);

    public void Dispose() => _builds.Dispose();

    [Fact]
    public async Task Build_OnlineWithNoSidecar_CompilesTheSidecarItsRunCreated() =>
        await _builds.ShouldCompileTheSidecarItCreatedAsync();

    [Fact]
    public async Task Build_OnlineWithAQueryTheServerRefuses_FailsAtTheQuery() =>
        await _builds.ShouldFailAtTheRefusedQueryAsync();

    [Fact]
    public async Task Build_OnlineWithABrokenSettingsFile_ListsItsErrorOnce() =>
        await _builds.ShouldListABrokenSettingsFileOnceAsync();
}
