using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Npgsql;
using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Describing.Postgres;
using Xunit;

namespace SqlSource.Tests.Postgres.Describer;

// The nullability walk against a real server: what only a planner can show, and what a describe leaves behind.
// The matrix of join shapes is the fixture cases named walk-*, which FixtureCasesTests describes here.
[Collection(UsesPostgres.Name)]
public sealed class WalkTests(PostgresContainer postgres)
{
    // A partitioned table of these tests alone, made by SQL that can be run again, and left: the container is
    // thrown away, and the fixtures are recorded in one of their own.
    private const string Partitioned = """
        CREATE TABLE IF NOT EXISTS walk_parts (id integer NOT NULL, kind integer NOT NULL) PARTITION BY LIST (kind);
        CREATE TABLE IF NOT EXISTS walk_parts_1 PARTITION OF walk_parts FOR VALUES IN (1);
        CREATE TABLE IF NOT EXISTS walk_parts_2 PARTITION OF walk_parts FOR VALUES IN (2);
        """;

    private async Task<QueryDescription> ShouldDescribeAsync(string sql)
    {
        var described = await Sessions.DescribeAsync(postgres.ConnectionString, Requests.Of(sql));

        described.Failure.ShouldBeNull(described.Failure?.Descriptor.Id);
        return described.Value.ShouldNotBeNull();
    }

    private async Task<(bool? Nullable, string? Source)[]> WalkedAsync(string sql)
    {
        var description = await ShouldDescribeAsync(sql);

        description.Plan.ShouldBe(SidecarValues.Plan.Walked);
        return
        [
            .. description.Columns.ShouldNotBeNull().Select(static column => (column.Nullable, column.NullableSource)),
        ];
    }

