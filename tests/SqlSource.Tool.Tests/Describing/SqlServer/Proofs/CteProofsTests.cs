using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Tool.Describing.SqlServer;
using SqlSource.Tool.Describing.SqlServer.Proofs;
using Xunit;
using static SqlSource.Tool.Tests.Describing.SqlServer.Proofs.ColumnProofsTests;
using static SqlSource.Tool.Tests.SqlServerScript;

namespace SqlSource.Tool.Tests.Describing.SqlServer.Proofs;

// The proofs of a column read from a CTE of several branches, a recursive one among them: every branch proves its item,
// a recursive member with the CTE's own columns of the set proved taken as not null.  The server answers every column
// of a recursive CTE nullable, in the query and in "operands" of a recursive member, which the answers here copy: an
// operand under the CTE's alias is answered nullable.
public sealed partial class CteProofsTests
{
    private const string Tree =
        "WITH tree AS (SELECT c.id, c.parent_id, c.name, CAST(c.name AS nvarchar(400)) AS path, 1 AS depth "
        + "FROM dbo.categories AS c WHERE c.parent_id IS NULL UNION ALL "
        + "SELECT c.id, c.parent_id, c.name, CAST(t.path + N' > ' + c.name AS nvarchar(400)), t.depth + 1 "
        + "FROM dbo.categories AS c JOIN tree AS t ON t.id = c.parent_id) "
        + "SELECT t.id, t.parent_id, t.name, t.path, t.depth FROM tree AS t";

    private static readonly (bool?, string?) Proved = (false, SidecarValues.NullableSource.Expression);
    private static readonly (bool?, string?) Server = (true, SidecarValues.NullableSource.Server);

    [Fact]
    public void Apply_RecursiveCte_ProvesWhatEveryMemberProves()
    {
        var proofs = Read(Tree, Rows("id", "parent_id", "name", "path", "depth"));

        proofs.OperandQueries.Count.ShouldBe(3);
        proofs.OperandQueries[1].Sql.ShouldStartWith("WITH tree AS (");
        proofs
            .OperandQueries[1]
            .Sql.ShouldEndWith(
                "SELECT c.id\nAS [1],\nc.parent_id\nAS [2],\nc.name\nAS [3]\n"
                    + "FROM dbo.categories AS c WHERE c.parent_id IS NULL"
            );
        proofs
            .OperandQueries[2]
            .Sql.ShouldEndWith(
                "SELECT c.id\nAS [1],\nc.parent_id\nAS [2],\nc.name\nAS [3],\nt.path\nAS [4],\nt.depth\nAS [5]\n"
                    + "FROM dbo.categories AS c JOIN tree AS t ON t.id = c.parent_id"
            );
        Nullability(proofs.Apply(Facts(proofs, "c.parent_id"))).ShouldBe([Proved, Server, Proved, Proved, Proved]);
        Nullability(proofs.Apply(null)).ShouldBe([Server, Server, Server, Server, Server]);
    }

    // A member that can give NULL in a place leaves that column nullable, and every column whose proof reads it.
    [Theory]
    [InlineData("c.name", new[] { true, false, false, false, true })]
    [InlineData("c.id", new[] { false, false, true, true, true })]
    public void Apply_MemberGivesNull_ColumnsThatReadItStayNullable(string nullable, bool[] proved)
    {
        var proofs = Read(Tree, Rows("id", "parent_id", "name", "path", "depth"));

        Nullability(proofs.Apply(Facts(proofs, "c.parent_id", nullable)))
            .ShouldBe([.. proved.Select(value => value ? Proved : Server)]);
    }

    // Each column holds only by the other's induction, and neither by the server's answer.
    [Fact]
    public void Apply_ColumnsThatReadEachOther_ProvedTogether()
    {
        var proofs = Read(
            "WITH t (a, b) AS (SELECT 1, 2 UNION ALL SELECT b, a FROM t WHERE a < 9) SELECT a, b FROM t",
            Rows("a", "b")
        );

        Nullability(proofs.Apply(Facts(proofs))).ShouldBe([Proved, Proved]);
    }

