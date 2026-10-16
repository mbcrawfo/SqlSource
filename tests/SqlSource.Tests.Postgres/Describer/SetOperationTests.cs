using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using Npgsql;
using Shouldly;
using SqlSource.Snapshot;
using Xunit;

namespace SqlSource.Tests.Postgres.Describer;

// What part C of the walk answers against a real server: a column of a set operation, from the branches the operation
// returns rows of, and the shapes that keep it unknown.  The set operations are pinned for PostgreSQL 14 and 18 alone:
// 14 plans INTERSECT and EXCEPT as a SetOp over an Append of flagged members, 18 as a SetOp with two inputs.  Where a
// column stays unknown because the server can return NULL in it, the test runs the query over rows that make it, in a
// transaction it rolls back: the fixture's tables are counted by other tests.
[Collection(UsesPostgres.Name)]
public sealed class SetOperationTests(PostgresContainer postgres)
{
    // A partitioned table of these tests alone, made by SQL that can be run again, and left: the container is thrown
    // away.  Its note is the one column that may be NULL.
    private const string Partitioned = """
        CREATE TABLE IF NOT EXISTS setop_parts (id integer NOT NULL, kind integer NOT NULL, note text)
            PARTITION BY LIST (kind);
        CREATE TABLE IF NOT EXISTS setop_parts_1 PARTITION OF setop_parts FOR VALUES IN (1);
        CREATE TABLE IF NOT EXISTS setop_parts_2 PARTITION OF setop_parts FOR VALUES IN (2);
        """;

    // Two users, one of whom has an order: x has no email and no deleted_at, its order has no note, and y has no
    // order, so a LEFT JOIN of users to orders extends y with NULL.  The name x is the one that NULLIF(name, 'x')
    // makes NULL.
    private const string Rows = """
        INSERT INTO users (name) VALUES ('x'), ('y');
        INSERT INTO orders (user_id, total) SELECT id, 1 FROM users WHERE name = 'x';
        """;

    private static readonly (bool?, string?) Proven = (false, "set-operation");

    private static readonly (bool?, string?) Unknown = (null, "no-origin");

    private async Task<(bool? Nullable, string? Source)[]> WalkedAsync(string sql)
    {
        if (postgres.Major is not (14 or 18))
        {
            Assert.Skip("The set operations are pinned for PostgreSQL 14 and 18 alone.");
        }

        var described = await Sessions.DescribeAsync(postgres.ConnectionString, Requests.Of(sql));

        described.Failure.ShouldBeNull(described.Failure?.Descriptor.Id);
        var description = described.Value.ShouldNotBeNull();
        description.Plan.ShouldBe(SidecarValues.Plan.Walked);
        return
        [
            .. description.Columns.ShouldNotBeNull().Select(static column => (column.Nullable, column.NullableSource)),
        ];
    }

