using System;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Tool.Describing.Capture;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Testing;
using Xunit;
using static SqlSource.Tool.Testing.NullabilityAnswers;

namespace SqlSource.Tests.Postgres.Describer;

// The nullability walk against a real server: what only a planner can show, and what a describe leaves behind.
// The matrix of join shapes is the fixture cases named walk-*, which FixtureCasesTests describes here.
[Collection(UsesPostgres.Name)]
public sealed class WalkTests(PostgresContainer postgres)
{
    private Task<QueryDescription> ShouldDescribeAsync(string sql) =>
        Sessions.ShouldDescribeAsync(postgres.ConnectionString, sql);

    private async Task<(bool? Nullable, string? Source)[]> WalkedAsync(string sql)
    {
        var description = await ShouldDescribeAsync(sql);

        description.Plan.ShouldBe(SidecarValues.Plan.Walked);
        return Of(description.Columns.ShouldNotBeNull());
    }

    // A left join: the side it keeps is not null by the catalog, and the side it extends is nullable by the join.
    [Fact]
    public async Task DescribeAsync_LeftJoin_KeepsTheOuterSideNotNullAndExtendsTheInnerOne() =>
        (
            await WalkedAsync(
                "SELECT u.id, u.name, o.id AS order_id, o.total "
                    + "FROM users u LEFT JOIN orders o ON o.user_id = u.id WHERE u.id = @id"
            )
        ).ShouldBe([NotNullCatalog, NotNullCatalog, NullableOuterJoin, NullableOuterJoin]);

    // With a condition on the nullable side the planner makes the join inner, which the text does not show.
    [Fact]
    public async Task DescribeAsync_LeftJoinThatAWhereMakesInner_IsNotNullThroughout() =>
        (
            await WalkedAsync(
                "SELECT u.id, u.name, o.id AS order_id, o.total "
                    + "FROM users u LEFT JOIN orders o ON o.user_id = u.id WHERE u.id = @id AND o.total > @min"
            )
        ).ShouldBe([NotNullCatalog, NotNullCatalog, NotNullCatalog, NotNullCatalog]);

    // A column with no origin is answered from the plan, so a query without a word of the gate is planned for it,
    // and its column of a table keeps the catalog's answer.
    [Fact]
    public async Task DescribeAsync_UngatedQueryWithAnExpression_IsWalkedAndKeepsTheCatalogsAnswer()
    {
        var description = await ShouldDescribeAsync("SELECT u.id, count(*) OVER () AS n FROM users u");

        description.Plan.ShouldBe(SidecarValues.Plan.Walked);
        var id = description.Columns.ShouldNotBeNull()[0];
        (id.Nullable, id.NullableSource).ShouldBe(NotNullCatalog);
    }

    [Fact]
    public async Task DescribeAsync_UngatedQueryWhoseEveryColumnHasAnOrigin_NeedsNoPlan() =>
        (await ShouldDescribeAsync("SELECT u.id, u.name FROM users u")).Plan.ShouldBe(SidecarValues.Plan.NotNeeded);

    // Column aliases print the columns under the aliases, which the check of the plan's Output takes for another
    // statement's: the walk still answers the columns, and reads no expression.
    [Fact]
    public async Task DescribeAsync_TableWithColumnAliases_IsWalked() =>
        (await WalkedAsync("SELECT u.a, o.total FROM users u(a, b) LEFT JOIN orders o ON o.user_id = u.a")).ShouldBe([
            NotNullCatalog,
            NullableOuterJoin,
        ]);

    // The tool leaves the database as it found it: no statement, and the settings as "open" read them.
    [Fact]
    public async Task DescribeAsync_GatedQuery_LeavesNoStatementAndThePlanCacheModeAsOpenReadIt()
    {
        var token = TestContext.Current.CancellationToken;
        await using var session = await Sessions.OpenSessionAsync(postgres.ConnectionString);
        var read = session.Server.Settings.Single(static setting => setting.Name == "plan_cache_mode").Value;

        var described = await session.DescribeAsync(
            DescribeRequests.Of("SELECT u.id, o.total FROM users u LEFT JOIN orders o ON o.user_id = u.id"),
            token
        );

        described.Value.ShouldNotBeNull().Plan.ShouldBe(SidecarValues.Plan.Walked);
        (await Sessions.PreparedStatementsAsync(session)).ShouldBe(0);
        await using var show = await session.Connection.CommandAsync("SHOW plan_cache_mode", token);
        (await show.ExecuteScalarAsync(token)).ShouldBe(read);
    }

