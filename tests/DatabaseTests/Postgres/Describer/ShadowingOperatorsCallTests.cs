using System;
using System.Globalization;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Tool.Describing.Postgres;
using SqlSource.Tool.Logging;
using Xunit;

namespace SqlSource.DatabaseTests.Postgres.Describer;

// Each catalog call against a real server, on a connection whose search_path puts a schema of operators that answer
// false, on the argument types of the calls' comparisons, before pg_catalog: the operators of its SQL are the
// catalog's, so it answers as on the default path.  An error of tables, relations, partitions or overloads is logged
// and never reported, and a wrong answer of theirs costs a column its origin, so only a test of the call itself sees
// it.
[Collection(UsesPostgres.Name)]
public sealed class ShadowingOperatorsCallTests(PostgresDatabases postgres)
{
    // The call's answer on the default path, then on the shadowing one.  Both connections are opened before the
    // schema is made, so that what Npgsql reads of the types when it opens one is not read under the schema.
    private async Task<(TAnswer OnTheDefaultPath, TAnswer Shadowed)> AnswersAsync<TAnswer>(
        Func<PostgresLive, Task<TAnswer>> ask
    )
    {
        var token = TestContext.Current.CancellationToken;
        await using var plain = new PostgresConnection(postgres.ConnectionString, NullRunLog.Instance);
        await using var shadowed = new PostgresConnection(
            $"{postgres.ConnectionString};{ScratchSchema.ShadowingOperatorsPath}",
            NullRunLog.Instance
        );
        _ = await plain.OpenAsync(token);
        _ = await shadowed.OpenAsync(token);
        try
        {
            await postgres.ExecuteAsync(ScratchSchema.ShadowingOperators);
            return (await ask(new PostgresLive(plain)), await ask(new PostgresLive(shadowed)));
        }
        finally
        {
            await postgres.ExecuteAsync(ScratchSchema.DropShadowingOperators);
        }
    }

    private async Task<long> OidOfAsync(string relation) =>
        Convert.ToInt64(
            await postgres.ScalarAsync($"SELECT '{relation}'::pg_catalog.regclass::pg_catalog.oid"),
            CultureInfo.InvariantCulture
        );

    [Fact]
    public async Task PrepareAsync_OperatorsBeforeTheCatalog_AnswersAsOnTheDefaultPath()
    {
        var (expected, actual) = await AnswersAsync(live =>
            live.PrepareAsync(
                new PostgresPrepareRequest("shadowed_prepare", "SELECT $1 AS n", ["pg_catalog.int4"]),
                TestContext.Current.CancellationToken
            )
        );

        expected.Error.ShouldBeNull(expected.Error?.Message);
        expected.ParameterTypes.ShouldBe([23L]);
        actual.ShouldBeEquivalentTo(expected);
    }

    [Fact]
    public async Task TablesAsync_OperatorsBeforeTheCatalog_AnswersAsOnTheDefaultPath()
    {
        await postgres.ExecuteAsync(ScratchSchema.Partitioned);
        long[] oids = [await OidOfAsync("public.orders"), await OidOfAsync("public.scratch_parts")];

        var (expected, actual) = await AnswersAsync(live =>
            live.TablesAsync(new PostgresTablesRequest(oids), TestContext.Current.CancellationToken)
        );

        expected.Error.ShouldBeNull(expected.Error?.Message);
        expected.Tables.ShouldNotBeNull().Count.ShouldBe(2);
        actual.ShouldBeEquivalentTo(expected);
    }

    [Fact]
    public async Task RelationsAsync_OperatorsBeforeTheCatalog_AnswersAsOnTheDefaultPath()
    {
        await postgres.ExecuteAsync(ScratchSchema.Partitioned);

        var (expected, actual) = await AnswersAsync(live =>
            live.RelationsAsync(
                new PostgresRelationsRequest([
                    new PostgresRelationName("public", "orders"),
                    new PostgresRelationName("public", "scratch_parts"),
                ]),
                TestContext.Current.CancellationToken
            )
        );

        expected.Error.ShouldBeNull(expected.Error?.Message);
        expected.Tables.ShouldNotBeNull().Count.ShouldBe(2);
        actual.ShouldBeEquivalentTo(expected);
    }

    [Fact]
    public async Task PartitionsAsync_OperatorsBeforeTheCatalog_AnswersAsOnTheDefaultPath()
    {
        await postgres.ExecuteAsync(ScratchSchema.Partitioned);
        var parent = await OidOfAsync("public.scratch_parts");

        var (expected, actual) = await AnswersAsync(live =>
            live.PartitionsAsync(
                new PostgresPartitionsRequest([new PostgresRelationName("public", "scratch_parts_1")]),
                TestContext.Current.CancellationToken
            )
        );

        expected.Error.ShouldBeNull(expected.Error?.Message);
        expected.Rows.ShouldNotBeNull().ShouldHaveSingleItem().Ancestors.ShouldBe([parent]);
        actual.ShouldBeEquivalentTo(expected);
    }

    // An array, which links to its element, and a range, which links to its subtype and has a multirange.
    [Fact]
    public async Task TypesAsync_OperatorsBeforeTheCatalog_AnswersAsOnTheDefaultPath()
    {
        var (expected, actual) = await AnswersAsync(live =>
            live.TypesAsync(new PostgresTypesRequest([1009L, 3904L]), TestContext.Current.CancellationToken)
        );

        expected.Error.ShouldBeNull(expected.Error?.Message);
        expected.Rows.ShouldNotBeNull().Count.ShouldBe(4);
        actual.ShouldBeEquivalentTo(expected);
    }

    [Fact]
    public async Task SpellingsAsync_OperatorsBeforeTheCatalog_AnswersAsOnTheDefaultPath()
    {
        var (expected, actual) = await AnswersAsync(live =>
            live.SpellingsAsync(
                new PostgresSpellingsRequest([new PostgresTypmodPair(1043, 36)]),
                TestContext.Current.CancellationToken
            )
        );

        expected.Error.ShouldBeNull(expected.Error?.Message);
        _ = expected.Rows.ShouldNotBeNull().ShouldHaveSingleItem();
        actual.ShouldBeEquivalentTo(expected);
    }

    // The schema holds a "=", which overloads answers on any path; no schema but pg_catalog holds a count or a "*".
    [Fact]
    public async Task OverloadsAsync_OperatorsBeforeTheCatalog_AnswersAsOnTheDefaultPath()
    {
        var (expected, actual) = await AnswersAsync(live =>
            live.OverloadsAsync(
                new PostgresOverloadsRequest(["count"], ["=", "*"]),
                TestContext.Current.CancellationToken
            )
        );

        expected.Error.ShouldBeNull(expected.Error?.Message);
        expected.Functions.ShouldNotBeNull().ShouldBeEmpty();
        expected.Operators.ShouldBe(["="]);
        actual.ShouldBeEquivalentTo(expected);
    }
}
