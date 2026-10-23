using System.Collections.Generic;
using Shouldly;
using SqlSource.Tool.Describing.SqlServer;
using SqlSource.Tool.Describing.SqlServer.Proofs;
using Xunit;
using static SqlSource.Tool.Tests.SqlServerScript;

namespace SqlSource.Tool.Tests.Describing.SqlServer.Proofs;

// The text of "operands", built from the query's own characters, and what its answer says of each operand.
public sealed class OperandsQueryTests
{
    // The WITH clause stays, each operand is aliased on a line of its own, and the FROM runs to the end of the WHERE:
    // the GROUP BY and the ORDER BY are left out.
    [Fact]
    public void Text_WithAndWhere_RearrangesTheQuery()
    {
        const string Sql =
            "WITH c AS (SELECT 1 AS a) SELECT COALESCE(t.x, t.y) AS z FROM t JOIN c ON c.a = t.id WHERE t.id > @p "
            + "GROUP BY t.x, t.y ORDER BY z";
        var (tokens, statement, operands) = Read(Sql);

        OperandsQuery
            .Text(Sql, tokens, statement, operands)
            .ShouldBe(
                "WITH c AS (SELECT 1 AS a)\nSELECT t.x\nAS [1],\nt.y\nAS [2]\n"
                    + "FROM t JOIN c ON c.a = t.id WHERE t.id > @p"
            );
    }

    // A line comment inside an operand is kept, and the alias stands on the next line, where no comment reaches it.
    [Fact]
    public void Text_LineCommentInOperand_EndsBeforeTheAlias()
    {
        const string Sql = "SELECT COALESCE(t.a, -- x\n t.b) AS c FROM t";
        var tokens = TsqlTokens.Read(Sql);
        var statement = SelectStatement.Read(tokens).ShouldNotBeNull();

        var text = OperandsQuery.Text(Sql, tokens, statement, [statement.Body.Items[0].Expression]);

        text.ShouldBe("SELECT COALESCE(t.a, -- x\n t.b)\nAS [1]\nFROM t");
        var read = SelectStatement.Read(TsqlTokens.Read(text)).ShouldNotBeNull();
        read.Body.Items.ShouldHaveSingleItem().Name.ShouldBe("1");
        _ = read.Body.FromThroughWhere.ShouldNotBeNull();
    }

    [Fact]
    public void Text_NoFrom_SelectsOperandsAlone()
    {
        const string Sql = "SELECT CAST(NEXT VALUE FOR dbo.s AS int) AS a";
        var (tokens, statement, operands) = Read(Sql);

        OperandsQuery.Text(Sql, tokens, statement, operands).ShouldBe("SELECT NEXT VALUE FOR dbo.s\nAS [1]");
    }

    // The hidden row of browse mode 2 is no operand's.
    [Fact]
    public void NotNullOf_Rows_EachOperandsAnswer()
    {
        var answer = new SqlServerColumnsAnswer(
            [OperandRow(1, nullable: false), OperandRow(2, nullable: true), Rowstat with { ColumnOrdinal = 3 }],
            null,
            null
        );

        OperandsQuery.NotNullOf(answer, 2).ShouldBe([true, false]);
    }

    // The server could not say: the operand is not proved.
    [Fact]
    public void NotNullOf_NullabilityUnknown_IsNotNotNull()
    {
        var answer = new SqlServerColumnsAnswer([OperandRow(1, nullable: null)], null, null);

        OperandsQuery.NotNullOf(answer, 1).ShouldBe([false]);
    }

    // Each answer that proves no operand of two, by why.
    private static readonly Dictionary<string, SqlServerColumnsAnswer> Unusable = new()
    {
        { "an error", new SqlServerColumnsAnswer(null, null, Lost) },
        {
            "error rows",
            new SqlServerColumnsAnswer(null, [new SqlServerDescribeError(207, 2, "Invalid column name 'x'.")], null)
        },
        { "neither rows nor an error", new SqlServerColumnsAnswer(null, null, null) },
        { "one row for two operands", new SqlServerColumnsAnswer([OperandRow(1, false), Rowstat], null, null) },
        {
            "three rows for two operands",
            new SqlServerColumnsAnswer([OperandRow(1, false), OperandRow(2, false), OperandRow(3, false)], null, null)
        },
        {
            "rows named out of order",
            new SqlServerColumnsAnswer(
                [OperandRow(1, false) with { Name = "2" }, OperandRow(2, false) with { Name = "1" }],
                null,
                null
            )
        },
        {
            "a row of another name",
            new SqlServerColumnsAnswer([OperandRow(1, false), OperandRow(2, false) with { Name = "b" }], null, null)
        },
    };

    [Theory]
    [InlineData("an error")]
    [InlineData("error rows")]
    [InlineData("neither rows nor an error")]
    [InlineData("one row for two operands")]
    [InlineData("three rows for two operands")]
    [InlineData("rows named out of order")]
    [InlineData("a row of another name")]
    public void NotNullOf_Unusable_IsNull(string why) => OperandsQuery.NotNullOf(Unusable[why], 2).ShouldBeNull(why);

    // The tokens and the statement of the SQL, and the operands its one item's proof asks.
    private static (
        IReadOnlyList<TsqlToken> Tokens,
        SelectStatement Statement,
        IReadOnlyList<TokenRange> Operands
    ) Read(string sql)
    {
        var tokens = TsqlTokens.Read(sql);
        var statement = SelectStatement.Read(tokens).ShouldNotBeNull();
        var proofs = new ExpressionProofs(tokens, statement.Body);
        _ = proofs.Of(statement.Body.Items.ShouldHaveSingleItem().Expression);
        return (tokens, statement, proofs.Operands);
    }
}
