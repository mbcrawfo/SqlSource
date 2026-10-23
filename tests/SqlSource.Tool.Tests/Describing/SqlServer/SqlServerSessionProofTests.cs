using System.Collections.Generic;
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

// The session over a capture the test writes: the proofs that correct the server's nullability, the call "operands"
// that asks about their operands, after "columns" and before "table", and a describe that goes on whatever "operands"
// answers.  No driver runs.
public sealed class SqlServerSessionProofTests
{
    private const string Shown = "SELECT id, COALESCE(nickname, name) AS shown FROM dbo.users";
    private const string ShownOperands = "SELECT nickname\nAS [1],\nname\nAS [2]\nFROM dbo.users";

    private static readonly SqlServerColumnRow NicknameColumn = NameColumn with
    {
        Name = "nickname",
        IsNullable = true,
        SystemTypeName = "nvarchar(50)",
        MaxLength = 100,
        SourceColumn = "nickname",
    };

    [Fact]
    public async Task DescribeAsync_FormNeedsOperands_AsksOperandsBeforeTable()
    {
        var operands = new SqlServerColumnsRequest(ShownOperands, null);
        var script = Script(Shown, [IdColumn, Expression(2, "shown"), Rowstat with { ColumnOrdinal = 3 }])
            .Add("operands", operands, new SqlServerColumnsAnswer([Operand(1, true), Operand(2, false)], null, null));

        var (described, asked) = await script.DescribeAsync(Of(SqlDialectRules.SqlServer, Shown));

        asked.ShouldBe([
            Call("parameters", new SqlServerParametersRequest(Shown, null)),
            Call("columns", new SqlServerColumnsRequest(Shown, null)),
            Call("operands", operands),
        ]);
        Nullability(described)
            .ShouldBe([(false, SidecarValues.NullableSource.Server), (false, SidecarValues.NullableSource.Expression)]);
    }

    // Every column comes from dbo.users, so "table" is asked, after "operands".
    [Fact]
    public async Task DescribeAsync_OperandOfOneTable_AsksTableAfterOperands()
    {
        const string Sql = "SELECT id, nickname FROM dbo.users";
        var operands = new SqlServerColumnsRequest("SELECT nickname\nAS [1]\nFROM dbo.users", null);
        var table = new SqlServerTableRequest("dbo", "users");
        var script = Script(Sql, [IdColumn, NicknameColumn, Rowstat with { ColumnOrdinal = 3 }])
            .Add("operands", operands, new SqlServerColumnsAnswer([Operand(1, true), Rowstat], null, null))
            .Add("table", table, new SqlServerColumnsAnswer([IdColumn, NicknameColumn, Rowstat], null, null));

        var (described, asked) = await script.DescribeAsync(Of(SqlDialectRules.SqlServer, Sql));

        asked.Select(static call => call.Method).ShouldBe(["parameters", "columns", "operands", "table"]);
        asked[2].ShouldBe(Call("operands", operands));
        Nullability(described)
            .ShouldBe([(false, SidecarValues.NullableSource.Server), (true, SidecarValues.NullableSource.Server)]);
        described.Value.ShouldNotBeNull().TableMatch.ShouldBe(SidecarValues.TableMatch.Matched);
    }

    // "operands" whose connection was lost, whose describe answers error rows, or whose rows are not the operands:
    // nothing is reported, the proofs of the text alone hold, and the others keep the server's answer.
    [Theory]
    [InlineData("lost")]
    [InlineData("error rows")]
    [InlineData("short")]
    public async Task DescribeAsync_OperandsFails_KeepsTextProofsAndGoesOn(string how)
    {
        const string Sql = "SELECT COUNT(*) AS n, COALESCE(nickname, name) AS shown FROM dbo.users";
        var answer = how switch
        {
            "lost" => new SqlServerColumnsAnswer(null, null, Lost),
            "error rows" => new SqlServerColumnsAnswer(
                null,
                [new SqlServerDescribeError(207, 2, "Invalid column name 'nickname'.")],
                null
            ),
            _ => new SqlServerColumnsAnswer([Operand(1, false)], null, null),
        };
        var script = Script(Sql, [Expression(1, "n"), Expression(2, "shown")])
            .Add("operands", new SqlServerColumnsRequest(ShownOperands, null), answer);

        var (described, asked) = await script.DescribeAsync(Of(SqlDialectRules.SqlServer, Sql));

        asked.Select(static call => call.Method).ShouldBe(["parameters", "columns", "operands"]);
        Nullability(described)
            .ShouldBe([(false, SidecarValues.NullableSource.Expression), (true, SidecarValues.NullableSource.Server)]);
        described.Value.ShouldNotBeNull().TableMatch.ShouldBe(SidecarValues.TableMatch.NoOrigin);
    }

