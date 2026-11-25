using System.Collections.Generic;
using System.Linq;
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
            .Text(Sql, tokens, statement.With, statement.Body, operands)
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

        var text = OperandsQuery.Text(
            Sql,
            tokens,
            statement.With,
            statement.Body,
            [statement.Body.Items[0].Expression]
        );

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

        OperandsQuery
            .Text(Sql, tokens, statement.With, statement.Body, operands)
            .ShouldBe("SELECT NEXT VALUE FOR dbo.s\nAS [1]");
    }

    // The calls of one item's proof that asks only over the rows: the text Text gives, one place for each operand.
    [Theory]
    [InlineData(
        "WITH c AS (SELECT 1 AS a) SELECT COALESCE(t.x, t.y) AS z FROM t JOIN c ON c.a = t.id WHERE t.id > @p "
            + "GROUP BY t.x, t.y ORDER BY z"
    )]
    [InlineData("SELECT COALESCE(b.x, b.y) AS z FROM a LEFT OUTER JOIN b ON b.k = a.k WHERE b.k = 1")]
    public void Calls_RowsOnly_IsTheTextOfBefore(string sql)
    {
        var tokens = TsqlTokens.Read(sql);
        var statement = SelectStatement.Read(tokens).ShouldNotBeNull();
        var filtered = FilteredRows.Read(tokens, statement.Body, statement.Ctes);
        var proofs = new ExpressionProofs(tokens, statement.Body, filtered);
        _ = proofs.Of(statement.Body.Items.ShouldHaveSingleItem().Expression);
        var count = proofs.Operands.Count;

        var (queries, places) = OperandsQuery.Calls(
            sql,
            tokens,
            statement.With,
            statement.Body,
            proofs,
            filtered,
            filtered.Cuts
        );

        count.ShouldBeGreaterThan(0);
        queries.ShouldBe([
            new ColumnProofs.OperandQuery(
                OperandsQuery.Text(sql, tokens, statement.With, statement.Body, proofs.Operands, filtered.Cuts),
                count
            ),
        ]);
        places.ShouldBe(Enumerable.Range(0, count));
    }

    // The right side of the last FULL JOIN is its source alone after the word FROM: no ON, no WHERE.
    [Fact]
    public void Calls_RightSide_IsTheFromWordAndTheSourceAlone()
    {
        var (queries, places) = Calls(FullJoin, ("rec.invoice_id", OperandScope.RightSide));

        queries.ShouldBe([
            new ColumnProofs.OperandQuery(
                "WITH c AS (SELECT 1 AS a)\nSELECT rec.invoice_id\nAS [1]\nFROM\nreceived AS rec",
                1
            ),
        ]);
        places.ShouldBe([0]);
    }

    // The left side of the last FULL JOIN is the chain before it, its own outer joins kept.
    [Fact]
    public void Calls_LeftSide_IsTheChainBeforeTheFullJoin()
    {
        var (queries, places) = Calls(FullJoin, ("iss.id", OperandScope.LeftSide), ("cr.id", OperandScope.LeftSide));

        queries.ShouldBe([
            new ColumnProofs.OperandQuery(
                "WITH c AS (SELECT 1 AS a)\nSELECT iss.id\nAS [1],\ncr.id\nAS [2]\n"
                    + "FROM issued AS iss LEFT JOIN credited AS cr ON cr.id = iss.id",
                2
            ),
        ]);
        places.ShouldBe([0, 1]);
    }

    // The answers of the rows' call come first, then the left side's, then the right side's, whatever order the
    // operands are numbered in.  An operand of the same text in two scopes is asked in each.
    [Fact]
    public void Calls_Places_PutRowsBeforeTheSides()
    {
        var (queries, places) = Calls(
            FullJoin,
            ("rec.invoice_id", OperandScope.RightSide),
            ("iss.id", OperandScope.Rows),
            ("iss.id", OperandScope.LeftSide)
        );

        queries
            .Select(static query => query.Sql.Split('\n')[1])
            .ShouldBe(["SELECT iss.id", "SELECT iss.id", "SELECT rec.invoice_id"]);
        queries.Select(static query => query.Count).ShouldBe([1, 1, 1]);
        places.ShouldBe([2, 0, 1]);
    }

    // A side with no FULL JOIN to be one of is asked nothing, and its operand has a place where no answer is.
    [Fact]
    public void Calls_SideWithoutAFullJoin_AsksNothingForIt()
    {
        var (queries, places) = Calls(
            "SELECT 1 AS n FROM issued AS iss LEFT JOIN received AS rec ON rec.invoice_id = iss.id",
            ("iss.id", OperandScope.Rows),
            ("rec.invoice_id", OperandScope.RightSide)
        );

        queries.ShouldHaveSingleItem().Count.ShouldBe(1);
        places[0].ShouldBe(0);
        places[1].ShouldBe(OperandsQuery.Unasked);
    }

    // The hidden row of browse mode 2 is no operand's.
    [Fact]
    public void AnswersOf_Rows_EachOperandsAnswer()
    {
        var answer = new SqlServerColumnsAnswer(
            [OperandRow(1, nullable: false), OperandRow(2, nullable: true), Rowstat with { ColumnOrdinal = 3 }],
            null,
            null
        );

        OperandsQuery.AnswersOf(answer, 2).ShouldBe(Answers(true, false));
    }

    // The server could not say: the operand is not proved.
    [Fact]
    public void AnswersOf_NullabilityUnknown_IsNotNotNull()
    {
        var answer = new SqlServerColumnsAnswer([OperandRow(1, nullable: null)], null, null);

        OperandsQuery.AnswersOf(answer, 1).ShouldBe(Answers(false));
    }

    // A CLR type, by its assembly or by the name of one of the system's, and a row of no type, may be a CLR type; an
    // alias type such as sysname is none.  An answered row is one or the other, Clr or NotClr.  Each row is run with
    // the operand answered null and not null.
    [Theory]
    [MemberData(nameof(ClrTypeRows))]
    public void AnswersOf_Type_NotClrAsTheRowSays(
        string? systemType,
        string? userSchema,
        string? userName,
        string? assembly,
        bool notClr,
        bool nullable
    )
    {
        var row = OperandRow(1, nullable: nullable) with
        {
            SystemTypeName = systemType,
            UserTypeSchema = userSchema,
            UserTypeName = userName,
            AssemblyQualifiedTypeName = assembly,
        };

        OperandsQuery
            .AnswersOf(new SqlServerColumnsAnswer([row], null, null), 1)
            .ShouldBe([new OperandFacts(NotNull: !nullable, notClr, Clr: !notClr)]);
    }

    // Only a column of a table that is not computed is read twice as one value: a view's or a function's column may be
    // an expression, and a row with no source column is one.
    [Theory]
    [InlineData("U", false, "x", true)]
    [InlineData("U", true, "x", false)]
    [InlineData("U", null, "x", false)]
    [InlineData("U", false, null, false)]
    [InlineData("V", false, "x", false)]
    [InlineData("IF", false, "x", false)]
    [InlineData("TF", false, "x", false)]
    [InlineData(null, false, "x", false)]
    public void AnswersOf_Source_StoredAsTheRowSays(string? sourceType, bool? computed, string? column, bool stored)
    {
        var row = OperandRow(1, nullable: true) with
        {
            SourceType = sourceType,
            IsComputedColumn = computed,
            SourceColumn = column,
        };

        OperandsQuery
            .AnswersOf(new SqlServerColumnsAnswer([row], null, null), 1)
            .ShouldNotBeNull()
            .ShouldHaveSingleItem()
            .Stored.ShouldBe(stored);
    }

    public static TheoryData<string?, string?, string?, string?, bool, bool> ClrTypeRows()
    {
        (string? SystemType, string? UserSchema, string? UserName, string? Assembly, bool NotClr)[] rows =
        [
            ("geometry", "sys", "geometry", "Microsoft.SqlServer.Types.SqlGeometry, Microsoft.SqlServer.Types", false),
            ("dbo.Point", "dbo", "Point", "Shapes.Point, Shapes", false),
            ("geography", null, null, null, false),
            ("HierarchyId", null, null, null, false),
            (null, null, null, null, false),
            ("nvarchar(128)", "sys", "sysname", null, true),
            ("int", null, null, null, true),
        ];
        var data = new TheoryData<string?, string?, string?, string?, bool, bool>();
        foreach (var (systemType, userSchema, userName, assembly, notClr) in rows)
        {
            data.Add(systemType, userSchema, userName, assembly, notClr, true);
            data.Add(systemType, userSchema, userName, assembly, notClr, false);
        }

        return data;
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
    public void AnswersOf_Unusable_IsNull(string why) => OperandsQuery.AnswersOf(Unusable[why], 2).ShouldBeNull(why);

    // A FROM whose last FULL JOIN has a LEFT JOIN on its left side, behind a WITH clause and before a WHERE.
    private const string FullJoin =
        "WITH c AS (SELECT 1 AS a) SELECT 1 AS n FROM issued AS iss LEFT JOIN credited AS cr ON cr.id = iss.id "
        + "FULL JOIN received AS rec ON rec.invoice_id = iss.id WHERE iss.id > 0";

    // The calls for the operands, each the first run of tokens of its text in the SQL, asked in its scope.
    private static (IReadOnlyList<ColumnProofs.OperandQuery> Queries, IReadOnlyList<int> Places) Calls(
        string sql,
        params (string Text, OperandScope Scope)[] operands
    )
    {
        var tokens = TsqlTokens.Read(sql);
        var statement = SelectStatement.Read(tokens).ShouldNotBeNull();
        var filtered = FilteredRows.Read(tokens, statement.Body, statement.Ctes);
        return OperandsQuery.Calls(
            sql,
            tokens,
            statement.With,
            statement.Body,
            [.. operands.Select(operand => RangeOf(sql, tokens, operand.Text))],
            [.. operands.Select(static operand => operand.Scope)],
            filtered,
            filtered.Cuts
        );
    }

    private static TokenRange RangeOf(string sql, IReadOnlyList<TsqlToken> tokens, string text)
    {
        for (var start = 0; start < tokens.Count; start++)
        {
            for (var end = start + 1; end <= tokens.Count; end++)
            {
                if (sql[tokens[start].Start..tokens[end - 1].End] == text)
                {
                    return new TokenRange(start, end);
                }
            }
        }

        throw new ShouldAssertException($"No tokens of {text}");
    }

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