    // The ModifyTable sits over a scan of its own target with the same alias: one instance, not a doubled alias.
    [Fact]
    public async Task DescribeAsync_UpdateFromReturning_GivesTheTargetTheCatalogsAnswer() =>
        (
            await WalkedAsync(
                "UPDATE users u SET name = @name FROM orders o LEFT JOIN billing.invoices i ON i.order_id = o.id "
                    + "WHERE o.user_id = u.id RETURNING u.id, o.total, i.amount"
            )
        ).ShouldBe([NotNullCatalog, NotNullCatalog, NullableOuterJoin]);

    // A DELETE of one table prints bare names, which are its target's.  The function's name is what gates it.
    [Fact]
    public async Task DescribeAsync_DeleteReturningBareNames_GivesTheTargetTheCatalogsAnswer() =>
        (await WalkedAsync("DELETE FROM orders WHERE left(note, 1) = @initial RETURNING id, note")).ShouldBe([
            NotNullCatalog,
            NullableCatalog,
        ]);

    // The gate lets the query in for the word, and here the word is a table's alias.  The plan prints a table
    // aliased old as it prints PostgreSQL 18's old row, so in a statement that names the word any alias but the
    // target's keeps the heuristic, on every version.
    [Fact]
    public async Task DescribeAsync_TableAliasedOld_KeepsTheHeuristicForItsColumns() =>
        (
            await WalkedAsync(
                "UPDATE users u SET name = @name FROM orders old WHERE old.user_id = u.id RETURNING old.total, u.id"
            )
        ).ShouldBe([NullableHeuristic, NotNullCatalog]);

    // An alias of an inner scope reaches the plan under its own name: the SELECT of an INSERT ... SELECT aliases
    // users old, and the old row of the RETURNING list prints as old.id too, NULL on every row an INSERT returns.
    [Fact]
    public async Task DescribeAsync_InsertSelectFromATableAliasedOld_KeepsTheHeuristicForOld()
    {
        postgres.SkipBelow(18, PostgresContainer.NoOldAndNewInReturning);

        (
            await WalkedAsync(
                "INSERT INTO orders (user_id, total) SELECT old.id, 0 FROM users old RETURNING old.id, new.id"
            )
        ).ShouldBe([NullableHeuristic, NullableHeuristic]);
    }

    // PostgreSQL 18 resolves the old and the new row written as quoted identifiers too, and an inner scope's alias
    // "old" prints as old.id as the old row does.  The quoted names are what gate the query.
    [Fact]
    public async Task DescribeAsync_InsertSelectFromATableQuotedOld_KeepsTheHeuristicForOld()
    {
        postgres.SkipBelow(18, PostgresContainer.NoOldAndNewInReturning);

        (
            await WalkedAsync(
                "INSERT INTO orders (user_id, total) SELECT \"old\".id, 0 FROM users \"old\" "
                    + "RETURNING \"old\".id, \"new\".id"
            )
        ).ShouldBe([NullableHeuristic, NullableHeuristic]);
    }

    // The quoted old row of an INSERT is NULL on every row, and the statement has no other word of the gate.
    [Fact]
    public async Task DescribeAsync_ReturningQuotedOldWithNoOtherGateWord_IsGatedAndKeepsTheHeuristic()
    {
        postgres.SkipBelow(18, PostgresContainer.NoOldAndNewInReturning);

        (
            await WalkedAsync("INSERT INTO orders (user_id, total) VALUES (@user_id, @total) RETURNING \"old\".id")
        ).ShouldBe([NullableHeuristic]);
    }

