using System;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Tool.Describing.Postgres;
using SqlSource.Tool.Logging;
using Xunit;

namespace SqlSource.DatabaseTests.Postgres.Describer;

// Each catalog call against a real server, on a connection whose search_path puts domains named like the built-in
// types text, oid and int4 before pg_catalog: the casts and functions of its SQL are the catalog's, so it answers as
// on any other path.  An error of tables, relations, partitions or overloads is logged and never reported, so only a
// test of the call itself sees it.
[Collection(UsesPostgres.Name)]
public sealed class HiddenTypesCallTests(PostgresDatabases postgres)
{
    private async Task<(PostgresConnection Connection, PostgresLive Live)> HiddenAsync()
    {
        await postgres.ExecuteAsync(ScratchSchema.HidingTypes);
        var connection = new PostgresConnection(
            $"{postgres.ConnectionString};{ScratchSchema.HidingTypesPath}",
            NullRunLog.Instance
        );
        return (connection, new PostgresLive(connection));
    }

    private async Task<long> OidOfAsync(string relation) =>
        Convert.ToInt64(
            await postgres.ScalarAsync($"SELECT '{relation}'::pg_catalog.regclass::pg_catalog.oid"),
            System.Globalization.CultureInfo.InvariantCulture
        );

    [Fact]
    public async Task PrepareAsync_SearchPathHidingBuiltInTypes_AnswersTheParameterTypes()
    {
        var (connection, live) = await HiddenAsync();
        await using var _ = connection;

        var answer = await live.PrepareAsync(
            new PostgresPrepareRequest("hidden_prepare", "SELECT $1 AS n", ["pg_catalog.int4"]),
            TestContext.Current.CancellationToken
        );

        answer.Error.ShouldBeNull(answer.Error?.Message);
        answer.ParameterTypes.ShouldBe([23L]);
    }

    [Fact]
    public async Task TablesAsync_SearchPathHidingBuiltInTypes_AnswersTheRows()
    {
        var orders = await OidOfAsync("public.orders");
        var (connection, live) = await HiddenAsync();
        await using var _ = connection;

        var answer = await live.TablesAsync(new PostgresTablesRequest([orders]), TestContext.Current.CancellationToken);

        answer.Error.ShouldBeNull(answer.Error?.Message);
        var row = answer.Tables.ShouldHaveSingleItem();
        (row.Schema, row.Name, row.Relkind).ShouldBe(("public", "orders", "r"));
        row.Columns.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task RelationsAsync_SearchPathHidingBuiltInTypes_AnswersTheRows()
    {
        var (connection, live) = await HiddenAsync();
        await using var _ = connection;

        var answer = await live.RelationsAsync(
            new PostgresRelationsRequest([new PostgresRelationName("public", "orders")]),
            TestContext.Current.CancellationToken
        );

        answer.Error.ShouldBeNull(answer.Error?.Message);
        answer.Tables.ShouldNotBeNull().Select(static row => (row.Schema, row.Name)).ShouldBe([("public", "orders")]);
    }

    [Fact]
    public async Task PartitionsAsync_SearchPathHidingBuiltInTypes_AnswersTheAncestors()
    {
        await postgres.ExecuteAsync(ScratchSchema.Partitioned);
        var parent = await OidOfAsync("public.scratch_parts");
        var (connection, live) = await HiddenAsync();
        await using var _ = connection;

        var answer = await live.PartitionsAsync(
            new PostgresPartitionsRequest([new PostgresRelationName("public", "scratch_parts_1")]),
            TestContext.Current.CancellationToken
        );

        answer.Error.ShouldBeNull(answer.Error?.Message);
        answer
            .Rows.ShouldNotBeNull()
            .ShouldHaveSingleItem()
            .ShouldSatisfyAllConditions(
                row => (row.Schema, row.Name).ShouldBe(("public", "scratch_parts_1")),
                row => row.Ancestors.ShouldBe([parent])
            );
    }

    // The row of text is the server's own: format_type writes it with its schema on this path, which the name of a
    // sidecar takes off (PostgresTypeNames).
    [Fact]
    public async Task TypesAsync_SearchPathHidingBuiltInTypes_AnswersTheRows()
    {
        var (connection, live) = await HiddenAsync();
        await using var _ = connection;

        var answer = await live.TypesAsync(new PostgresTypesRequest([1009L]), TestContext.Current.CancellationToken);

        answer.Error.ShouldBeNull(answer.Error?.Message);
        answer
            .Rows.ShouldNotBeNull()
            .Select(static row => (row.Oid, row.Typname, row.Nspname, row.FormatType))
            .ShouldBe([
                (25L, "text", "pg_catalog", "pg_catalog.text"),
                (1009L, "_text", "pg_catalog", "pg_catalog.text[]"),
            ]);
    }

    [Fact]
    public async Task OverloadsAsync_SearchPathHidingBuiltInTypes_AnswersTheNames()
    {
        var (connection, live) = await HiddenAsync();
        await using var _ = connection;

        var answer = await live.OverloadsAsync(
            new PostgresOverloadsRequest(["count", "jsonb_build_object"], ["*"]),
            TestContext.Current.CancellationToken
        );

        answer.Error.ShouldBeNull(answer.Error?.Message);
        answer.Functions.ShouldNotBeNull().ShouldBeEmpty();
        answer.Operators.ShouldNotBeNull().ShouldBeEmpty();
    }

    [Fact]
    public async Task SpellingsAsync_SearchPathHidingBuiltInTypes_AnswersTheSpellings()
    {
        var (connection, live) = await HiddenAsync();
        await using var _ = connection;

        var answer = await live.SpellingsAsync(
            new PostgresSpellingsRequest([new PostgresTypmodPair(1043, 36)]),
            TestContext.Current.CancellationToken
        );

        answer.Error.ShouldBeNull(answer.Error?.Message);
        answer.Rows.ShouldNotBeNull().ShouldBe([new PostgresSpellingRow(1043, 36, "character varying(32)")]);
    }
}