    [Fact]
    public async Task DescribeAsync_NothingToAsk_NoOperandsCall()
    {
        const string Sql = "SELECT COUNT(*) AS n FROM dbo.users";
        var script = Script(Sql, [Expression(1, "n")]);

        var (described, asked) = await script.DescribeAsync(Of(SqlDialectRules.SqlServer, Sql));

        asked.Select(static call => call.Method).ShouldBe(["parameters", "columns"]);
        Nullability(described).ShouldBe([(false, SidecarValues.NullableSource.Expression)]);
    }

    // "operands" is declared as "columns" is: every parameter at its described type.
    [Fact]
    public async Task DescribeAsync_Parameters_OperandsDeclaredAsColumns()
    {
        const string Sql = "SELECT COALESCE(nickname, name) AS shown FROM dbo.users WHERE id = @x";
        var operands = new SqlServerColumnsRequest(
            "SELECT nickname\nAS [1],\nname\nAS [2]\nFROM dbo.users WHERE id = @x",
            "@x int"
        );
        var script = new SqlServerScript()
            .Add("parameters", new SqlServerParametersRequest(Sql, null), new SqlServerParametersAnswer([IntRow], null))
            .Add(
                "columns",
                new SqlServerColumnsRequest(Sql, "@x int"),
                new SqlServerColumnsAnswer([Expression(1, "shown")], null, null)
            )
            .Add("operands", operands, new SqlServerColumnsAnswer([Operand(1, true), Operand(2, false)], null, null));

        var (described, asked) = await script.DescribeAsync(Of(SqlDialectRules.SqlServer, Sql));

        asked[^1].ShouldBe(Call("operands", operands));
        Nullability(described).ShouldBe([(false, SidecarValues.NullableSource.Expression)]);
    }

    // A script of a query with no parameter whose "columns" answers the rows.
    private static SqlServerScript Script(string sql, IReadOnlyList<SqlServerColumnRow> rows) =>
        new SqlServerScript()
            .Add("parameters", new SqlServerParametersRequest(sql, null), new SqlServerParametersAnswer([], null))
            .Add("columns", new SqlServerColumnsRequest(sql, null), new SqlServerColumnsAnswer(rows, null, null));

    // The row of a column of no origin that the server answers nullable.
    private static SqlServerColumnRow Expression(int ordinal, string name) =>
        IdColumn with
        {
            ColumnOrdinal = ordinal,
            Name = name,
            IsNullable = true,
            SourceDatabase = null,
            SourceSchema = null,
            SourceTable = null,
            SourceColumn = null,
            SourceType = null,
            IsIdentityColumn = null,
            IsComputedColumn = null,
        };

    // The row of the operand numbered n, as the server answers "AS [n]".
    private static SqlServerColumnRow Operand(int number, bool nullable) =>
        Expression(number, number.ToString(System.Globalization.CultureInfo.InvariantCulture)) with
        {
            IsNullable = nullable,
        };

    private static List<(bool? Nullable, string? Source)> Nullability(
        Outcome<QueryDescription, DescribeFailure> described
    ) =>
        [
            .. described
                .Value.ShouldNotBeNull()
                .Columns.ShouldNotBeNull()
                .Select(static column => (column.Nullable, column.NullableSource)),
        ];
}
