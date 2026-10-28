using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SqlSource.Snapshot;
using SqlSource.Tool.Describing.SqlServer;
using SqlSource.Tool.Testing.Fixtures;
using Xunit;

namespace SqlSource.DatabaseTests.SqlServer.Describer;

// The fixture cases of the SQL Server describer against a real server: describing a case's query.sql gives the
// entry of its expected.json.  With SQLSOURCE_RECORD_FIXTURES set it writes the case's capture.json and
// expected.json and compares nothing: tools/record-fixtures.sh runs it so.  The cases are in the repository, in
// tests/SqlSource.Tool.Tests/Fixtures/Describers/mssql, where the tool's tests replay them with no database.
[Collection(UsesSqlServer.Name)]
public sealed class FixtureCasesTests(SqlServerDatabases sqlServer)
{
    private static readonly IReadOnlyList<FixtureCase> Found = FixtureCases.InFolder(RepositoryPaths.DescriberFixtures);

    public static TheoryData<string> Cases => [with(Found.Select(static found => found.Name))];

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task DescribeAsync_Case_GivesTheExpectedEntry(string name) =>
        await FixtureCaseRuns.ShouldDescribeOrRecordAsync(
            Found.Single(found => found.Name == name),
            SidecarValues.Engine.SqlServer,
            static _ => new SqlServerDescriber(),
            sqlServer.ConnectionString
        );
}