    // b is NULL in the anchor, so it is not in the set, and a, which the recursive member reads from b, goes too.
    [Fact]
    public void Apply_ColumnThatReadsANullOne_StaysNullable()
    {
        var proofs = Read(
            "WITH t (a, b) AS (SELECT 1, NULL UNION ALL SELECT b, a FROM t WHERE a < 9) SELECT a, b FROM t",
            Rows("a", "b")
        );

        Nullability(proofs.Apply(Facts(proofs))).ShouldBe([Server, Server]);
    }

    [Theory]
    // A CTE of several branches that is not recursive.
    [InlineData("WITH q AS (SELECT id AS n FROM dbo.users UNION ALL SELECT COUNT(*) FROM dbo.orders) SELECT n FROM q")]
    [InlineData("WITH q (n) AS (SELECT id FROM dbo.users UNION SELECT 1 EXCEPT SELECT 2) SELECT q.n FROM q")]
    // Inside a form, through a join no outer join makes NULL, and in an aggregate.
    [InlineData(
        "WITH t (n) AS (SELECT 1 UNION ALL SELECT n + 1 FROM t WHERE n < 5) SELECT CAST(n AS bigint) AS n FROM t"
    )]
    [InlineData(
        "WITH t (n) AS (SELECT 1 UNION ALL SELECT n + 1 FROM t WHERE n < 5) "
            + "SELECT t.n FROM dbo.users AS u JOIN t ON t.n = u.id LEFT JOIN dbo.orders AS o ON o.user_id = u.id"
    )]
    [InlineData(
        "WITH t (n) AS (SELECT 1 UNION ALL SELECT n + 1 FROM t WHERE n < 5) SELECT MAX(n) AS n FROM t GROUP BY n"
    )]
    // A recursive CTE read by a later CTE of several branches, and the members of several anchors and recursions.
    [InlineData(
        "WITH t (n) AS (SELECT 1 UNION ALL SELECT n + 1 FROM t WHERE n < 5), "
            + "u (n) AS (SELECT n FROM t UNION ALL SELECT n * 2 FROM t) SELECT n FROM u"
    )]
    [InlineData(
        "WITH t (n) AS (SELECT 1 UNION ALL SELECT 2 UNION ALL SELECT n + 1 FROM t WHERE n < 5 "
            + "UNION ALL SELECT n + 2 FROM t WHERE n < 5) SELECT n FROM t"
    )]
    // A set operator whose every branch reads the CTE.
    [InlineData(
        "WITH t (n) AS (SELECT 1 UNION ALL SELECT n + 1 FROM t WHERE n < 5) "
            + "SELECT n FROM t UNION ALL SELECT n * 3 FROM t WHERE n > 2"
    )]
    public void Apply_EveryBranchProves_Proved(string sql)
    {
        var proofs = Read(sql, Rows("n"));

        Nullability(proofs.Apply(Facts(proofs))).ShouldBe([Proved]);
        Nullability(proofs.Apply(null)).ShouldBe([Server]);
    }