    // PostgreSQL 18's old row cannot be given the name of a table of the statement: the server refuses it.
    [Fact]
    public async Task DescribeAsync_OldAliasThatIsAlsoATablesAlias_IsRefusedByTheServer()
    {
        postgres.SkipBelow(18, PostgresContainer.NoOldAndNewInReturning);

        var described = await Sessions.DescribeAsync(
            postgres.ConnectionString,
            DescribeRequests.Of(
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
    public async Task DescribeAsync_JoinConditionTheNewerPlannerProvesFalse_IsNullableOnEveryVersion()
    {
        var proved = postgres.Major >= 17;

        var columns = await WalkedAsync(
            "SELECT u.id, o.total FROM users u LEFT JOIN orders o ON o.user_id = u.id AND o.id IS NULL"
        );

        columns.ShouldBe([NotNullCatalog, proved ? NullableHeuristic : NullableOuterJoin]);
    }

    // A relation the planner proved empty under an outer join is a Result, and a RETURNING list prints its column
    // as a bare name at the ModifyTable: not the target's, and nullable on every version.
    [Fact]
    public async Task DescribeAsync_ReturningOfARelationProvedEmpty_IsNullable()
    {
        var columns = await WalkedAsync(
            "DELETE FROM orders t USING users a LEFT JOIN billing.invoices b ON false "
                + "WHERE t.user_id = a.id RETURNING b.id, t.id"
        );

        columns[0].Nullable.ShouldBe(true);
        columns[1].ShouldBe(NotNullCatalog);
    }

    // A partitioned table scans as an Append of its partitions, whose aliases are the table's and a number: the walk
    // reads it as the table, by the partitions' ancestors.
    [Fact]
    public async Task DescribeAsync_PartitionedTable_IsAnsweredByItsSide()
    {
        await postgres.ExecuteAsync(ScratchSchema.Partitioned);

        (await WalkedAsync("SELECT p.id, u.name FROM scratch_parts p LEFT JOIN users u ON u.id = p.id")).ShouldBe([
            NotNullCatalog,
            NullableOuterJoin,
        ]);
    }

    // A query described with the planner's settings, with the plan "explain" answered.
    private async Task<(QueryDescription Description, string Plan)> DescribeWithSettingsAsync(
        string sql,
        params string[] settings
    )
    {
        var (described, capture) = await Sessions.DescribeCapturedAsync(
            postgres.ConnectionString,
            DescribeRequests.Of(sql),
            plannerSettings: settings
        );

        described.Failure.ShouldBeNull(described.Failure?.Descriptor.Id);
        var explain = capture.Calls.Single(static call => call.Method == "explain");
        return (described.Value.ShouldNotBeNull(), CaptureJson.TextOf(explain.Answer));
    }

    // A row of a full join has a row of one side at least, so the COALESCE of a NOT NULL column of each side, which
    // 14 and 18 compute at the join, a hash join and a merge join alike, is never NULL.
    [Theory]
    [InlineData("Hash Join", "enable_mergejoin=off", "enable_nestloop=off")]
    [InlineData("Merge Join", "enable_hashjoin=off", "enable_nestloop=off")]
    public async Task DescribeAsync_CoalesceOverBothSidesOfAFullJoin_IsNotNull(string join, string first, string second)
    {
        var (description, plan) = await DescribeWithSettingsAsync(
            "SELECT coalesce(u.id, o.user_id) AS k FROM users u FULL JOIN orders o ON o.user_id = u.id",
            first,
            second
        );

        plan.ShouldContain(join);
        description.Plan.ShouldBe(SidecarValues.Plan.Walked);
        Of(description.Columns.ShouldNotBeNull()).ShouldBe([NotNullExpression]);
    }

    // A COALESCE at a full join that is not over one NOT NULL column of each side, each extended by that join alone:
    // both of one side, a side with a LEFT JOIN under it, the full join on the side a LEFT JOIN extends, a cast of a
    // side's column, and a side's column that may be NULL.  The upper condition of the third is not strict in either
    // side, so the planner keeps the full join, and each plan is asserted to hold one.
    [Theory]
    [InlineData("SELECT coalesce(o.user_id, o.user_id) AS k FROM users u FULL JOIN orders o ON o.user_id = u.id")]
    [InlineData(
        "SELECT coalesce(u.id, o.user_id) AS k "
            + "FROM (users u LEFT JOIN bookings b ON b.user_id = u.id) FULL JOIN orders o ON o.user_id = u.id"
    )]
    [InlineData(
        "SELECT coalesce(u.id, o.user_id) AS k "
            + "FROM bookings b LEFT JOIN (users u FULL JOIN orders o ON o.user_id = u.id) "
            + "ON b.user_id = coalesce(u.id, o.user_id)"
    )]
    [InlineData("SELECT coalesce(o.id, u.id) AS k FROM users u FULL JOIN orders o ON o.user_id = u.id")]
    [InlineData("SELECT coalesce(u.deleted_at, o.placed_at) AS k FROM users u FULL JOIN orders o ON o.user_id = u.id")]
    public async Task DescribeAsync_CoalesceAtAFullJoinNotOverEachSide_KeepsNoOrigin(string sql)
    {
        var (description, plan) = await DescribeWithSettingsAsync(sql);

        var compact = plan.Replace("\\", "", StringComparison.Ordinal).Replace(" ", "", StringComparison.Ordinal);
        compact.ShouldContain("\"JoinType\":\"Full\"", Case.Sensitive, "the plan holds no full join");
        description.Plan.ShouldBe(SidecarValues.Plan.Walked);
        Of(description.Columns.ShouldNotBeNull()).ShouldBe([UnknownNoOrigin]);
    }
}
