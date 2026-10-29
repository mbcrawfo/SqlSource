using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Tool.Describing.Postgres;
using SqlSource.Tool.Logging;
using Xunit;

namespace SqlSource.DatabaseTests.Postgres.Describer;

// Each catalog call against a real server, on a connection whose search_path puts a schema of operators that answer
// false, on the argument types of the calls' comparisons, before pg_catalog: the operators of its SQL are the
// catalog's, so it answers as on the default path.  Each request reaches every comparison of its text, so that the
// answer changes when any one is shadowed, but for the schema of an operator in overloads, which no operator lets a
// test reach.  An error of tables, relations, partitions or overloads is logged and never reported, and a wrong answer
// of theirs costs a column its origin, so only a test of the call itself sees it.
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

    // A plain table, a partitioned one, a partitioned one with a foreign partition and a table with an INSTEAD rule:
    // every comparison of the text changes some row when it is shadowed.
    [Fact]
    public async Task TablesAsync_OperatorsBeforeTheCatalog_AnswersAsOnTheDefaultPath()
    {
        await MakeTablesAsync();
        long[] oids =
        [
            await OidOfAsync("public.orders"),
            await OidOfAsync("public.scratch_parts"),
            await OidOfAsync("public.fp_parted"),
            await OidOfAsync("public.ir_target"),
        ];

        var (expected, actual) = await AnswersAsync(live =>
            live.TablesAsync(new PostgresTablesRequest(oids), TestContext.Current.CancellationToken)
        );

        expected.Error.ShouldBeNull(expected.Error?.Message);
        expected
            .Tables.ShouldNotBeNull()
            .Select(static table => (table.Name, table.HasForeignPartition, table.HasInsteadRule))
            .ShouldBe(
                [
                    ("orders", false, false),
                    ("scratch_parts", false, false),
                    ("fp_parted", true, false),
                    ("ir_target", false, true),
                ],
                ignoreOrder: true
            );
        actual.ShouldBeEquivalentTo(expected);
    }

    [Fact]
    public async Task RelationsAsync_OperatorsBeforeTheCatalog_AnswersAsOnTheDefaultPath()
    {
        await MakeTablesAsync();

        var (expected, actual) = await AnswersAsync(live =>
            live.RelationsAsync(
                new PostgresRelationsRequest([
                    new PostgresRelationName("public", "orders"),
                    new PostgresRelationName("public", "scratch_parts"),
                    new PostgresRelationName("public", "fp_parted"),
                    new PostgresRelationName("public", "ir_target"),
                ]),
                TestContext.Current.CancellationToken
            )
        );

        expected.Error.ShouldBeNull(expected.Error?.Message);
        expected.Tables.ShouldNotBeNull().Count.ShouldBe(4);
        actual.ShouldBeEquivalentTo(expected);
    }

    private async Task MakeTablesAsync()
    {
        await postgres.ExecuteAsync(ScratchSchema.Partitioned);
        await postgres.ExecuteAsync(ScratchSchema.ForeignPartitions([with(postgres.ConnectionString)]));
        await postgres.ExecuteAsync(ScratchSchema.InsteadRule);
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

    // An array, which links to its element, a multirange, which links to its range, which links to its subtype, a
    // domain, which links to its base, and an enum, whose labels are read.  The range is not asked for, so that only
    // the multirange's link reaches it.
    [Fact]
    public async Task TypesAsync_OperatorsBeforeTheCatalog_AnswersAsOnTheDefaultPath()
    {
        var status = await TypeOidAsync("public.user_status");
        var email = await TypeOidAsync("public.email_t");

        var (expected, actual) = await AnswersAsync(live =>
            live.TypesAsync(
                new PostgresTypesRequest([1009L, 4451L, status, email]),
                TestContext.Current.CancellationToken
            )
        );

        expected.Error.ShouldBeNull(expected.Error?.Message);
        var rows = expected.Rows.ShouldNotBeNull();
        rows.Select(static row => row.Oid).ShouldBe([23L, 25L, 1009L, 1043L, 3904L, 4451L, status, email], true);
        rows.Single(row => row.Oid == status).EnumLabels.ShouldBe(["active", "disabled"]);
        actual.ShouldBeEquivalentTo(expected);
    }

    private async Task<long> TypeOidAsync(string type) =>
        Convert.ToInt64(
            await postgres.ScalarAsync($"SELECT '{type}'::pg_catalog.regtype::pg_catalog.oid"),
            CultureInfo.InvariantCulture
        );

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

    // Each comparison changes the answer when it is shadowed: a count and a "*" added to pg_catalog are held by their
    // OIDs alone, information_schema's _pg_expandarray by its schema alone, and the schema's own never and "=" by
    // both.  No operator outside pg_catalog has an OID below FirstNormalObjectId, so none is held by its schema
    // alone.
    [Fact]
    public async Task OverloadsAsync_OperatorsBeforeTheCatalog_AnswersAsOnTheDefaultPath()
    {
        try
        {
            await postgres.ExecuteAsync(ScratchSchema.CatalogCount);
            await postgres.ExecuteAsync(ScratchSchema.CatalogOperator);

            var (expected, actual) = await AnswersAsync(live =>
                live.OverloadsAsync(
                    new PostgresOverloadsRequest(["_pg_expandarray", "count", "never"], ["*", "="]),
                    TestContext.Current.CancellationToken
                )
            );

            expected.Error.ShouldBeNull(expected.Error?.Message);
            expected.Functions.ShouldBe(["_pg_expandarray", "count", "never"]);
            expected.Operators.ShouldBe(["*", "="]);
            actual.ShouldBeEquivalentTo(expected);
        }
        finally
        {
            await postgres.ExecuteAsync(ScratchSchema.DropCatalogCount);
            await postgres.ExecuteAsync(ScratchSchema.DropCatalogOperator);
        }
    }
}