    private async Task RunAsync(string sql)
    {
        await using var source = NpgsqlDataSource.Create(postgres.ConnectionString);
        await using var command = source.CreateCommand(sql);
        _ = await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    // Whether the query returns a NULL in the column over the rows of the setup, which are rolled back.
    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "The SQL is the test's own constants."
    )]
    private async Task<bool> ReturnsNullAsync(string setup, string sql, int column)
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = NpgsqlDataSource.Create(postgres.ConnectionString);
        await using var connection = await source.OpenConnectionAsync(token);
        await using var transaction = await connection.BeginTransactionAsync(token);
        await using (var insert = new NpgsqlCommand(setup, connection, transaction))
        {
            _ = await insert.ExecuteNonQueryAsync(token);
        }

        var found = false;
        await using (var select = new NpgsqlCommand(sql, connection, transaction))
        await using (var reader = await select.ExecuteReaderAsync(token))
        {
            while (await reader.ReadAsync(token))
            {
                found |= await reader.IsDBNullAsync(column, token);
            }
        }

        await transaction.RollbackAsync(token);
        return found;
    }

    // UNION ALL and UNION return every row of every branch: a NOT NULL column in each is NOT NULL in the result.
    [Theory]
    [InlineData(
        "SELECT u.id, u.name FROM users u UNION ALL SELECT x.id, x.name FROM users x WHERE x.status = 'active'"
    )]
    [InlineData("SELECT u.id, u.name FROM users u UNION SELECT x.id, x.name FROM users x WHERE x.status = 'active'")]
    [InlineData(
        "SELECT u.id, u.name FROM users u UNION ALL SELECT x.id, x.name FROM users x "
            + "UNION ALL SELECT y.id, y.name FROM users y"
    )]
    public async Task Describe_UnionOfNotNullColumns_IsAnswered(string sql) =>
        (await WalkedAsync(sql)).ShouldBe([Proven, Proven]);

    // The planner casts the int4 branch to the result's int8 and the plan does not print the cast; under UNION and
    // UNION ALL the cast branch is a scan the walk does not follow, on either major.
    [Theory]
    [InlineData("SELECT u.id FROM users u UNION ALL SELECT o.id FROM orders o")]
    [InlineData("SELECT o.id FROM orders o UNION ALL SELECT u.id FROM users u")]
    [InlineData("SELECT u.id FROM users u UNION SELECT o.id FROM orders o")]
    public async Task Describe_UnionOfInt4AndInt8_StaysUnknown(string sql) =>
        (await WalkedAsync(sql)).ShouldBe([Unknown]);

    // Under INTERSECT and EXCEPT 14's flagged member is the planner's own leaf scan, followed by place; 18 prints the
    // cast branch's scan as an input like any other, and it stays unknown.  The INTERSECT's other input, an int8
    // that can be NULL, proves nothing.
    [Theory]
    [InlineData("SELECT u.id FROM users u INTERSECT SELECT s.big FROM samples s")]
    [InlineData("SELECT u.id FROM users u EXCEPT SELECT o.id FROM orders o")]
    public async Task Describe_IntersectAndExceptOfACastBranch_AreAnsweredOn14AndUnknownOn18(string sql) =>
        (await WalkedAsync(sql)).ShouldBe([postgres.Major == 14 ? Proven : Unknown]);

    // An INTERSECT is proven by either input: the int8 side needs no cast.
    [Fact]
    public async Task Describe_IntersectWhoseInt8InputIsNotNull_IsAnswered() =>
        (await WalkedAsync("SELECT u.id FROM users u INTERSECT SELECT o.id FROM orders o")).ShouldBe([Proven]);

    // A branch's NULL, a nullable column of a branch, and the extended side of a LEFT JOIN in a branch: the column
    // may be NULL, and is.
    [Theory]
    [InlineData("SELECT u.id FROM users u UNION ALL SELECT NULL::integer", 0)]
    [InlineData("SELECT NULL::integer UNION ALL SELECT u.id FROM users u", 0)]
    [InlineData("SELECT u.id FROM users u UNION SELECT NULL::integer", 0)]
    [InlineData("SELECT u.created_at FROM users u UNION ALL SELECT x.deleted_at FROM users x", 0)]
    [InlineData("SELECT x.deleted_at FROM users x UNION ALL SELECT u.created_at FROM users u", 0)]
    public async Task Describe_UnionWithABranchThatMayBeNull_StaysUnknown(string sql, int column)
    {
        (await WalkedAsync(sql)).ShouldBe([Unknown]);
        (await ReturnsNullAsync(Rows, sql, column)).ShouldBeTrue();
    }

    // The preserved side of a LEFT JOIN in a branch is answered by its column, the extended side is not.
    [Fact]
    public async Task Describe_UnionOfALeftJoinBranch_AnswersThePreservedColumnAlone()
    {
        var sql =
            "SELECT u.id, o.id FROM users u LEFT JOIN orders o ON o.user_id = u.id "
            + "UNION ALL SELECT i.id, i.order_id FROM billing.invoices i";

        (await WalkedAsync(sql)).ShouldBe([Proven, Unknown]);
        (await ReturnsNullAsync(Rows, sql, 1)).ShouldBeTrue();
    }

    // INTERSECT returns the rows that both inputs hold: one input that proves the column is enough, whichever it is.
    [Theory]
    [InlineData("SELECT u.deleted_at FROM users u INTERSECT SELECT x.created_at FROM users x")]
    [InlineData("SELECT u.created_at FROM users u INTERSECT SELECT x.deleted_at FROM users x")]
    [InlineData("SELECT u.deleted_at FROM users u INTERSECT ALL SELECT x.created_at FROM users x")]
    public async Task Describe_IntersectWithOneNotNullInput_IsAnswered(string sql) =>
        (await WalkedAsync(sql)).ShouldBe([Proven]);

    // Both inputs may be NULL, and NULL equals NULL in an INTERSECT: the row is returned.
    [Fact]
    public async Task Describe_IntersectOfTwoNullableColumns_StaysUnknown()
    {
        var sql = "SELECT u.deleted_at FROM users u INTERSECT SELECT x.deleted_at FROM users x";

        (await WalkedAsync(sql)).ShouldBe([Unknown]);
        (await ReturnsNullAsync(Rows, sql, 0)).ShouldBeTrue();
    }

    // EXCEPT returns rows of its left input: only the left proves the column.
    [Theory]
    [InlineData("SELECT u.created_at FROM users u EXCEPT SELECT x.deleted_at FROM users x")]
    [InlineData("SELECT u.created_at FROM users u EXCEPT ALL SELECT x.deleted_at FROM users x")]
    public async Task Describe_ExceptWithANotNullLeft_IsAnswered(string sql) =>
        (await WalkedAsync(sql)).ShouldBe([Proven]);

    [Theory]
    [InlineData("SELECT u.deleted_at FROM users u EXCEPT SELECT x.created_at FROM users x")]
    [InlineData("SELECT u.deleted_at FROM users u EXCEPT ALL SELECT x.created_at FROM users x")]
    public async Task Describe_ExceptWithANullableLeft_StaysUnknown(string sql)
    {
        (await WalkedAsync(sql)).ShouldBe([Unknown]);
        (await ReturnsNullAsync(Rows, sql, 0)).ShouldBeTrue();
    }

    // a EXCEPT b EXCEPT c is (a EXCEPT b) EXCEPT c, whose left input is a set operation of its own: 14 plans it as a
    // Result member over the inner SetOp.  The row description's type is the outer operation's, and the inner one is
    // of the common type of its own inputs, which may be the type of an input that proves nothing: below another set
    // operation each input must prove the column, and the leftmost branch alone does not.  That is unknown where the
    // server cannot return NULL, never the other way.
    [Theory]
    [InlineData(
        "SELECT u.created_at FROM users u EXCEPT SELECT x.created_at FROM users x"
            + " EXCEPT SELECT y.created_at FROM users y",
        true
    )]
    [InlineData(
        "SELECT u.created_at FROM users u EXCEPT SELECT x.deleted_at FROM users x"
            + " EXCEPT SELECT y.deleted_at FROM users y",
        false
    )]
    [InlineData(
        "SELECT u.deleted_at FROM users u EXCEPT SELECT x.created_at FROM users x"
            + " EXCEPT SELECT y.created_at FROM users y",
        false
    )]
    [InlineData(
        "SELECT u.created_at FROM users u EXCEPT (SELECT x.deleted_at FROM users x"
            + " EXCEPT SELECT y.deleted_at FROM users y)",
        true
    )]
    [InlineData(
        "SELECT u.deleted_at FROM users u EXCEPT (SELECT x.created_at FROM users x"
            + " INTERSECT SELECT y.created_at FROM users y)",
        false
    )]
    [InlineData(
        "(SELECT u.created_at FROM users u INTERSECT SELECT x.created_at FROM users x)"
            + " EXCEPT SELECT y.deleted_at FROM users y",
        true
    )]
    [InlineData(
        "(SELECT u.created_at FROM users u INTERSECT SELECT x.deleted_at FROM users x)"
            + " EXCEPT SELECT y.deleted_at FROM users y",
        false
    )]
    public async Task Describe_NestedSetOperations_AreAnsweredByWhatEachLevelProves(string sql, bool proven) =>
        (await WalkedAsync(sql)).ShouldBe([proven ? Proven : Unknown]);

    // The nested shapes whose column the server does return NULL in.
    [Theory]
    [InlineData(
        "SELECT u.deleted_at FROM users u EXCEPT SELECT x.created_at FROM users x"
            + " EXCEPT SELECT y.created_at FROM users y"
    )]
    [InlineData(
        "SELECT u.deleted_at FROM users u EXCEPT (SELECT x.created_at FROM users x"
            + " INTERSECT SELECT y.created_at FROM users y)"
    )]
    public async Task Describe_NestedSetOperationWithANullableLeftmostBranch_ReturnsNull(string sql) =>
        (await ReturnsNullAsync(Rows, sql, 0)).ShouldBeTrue();

    // A partitioned table in a branch is an Append of its partitions nested as a member: each must prove the column.
    [Fact]
    public async Task Describe_UnionAllOfAPartitionedTable_IsAnsweredByItsColumns()
    {
        await RunAsync(Partitioned);
        var sql = "SELECT p.id, p.note FROM setop_parts p UNION ALL SELECT o.user_id, o.note FROM orders o";

        (await WalkedAsync(sql)).ShouldBe([Proven, Unknown]);
        (
            await ReturnsNullAsync(Rows + "INSERT INTO setop_parts (id, kind) VALUES (1, 1), (2, 2);", sql, 1)
        ).ShouldBeTrue();
    }

    // Names that cross do not cross the answer: a branch is read by place, not by the names its scan prints.  The
    // branch's two columns share a name, and the left input's second is the nullable one.
    [Theory]
    [InlineData(
        "SELECT u.created_at AS note, u.deleted_at AS note FROM users u "
            + "EXCEPT SELECT x.created_at, x.created_at FROM users x"
    )]
    [InlineData(
        "SELECT u.created_at AS note, u.deleted_at AS note FROM users u "
            + "INTERSECT SELECT x.created_at, x.deleted_at FROM users x"
    )]
    public async Task Describe_BranchWithTwoColumnsOfOneName_IsReadByPlace(string sql)
    {
        (await WalkedAsync(sql)).ShouldBe([Proven, Unknown]);
        (await ReturnsNullAsync(Rows, sql, 1)).ShouldBeTrue();
    }

    // A subquery in FROM that the result reads in another order than its columns: the root is the subquery's scan, a
    // user's alias that the walk does not follow, so nothing is answered, and no column is read as another.  The
    // first column is created_at, which the server never returns NULL in, and the second is deleted_at, which it does.
    [Theory]
    [InlineData(
        "SELECT s.b, s.a FROM (SELECT u.deleted_at AS a, u.created_at AS b FROM users u "
            + "EXCEPT SELECT x.created_at, x.deleted_at FROM users x) s"
    )]
    [InlineData(
        "SELECT s.b, s.a FROM (SELECT u.deleted_at AS a, u.created_at AS b FROM users u "
            + "UNION ALL SELECT x.created_at, x.deleted_at FROM users x) s"
    )]
    public async Task Describe_SetOperationInASubqueryReadInAnotherOrder_StaysUnknown(string sql)
    {
        (await WalkedAsync(sql)).ShouldBe([Unknown, Unknown]);
        (await ReturnsNullAsync(Rows, sql, 1)).ShouldBeTrue();
    }

    // A user who writes the planner's alias in a branch gets a scan that prints its columns in the order its query
    // needs, not the branch's: the text makes the walk follow no alias by place.  Column 0 is deleted_at here.
    [Fact]
    public async Task Describe_BranchThatWritesTheLeafScansAlias_StaysUnknown()
    {
        var sql =
            "SELECT \"*SELECT* 1\".b, \"*SELECT* 1\".a FROM "
            + "(SELECT u.created_at AS a, u.deleted_at AS b FROM users u OFFSET 0) AS \"*SELECT* 1\" "
            + "EXCEPT SELECT x.created_at, x.created_at FROM users x";

        (await WalkedAsync(sql)).ShouldBe([Unknown, Unknown]);
        (await ReturnsNullAsync(Rows, sql, 0)).ShouldBeTrue();
    }

    // The same through a view, where the SQL of the query holds no *SELECT* at all: b is NULLIF(name, 'x') and a the
    // name, in the opposite order of the view's subquery, and the row of the user named x has a NULL b.
    [Fact]
    public async Task Describe_ViewWhoseSubqueryIsNamedLikeALeafScan_StaysUnknown()
    {
        await RunAsync(
            "CREATE OR REPLACE VIEW setop_leaf_alias AS SELECT \"*SELECT* 9\".b, \"*SELECT* 9\".a FROM "
                + "(SELECT u.name AS a, NULLIF(u.name, 'x') AS b FROM users u OFFSET 0) AS \"*SELECT* 9\""
        );
        var sql = "SELECT b, a FROM setop_leaf_alias EXCEPT SELECT 'zz', 'zz'";

        (await WalkedAsync(sql)).ShouldBe([Unknown, Unknown]);
        (await ReturnsNullAsync(Rows, sql, 0)).ShouldBeTrue();
    }

    // GROUP BY over a UNION ALL: the aggregate is computed above the set operation and stays unknown, and the key is
    // the Aggregate's entry that is the Append's printed one at its place, so it is answered like the union's column.
    [Fact]
    public async Task Describe_GroupByOverAUnionAll_AnswersTheKeyAndNotTheAggregate()
    {
        (
            await WalkedAsync(
                "SELECT s.k, count(*) AS n FROM (SELECT u.created_at AS k FROM users u "
                    + "UNION ALL SELECT x.created_at FROM users x) s GROUP BY s.k"
            )
        ).ShouldBe([Proven, Unknown]);
        (
            await WalkedAsync(
                "SELECT s.k, count(*) AS n FROM (SELECT u.deleted_at AS k FROM users u "
                    + "UNION ALL SELECT x.created_at FROM users x) s GROUP BY s.k"
            )
        ).ShouldBe([Unknown, Unknown]);
    }

    // A recursive CTE is a Recursive Union read through a CTE Scan: it proves nothing.
    [Fact]
    public async Task Describe_RecursiveCte_StaysUnknown() =>
        (
            await WalkedAsync(
                "WITH RECURSIVE r (n) AS (SELECT 1 UNION ALL SELECT r.n + 1 FROM r WHERE r.n < 5) SELECT r.n FROM r"
            )
        ).ShouldBe([Unknown]);

    // A branch with a LIMIT is a scan of its own that prints the columns the query above needs, in its order: read
    // in another order than the union's, it prints "*SELECT* 2"."nullif" first, and "*SELECT* 2" under an Append is
    // no leaf scan the walk follows by place.  Neither column is answered; the first is the one that is NULL.
    [Fact]
    public async Task Describe_UnionAllWithALimitBranchReadInAnotherOrder_StaysUnknown()
    {
        var sql =
            "SELECT s.b, s.a FROM (SELECT u.name AS a, u.name AS b FROM users u "
            + "UNION ALL (SELECT x.name, NULLIF(x.name, 'x') FROM users x LIMIT 5)) s";

        (await WalkedAsync(sql)).ShouldBe([Unknown, Unknown]);
        (await ReturnsNullAsync(Rows, sql, 0)).ShouldBeTrue();
    }

    // The same through an inlined SQL function: the planner pulls its union up into the query.
    [Fact]
    public async Task Describe_UnionAllOfAnInlinedFunctionWithALimitBranch_StaysUnknown()
    {
        await RunAsync(
            "CREATE OR REPLACE FUNCTION setop_pairs() RETURNS TABLE (a varchar, b varchar) LANGUAGE sql STABLE AS "
                + "$$ SELECT u.name, u.name FROM users u "
                + "UNION ALL (SELECT x.name, NULLIF(x.name, 'x') FROM users x LIMIT 5) $$"
        );
        var sql = "SELECT b, a FROM setop_pairs()";

        (await WalkedAsync(sql)).ShouldBe([Unknown, Unknown]);
        (await ReturnsNullAsync(Rows, sql, 0)).ShouldBeTrue();
    }

    // A Merge Append is a union of its members as an Append is: an ORDER BY with a LIMIT over a UNION ALL of two
    // primary keys reads both through their indexes.
    [Fact]
    public async Task Describe_MergeAppendOfTwoIndexScans_IsAnswered() =>
        (
            await WalkedAsync(
                "SELECT s.id FROM (SELECT u.id FROM users u UNION ALL SELECT i.id FROM billing.invoices i) s "
                    + "ORDER BY s.id LIMIT 5"
            )
        ).ShouldBe([Proven]);
}