    // Each of these must stay nullable whatever the server answers of the operands it asks, every one not null but the
    // CTE's own columns.
    [Theory]
    [InlineData("WITH q AS (SELECT id AS n FROM dbo.users UNION ALL SELECT NULL) SELECT n FROM q")]
    [InlineData(
        "WITH t (n) AS (SELECT 1 UNION ALL SELECT n + 1 FROM t WHERE n < 5 UNION ALL SELECT NULL) SELECT n FROM t"
    )]
    [InlineData("WITH t (n) AS (SELECT 1 UNION ALL SELECT @p FROM t WHERE n < 5) SELECT n FROM t")]
    // The CTE read through an outer join, a name another source may hold, or a name in another case.
    [InlineData(
        "WITH t (n) AS (SELECT 1 UNION ALL SELECT n + 1 FROM t WHERE n < 5) "
            + "SELECT t.n FROM dbo.users AS u LEFT JOIN t ON t.n = u.id"
    )]
    [InlineData(
        "WITH t (n) AS (SELECT 1 UNION ALL SELECT n + 1 FROM t WHERE n < 5) "
            + "SELECT t.n FROM t RIGHT JOIN dbo.users AS u ON t.n = u.id"
    )]
    // A LEFT JOIN that the WHERE makes inner needs the server to name a stored column of the CTE, which it does not.
    [InlineData(
        "WITH t (n) AS (SELECT 1 UNION ALL SELECT n + 1 FROM t WHERE n < 5) "
            + "SELECT t.n FROM dbo.users AS u LEFT JOIN t ON t.n = u.id WHERE t.n > 0"
    )]
    [InlineData(
        "WITH t (n) AS (SELECT 1 UNION ALL SELECT n + 1 FROM t WHERE n < 5) "
            + "SELECT n FROM t JOIN dbo.users AS u ON t.n = u.id"
    )]
    [InlineData(
        "WITH t (n) AS (SELECT 1 UNION ALL SELECT n + 1 FROM t WHERE n < 5) SELECT T.n AS n FROM t AS T2, t AS T"
    )]
    [InlineData("WITH t (n) AS (SELECT 1 UNION ALL SELECT n + 1 FROM t WHERE n < 5) SELECT N FROM t")]
    [InlineData("WITH t (n) AS (SELECT 1 UNION ALL SELECT n + 1 FROM t WHERE n < 5) SELECT n FROM T")]
    [InlineData(
        "WITH t (n) AS (SELECT 1 UNION ALL SELECT n + 1 FROM t WHERE n < 5) SELECT x.n FROM (SELECT n FROM t) AS x"
    )]
    [InlineData("WITH t (n) AS (SELECT 1 UNION ALL SELECT n + 1 FROM t WHERE n < 5) SELECT n FROM dbo.t")]
    // A recursive member that reads its CTE through an outer join, a comma, an APPLY or a derived table.
    [InlineData(
        "WITH t (n) AS (SELECT 1 UNION ALL SELECT t.n + 1 FROM dbo.users AS u LEFT JOIN t ON t.n = u.id) "
            + "SELECT n FROM t"
    )]
    [InlineData(
        "WITH t (n) AS (SELECT 1 UNION ALL SELECT t.n + 1 FROM dbo.users AS u, t WHERE t.n = u.id) SELECT n FROM t"
    )]
    [InlineData(
        "WITH t (n) AS (SELECT 1 UNION ALL SELECT t.n + 1 FROM t CROSS APPLY (SELECT 1 AS one) AS o) SELECT n FROM t"
    )]
    [InlineData("WITH t (n) AS (SELECT 1 UNION ALL SELECT x.n + 1 FROM (SELECT n FROM t) AS x) SELECT n FROM t")]
    // Columns the reader cannot place.
    [InlineData("WITH t (n, m) AS (SELECT 1 UNION ALL SELECT n + 1 FROM t WHERE n < 5) SELECT n FROM t")]
    [InlineData("WITH t AS (SELECT 1 AS n, 2 AS n UNION ALL SELECT n + 1, 3 FROM t WHERE n < 5) SELECT n FROM t")]
    [InlineData("WITH t AS (SELECT * FROM dbo.users UNION ALL SELECT * FROM dbo.users) SELECT n FROM t")]
    [InlineData("WITH t AS (SELECT 1 AS n UNION ALL SELECT n + 1, 2 FROM t WHERE n < 5) SELECT n FROM t")]
    [InlineData("WITH t AS (SELECT 1 UNION ALL SELECT n + 1 FROM t WHERE n < 5) SELECT n FROM t")]
    // A grouping set's total, and GROUP BY ALL, in a branch or in the query.
    [InlineData("WITH q AS (SELECT id AS n FROM dbo.users GROUP BY ROLLUP (id) UNION ALL SELECT 1) SELECT n FROM q")]
    [InlineData("WITH q AS (SELECT id AS n FROM dbo.users GROUP BY ALL id UNION ALL SELECT 1) SELECT n FROM q")]
    [InlineData(
        "WITH t (n) AS (SELECT 1 UNION ALL SELECT n + 1 FROM t WHERE n < 5) SELECT n FROM t GROUP BY ROLLUP (n)"
    )]
    // An earlier CTE of several branches that lets a NULL through, read by a later one.
    [InlineData(
        "WITH t (n) AS (SELECT 1 UNION ALL SELECT NULL), u (n) AS (SELECT n FROM t UNION ALL SELECT 2) SELECT n FROM u"
    )]
    // A CTE that a later one names again hides it, and inside a CTE a later one is a table.
    [InlineData(
        "WITH t (n) AS (SELECT 1 UNION ALL SELECT n + 1 FROM t WHERE n < 5), u (n) AS (SELECT n FROM v "
            + "UNION ALL SELECT 1), v (n) AS (SELECT 1 UNION ALL SELECT 2) SELECT n FROM u"
    )]
    public void Apply_ABranchCanBeNull_StaysNullable(string sql)
    {
        var proofs = Read(sql, Rows("n"));

        Nullability(proofs.Apply(Facts(proofs))).ShouldBe([Server]);
    }