    // One value from the server, on a connection of the test's own.
    private async Task<object?> ScalarAsync(string sql)
    {
        await using var source = NpgsqlDataSource.Create(postgres.ConnectionString);
        await using var command = source.CreateCommand(sql);
        return await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);
    }

    private async Task<int> ServerMajorAsync() =>
        int.Parse((string)(await ScalarAsync("SHOW server_version_num"))!, CultureInfo.InvariantCulture) / 10000;

    // The query of the spec's Goal, and its table.
    [Fact]
    public async Task Describe_LeftJoin_KeepsTheOuterSideNotNullAndExtendsTheInnerOne() =>
        (
            await WalkedAsync(
                "SELECT u.id, u.name, o.id AS order_id, o.total "
                    + "FROM users u LEFT JOIN orders o ON o.user_id = u.id WHERE u.id = @id"
            )
        ).ShouldBe([(false, "catalog"), (false, "catalog"), (true, "outer-join"), (true, "outer-join")]);

    // With a condition on the nullable side the planner makes the join inner, which the text does not show.
    [Fact]
    public async Task Describe_LeftJoinThatAWhereMakesInner_IsNotNullThroughout() =>
        (
            await WalkedAsync(
                "SELECT u.id, u.name, o.id AS order_id, o.total "
                    + "FROM users u LEFT JOIN orders o ON o.user_id = u.id WHERE u.id = @id AND o.total > @min"
            )
        ).ShouldBe([(false, "catalog"), (false, "catalog"), (false, "catalog"), (false, "catalog")]);

    // A $n that the SQL writes itself is a parameter of the prepared statement and of no list: EXPLAIN EXECUTE is
    // refused with fewer arguments than the statement has, so the plan is asked with the server's count.  The $n is
    // in the join's condition, since in the WHERE it would make the join inner.
    [Fact]
    public async Task Describe_QueryWithADollarParameterOfItsOwn_IsWalked() =>
        (
            await WalkedAsync(
                "SELECT u.id, o.total FROM users u LEFT JOIN orders o ON o.user_id = u.id AND o.id = $2 "
                    + "WHERE u.id = @id"
            )
        ).ShouldBe([(false, "catalog"), (true, "outer-join")]);

    // The tool leaves the database as it found it: no statement, and the settings as "open" read them.
    [Fact]
    public async Task Describe_GatedQuery_LeavesNoStatementAndThePlanCacheModeAsOpenReadIt()
    {
        var token = TestContext.Current.CancellationToken;
        var opened = await Sessions.OpenAsync(postgres.ConnectionString);
        await using var session = opened.Value.ShouldBeOfType<PostgresSession>();
        var read = session.Server.Settings.Single(static setting => setting.Name == "plan_cache_mode").Value;

        var described = await session.DescribeAsync(
            Requests.Of("SELECT u.id, o.total FROM users u LEFT JOIN orders o ON o.user_id = u.id"),
            token
        );

        described.Value.ShouldNotBeNull().Plan.ShouldBe(SidecarValues.Plan.Walked);
        (await Sessions.PreparedStatementsAsync(session)).ShouldBe(0);
        await using var show = await session.Connection.CommandAsync("SHOW plan_cache_mode", token);
        (await show.ExecuteScalarAsync(token)).ShouldBe(read);
    }

    // The ModifyTable sits over a scan of its own target with the same alias: one instance, not a doubled alias.
    [Fact]
    public async Task Describe_UpdateFromReturning_GivesTheTargetTheCatalogsAnswer() =>
        (
            await WalkedAsync(
                "UPDATE users u SET name = @name FROM orders o LEFT JOIN billing.invoices i ON i.order_id = o.id "
                    + "WHERE o.user_id = u.id RETURNING u.id, o.total, i.amount"
            )
        ).ShouldBe([(false, "catalog"), (false, "catalog"), (true, "outer-join")]);

    // A DELETE of one table prints bare names, which are its target's.  The function's name is what gates it.
    [Fact]
    public async Task Describe_DeleteReturningBareNames_GivesTheTargetTheCatalogsAnswer() =>
        (await WalkedAsync("DELETE FROM orders WHERE left(note, 1) = @initial RETURNING id, note")).ShouldBe([
            (false, "catalog"),
            (true, "catalog"),
        ]);

    // The gate lets the query in for the word, and here the word is a table's alias.  The plan prints a table
    // aliased old as it prints PostgreSQL 18's old row, so in a statement that names the word any alias but the
    // target's keeps the heuristic, on every version.
    [Fact]
    public async Task Describe_TableAliasedOld_KeepsTheHeuristicForItsColumns() =>
        (
            await WalkedAsync(
                "UPDATE users u SET name = @name FROM orders old WHERE old.user_id = u.id RETURNING old.total, u.id"
            )
        ).ShouldBe([(true, "heuristic"), (false, "catalog")]);

    // An alias of an inner scope reaches the plan under its own name: the SELECT of an INSERT ... SELECT aliases
    // users old, and the old row of the RETURNING list prints as old.id too, NULL on every row an INSERT returns.
    [Fact]
    public async Task Describe_InsertSelectFromATableAliasedOld_KeepsTheHeuristicForOld()
    {
        Assert.SkipWhen(await ServerMajorAsync() < 18, "PostgreSQL before 18 has no old and new in RETURNING.");

        (
            await WalkedAsync(
                "INSERT INTO orders (user_id, total) SELECT old.id, 0 FROM users old RETURNING old.id, new.id"
            )
        ).ShouldBe([(true, "heuristic"), (true, "heuristic")]);
    }

    // PostgreSQL 18 resolves the old and the new row written as quoted identifiers too, and an inner scope's alias
    // "old" prints as old.id as the old row does.  The quoted names are what gate the query.
    [Fact]
    public async Task Describe_InsertSelectFromATableQuotedOld_KeepsTheHeuristicForOld()
    {
        Assert.SkipWhen(await ServerMajorAsync() < 18, "PostgreSQL before 18 has no old and new in RETURNING.");

        (
            await WalkedAsync(
                "INSERT INTO orders (user_id, total) SELECT \"old\".id, 0 FROM users \"old\" "
                    + "RETURNING \"old\".id, \"new\".id"
            )
        ).ShouldBe([(true, "heuristic"), (true, "heuristic")]);
    }

    // The quoted old row of an INSERT is NULL on every row, and the statement has no other word of the gate.
    [Fact]
    public async Task Describe_ReturningQuotedOldWithNoOtherGateWord_IsGatedAndKeepsTheHeuristic()
    {
        Assert.SkipWhen(await ServerMajorAsync() < 18, "PostgreSQL before 18 has no old and new in RETURNING.");

        (
            await WalkedAsync("INSERT INTO orders (user_id, total) VALUES (@user_id, @total) RETURNING \"old\".id")
        ).ShouldBe([(true, "heuristic")]);
    }

    // PostgreSQL 18's old row cannot be given the name of a table of the statement: the server refuses it.
    [Fact]
    public async Task Describe_OldAliasThatIsAlsoATablesAlias_IsRefusedByTheServer()
    {
        Assert.SkipWhen(await ServerMajorAsync() < 18, "PostgreSQL before 18 has no old and new in RETURNING.");

        var described = await Sessions.DescribeAsync(
            postgres.ConnectionString,
            Requests.Of(
                "UPDATE users u SET name = @name FROM orders o WHERE o.user_id = u.id "
                    + "RETURNING WITH (OLD AS o) o.id"
            )
        );

        var failure = described.Failure.ShouldNotBeNull();
        failure.Descriptor.Id.ShouldBe("SQLSRC225");
        failure
            .ServerLines.Any(static line => line.Contains("specified more than once", StringComparison.Ordinal))
            .ShouldBeTrue();
    }

    // PostgreSQL 17 proves the join's condition false from the NOT NULL and scans nothing: the plan prints a bare
    // "total" from a Result, which says nothing of a relation.  An older server scans orders, and the walk maps it.
    // Either way the column is nullable.
    [Fact]
    public async Task Describe_JoinConditionTheNewerPlannerProvesFalse_IsNullableOnEveryVersion()
    {
        var proved = await ServerMajorAsync() >= 17;

        var columns = await WalkedAsync(
            "SELECT u.id, o.total FROM users u LEFT JOIN orders o ON o.user_id = u.id AND o.id IS NULL"
        );

        columns.ShouldBe([(false, "catalog"), (true, proved ? "heuristic" : "outer-join")]);
    }

    // A relation the planner proved empty under an outer join is a Result, and a RETURNING list prints its column
    // as a bare name at the ModifyTable: not the target's, and nullable on every version.
    [Fact]
    public async Task Describe_ReturningOfARelationProvedEmpty_IsNullable()
    {
        var columns = await WalkedAsync(
            "DELETE FROM orders t USING users a LEFT JOIN billing.invoices b ON false "
                + "WHERE t.user_id = a.id RETURNING b.id, t.id"
        );

        columns[0].Nullable.ShouldBe(true);
        columns[1].ShouldBe((false, "catalog"));
    }

    // A partitioned table scans as an Append of its partitions, whose aliases are not the table's: its columns
    // keep the heuristic and are never false.
    [Fact]
    public async Task Describe_PartitionedTable_KeepsTheHeuristic()
    {
        await using (var source = NpgsqlDataSource.Create(postgres.ConnectionString))
        await using (var create = source.CreateCommand(Partitioned))
        {
            _ = await create.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        (await WalkedAsync("SELECT p.id, u.name FROM walk_parts p LEFT JOIN users u ON u.id = p.id")).ShouldBe([
            (true, "heuristic"),
            (true, "outer-join"),
        ]);
    }
}
