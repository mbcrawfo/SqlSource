using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Shouldly;
using SqlSource.Testing.EndToEnd;
using Xunit;

namespace SqlSource.Tests.SqlServer.EndToEnd;

// The types that SQL Server 2025 brings, json and vector: both rows of new_types into NewTypesDto.  The table is made
// on a server of major 17 or later alone, so the test skips below it.
[Collection(UsesSqlServer.Name)]
public sealed class NewTypesQueryTests(SqlServerContainer sqlServer)
{
    [Fact]
    public async Task QueryAsync_ListNewTypes_ReadsBothRows()
    {
        sqlServer.SkipBelow(17, "SQL Server before 2025 has no json or vector.");
        await using var connection = EndToEndSources.App(sqlServer);
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        var rows = (await connection.QueryAsync<NewTypesDto>(NewTypeQueries.ListNewTypes)).ToList();

        rows.Count.ShouldBe(2);
        SqlServerAllTypesRows.ShouldBeTheFullNewTypesRow(rows[0]);
        SqlServerAllTypesRows.ShouldBeTheNullNewTypesRow(rows[1]);
    }
}