    // A CTE's branches convert to its column's type, and a string converts to a CLR type's Null.
    [Fact]
    public void Apply_CteColumnOfAClrType_StaysNullable()
    {
        var proofs = Read("WITH q AS (SELECT shape AS n FROM dbo.s UNION ALL SELECT 'x') SELECT q.n FROM q", Rows("n"));

        Nullability(proofs.Apply(Facts(proofs))).ShouldBe([Proved]);
        Nullability(
                proofs.Apply([
                    .. OperandTexts(proofs).Select(text => new OperandFacts(text != "q.n", NotClr: text != "q.n")),
                ])
            )
            .ShouldBe([Server]);
    }

    // A CTE that no proof reads asks nothing.
    [Fact]
    public void Read_CteNotRead_AsksNothingOfIt()
    {
        var proofs = Read(
            "WITH t (n) AS (SELECT 1 UNION ALL SELECT n + 1 FROM t WHERE n < 5) SELECT COUNT(*) AS n FROM t",
            Rows("n")
        );

        proofs.OperandQueries.ShouldBeEmpty();
        Nullability(proofs.Apply(null)).ShouldBe([Proved]);
    }

    private static SqlServerColumnRow[] Rows(params string[] names) =>
        [.. names.Select((name, index) => ExpressionRow(index + 1, name))];

    // The answers of the calls, each operand of a type that is no CLR type and not null, but those named and those
    // of a CTE's own columns, which the server answers nullable.
    private static IReadOnlyList<OperandFacts> Facts(ColumnProofs proofs, params string[] nullable) =>
        [
            .. OperandTexts(proofs)
                .Select(text => new OperandFacts(!nullable.Contains(text) && !OwnColumn().IsMatch(text), NotClr: true)),
        ];

    // The text of each operand the calls ask, in the order of the answers.
    private static IEnumerable<string> OperandTexts(ColumnProofs proofs) =>
        proofs.OperandQueries.SelectMany(static query =>
        {
            var start = query.Sql.LastIndexOf(
                "SELECT ",
                query.Sql.IndexOf("\nAS [1]", StringComparison.Ordinal),
                StringComparison.Ordinal
            );
            return Operand()
                .Matches(query.Sql[(start + "SELECT ".Length)..])
                .Select(static match => match.Groups[1].Value);
        });

    // An operand that is a column of the CTEs of these tests, or of a table that reads one, which the server answers
    // nullable.
    [GeneratedRegex(@"^(t|q|u|x)\.|^[nab]$")]
    private static partial Regex OwnColumn();

    // An operand of a call's text and the alias after it.
    [GeneratedRegex(@"(?s)(.*?)\nAS \[\d+\](?:,\n)?")]
    private static partial Regex Operand();
}
