using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Parsing;
using SqlSource.Snapshot;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Describing.SqlServer;
using Xunit;
using static SqlSource.Tool.Testing.DescribeRequests;
using static SqlSource.Tool.Tests.SqlServerScript;
using static SqlSource.Tool.Tests.SqlServerSessionCases;

namespace SqlSource.Tool.Tests.Describing.SqlServer;

// The session over a capture the test writes: the calls a describe asks for the table of its columns, "table", and the
// match it makes with them, in order, and what it makes of their answers.  No driver runs: the connection is a word
// that SqlClient could not open.
public sealed class SqlServerSessionTableTests
{
    private static readonly SqlServerColumnRow NicknameColumn = NameColumn with
    {
        ColumnOrdinal = 3,
        Name = "nickname",
        IsNullable = true,
        SystemTypeName = "nvarchar(50)",
        MaxLength = 100,
        SourceColumn = "nickname",
    };

    // The failure of a describe of the SQL, whose one column is dbo.users.id, when "table" answers as given.
    private static async Task<DescribeFailure> TableAnsweringAsync(string sql, SqlServerColumnsAnswer answer)
    {
        var script = new SqlServerScript()
            .Add("parameters", new SqlServerParametersRequest(sql, null), new SqlServerParametersAnswer([], null))
            .Add(
                "columns",
                new SqlServerColumnsRequest(sql, null),
                new SqlServerColumnsAnswer([IdColumn, Rowstat], null, null)
            )
            .Add("table", new SqlServerTableRequest("dbo", "users"), answer);

        var (described, _) = await script.DescribeAsync(Of(SqlDialectRules.SqlServer, sql));

        return described.Failure.ShouldNotBeNull();
    }

    // A self-join: two columns of one table, whose list is the table's in part, ask "table" once.
    [Fact]
    public async Task DescribeAsync_OneTable_AsksTableOnce()
    {
        const string Sql = "SELECT a.id, b.name FROM dbo.users AS a JOIN dbo.users AS b ON b.id = a.id";
        var columns = new SqlServerColumnsRequest(Sql, null);
        var table = new SqlServerTableRequest("dbo", "users");
        var script = new SqlServerScript()
            .Add("parameters", new SqlServerParametersRequest(Sql, null), new SqlServerParametersAnswer([], null))
            .Add("columns", columns, new SqlServerColumnsAnswer([IdColumn, NameColumn, Rowstat], null, null))
            .Add(
                "table",
                table,
                new SqlServerColumnsAnswer([IdColumn, NameColumn, NicknameColumn, Rowstat], null, null)
            );

        var (described, asked) = await script.DescribeAsync(Of(SqlDialectRules.SqlServer, Sql));

        asked.ShouldBe([
            Call("parameters", new SqlServerParametersRequest(Sql, null)),
            Call("columns", columns),
            Call("table", table),
        ]);
        var description = described.Value.ShouldNotBeNull();
        description.ResultKind.ShouldBe(SidecarResultKind.Rows);
        description.MatchesTable.ShouldBeNull();
        description.TableMatch.ShouldBe(SidecarValues.TableMatch.ColumnsDiffer);
        description.Plan.ShouldBe(SidecarValues.Plan.Skipped);
    }

    [Fact]
    public async Task DescribeAsync_TwoTables_AsksNoTable()
    {
        const string Sql = "SELECT u.id, o.id AS order_id FROM dbo.users AS u JOIN dbo.orders AS o ON o.user_id = u.id";
        var columns = new SqlServerColumnsRequest(Sql, null);
        var orderId = IdColumn with { ColumnOrdinal = 2, Name = "order_id", SourceTable = "orders" };
        var script = new SqlServerScript()
            .Add("parameters", new SqlServerParametersRequest(Sql, null), new SqlServerParametersAnswer([], null))
            .Add("columns", columns, new SqlServerColumnsAnswer([IdColumn, orderId, Rowstat], null, null));

        var (described, asked) = await script.DescribeAsync(Of(SqlDialectRules.SqlServer, Sql));

        asked.Select(call => call.Method).ShouldBe(["parameters", "columns"]);
        var description = described.Value.ShouldNotBeNull();
        description.Columns.ShouldNotBeNull().Count.ShouldBe(2);
        description.MatchesTable.ShouldBeNull();
        description.TableMatch.ShouldBe(SidecarValues.TableMatch.SeveralTables);
        description.Plan.ShouldBe(SidecarValues.Plan.Skipped);
    }

    // COUNT(*) names no source, so no table is asked.
    [Fact]
    public async Task DescribeAsync_NoOrigin_AsksNoTable()
    {
        const string Sql = "SELECT COUNT(*) AS n FROM dbo.users";
        var columns = new SqlServerColumnsRequest(Sql, null);
        var count = IdColumn with
        {
            Name = "n",
            IsNullable = true,
            SourceDatabase = null,
            SourceSchema = null,
            SourceTable = null,
            SourceColumn = null,
            SourceType = null,
            IsIdentityColumn = null,
            IsComputedColumn = null,
        };
        var script = new SqlServerScript()
            .Add("parameters", new SqlServerParametersRequest(Sql, null), new SqlServerParametersAnswer([], null))
            .Add("columns", columns, new SqlServerColumnsAnswer([count], null, null));

        var (described, asked) = await script.DescribeAsync(Of(SqlDialectRules.SqlServer, Sql));

        asked.Select(call => call.Method).ShouldBe(["parameters", "columns"]);
        var description = described.Value.ShouldNotBeNull();
        description.Columns.ShouldBe([
            new SidecarColumn(0, "n", Int, true, SidecarValues.NullableSource.Server, null, null, null),
        ]);
        description.MatchesTable.ShouldBeNull();
        description.TableMatch.ShouldBe(SidecarValues.TableMatch.NoOrigin);
    }

    // The server names a table-valued function as its columns' source, and SELECT * from it fails without its
    // arguments: a column of a function has no origin, so no table is asked.
    [Theory]
    [InlineData("IF")]
    [InlineData("TF")]
    public async Task DescribeAsync_ColumnsOfATableValuedFunction_AsksNoTable(string type)
    {
        const string Sql = "SELECT id FROM dbo.orders_of(1)";
        var columns = new SqlServerColumnsRequest(Sql, null);
        var id = IdColumn with { SourceTable = "orders_of", SourceType = type, IsIdentityColumn = false };
        var script = new SqlServerScript()
            .Add("parameters", new SqlServerParametersRequest(Sql, null), new SqlServerParametersAnswer([], null))
            .Add("columns", columns, new SqlServerColumnsAnswer([id, Rowstat], null, null));

        var (described, asked) = await script.DescribeAsync(Of(SqlDialectRules.SqlServer, Sql));

        asked.Select(call => call.Method).ShouldBe(["parameters", "columns"]);
        var description = described.Value.ShouldNotBeNull();
        description.Columns.ShouldNotBeNull().Single().Origin.ShouldBeNull();
        description.MatchesTable.ShouldBeNull();
        description.TableMatch.ShouldBe(SidecarValues.TableMatch.NoOrigin);
    }

    // A "table" whose connection was lost, or whose describe answers error rows, fails the query at the table match.
    [Fact]
    public async Task DescribeAsync_TableFails_Is225AtTableMatch()
    {
        const string Sql = "SELECT id FROM dbo.users";
        var lost = await TableAnsweringAsync(Sql, new SqlServerColumnsAnswer(null, null, Lost));
        var refused = await TableAnsweringAsync(
            Sql,
            new SqlServerColumnsAnswer(
                null,
                [new SqlServerDescribeError(208, 7, "Invalid object name 'dbo.users'.")],
                null
            )
        );

        lost.Descriptor.Id.ShouldBe("SQLSRC225");
        lost.Step.ShouldBe(DescribeStep.TableMatch);
        lost.ServerLines.ShouldBe([ConnectionErrors.LostConnection]);
        refused.Descriptor.Id.ShouldBe("SQLSRC225");
        refused.Step.ShouldBe(DescribeStep.TableMatch);
        refused.ServerLines.ShouldBe(["Msg 208: Invalid object name 'dbo.users'."]);
    }
}
