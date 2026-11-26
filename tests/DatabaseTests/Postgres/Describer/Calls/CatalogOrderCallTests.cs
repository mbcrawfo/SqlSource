using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Tool.Describing.Postgres;
using SqlSource.Tool.Logging;
using Xunit;

namespace SqlSource.DatabaseTests.Postgres.Describer.Calls;

// What the catalog calls read where the catalog's own order or kind decides it, against a real server: a rule's kind,
// the order of a partition's ancestors and the order of an enum's labels, none of which the fixture schema shows.
[Collection(UsesPostgres.Name)]
public sealed class CatalogOrderCallTests(PostgresDatabases postgres)
{
    // A DO ALSO rule leaves the table's row to the statement, so only the DO INSTEAD rule is one.
    [Fact]
    public async Task TablesAsync_DoAlsoRule_IsNoInsteadRule()
    {
        try
        {
            await postgres.ExecuteAsync(ScratchSchema.Written);
            long[] oids = [await OidOfAsync("written.also"), await OidOfAsync("written.ruled")];

            var answer = await AskAsync(live =>
                live.TablesAsync(new PostgresTablesRequest(oids), TestContext.Current.CancellationToken)
            );

            answer.Error.ShouldBeNull(answer.Error?.Message);
            answer
                .Tables.ShouldNotBeNull()
                .Select(static table => (table.Name, table.HasInsteadRule))
                .ShouldBe([("also", false), ("ruled", true)], ignoreOrder: true);
        }
        finally
        {
            await postgres.ExecuteAsync(ScratchSchema.DropWritten);
        }
    }

    // The nearest ancestor comes first, whatever the order of the OIDs: the leaf's parent is made after its
    // grandparent, so its OID is the higher.
    [Fact]
    public async Task PartitionsAsync_PartitionTwoLevelsDown_AnswersItsAncestorsNearestFirst()
    {
        try
        {
            await postgres.ExecuteAsync(ScratchSchema.Written);
            long[] ancestors = [await OidOfAsync("written.p_high"), await OidOfAsync("written.p")];

            var answer = await AskAsync(live =>
                live.PartitionsAsync(
                    new PostgresPartitionsRequest([new PostgresRelationName("written", "p_deep")]),
                    TestContext.Current.CancellationToken
                )
            );

            answer.Error.ShouldBeNull(answer.Error?.Message);
            answer.Rows.ShouldNotBeNull().ShouldHaveSingleItem().Ancestors.ShouldBe(ancestors);
        }
        finally
        {
            await postgres.ExecuteAsync(ScratchSchema.DropWritten);
        }
    }

    // A label added BEFORE another sorts before it, though it was added after and its text sorts after it.
    [Fact]
    public async Task TypesAsync_EnumWithALabelAddedBefore_AnswersTheLabelsInTheirOrder()
    {
        await postgres.ExecuteAsync(
            "DROP TYPE IF EXISTS public.td_ordered; CREATE TYPE public.td_ordered AS ENUM ('a')"
        );
        try
        {
            await postgres.ExecuteAsync("ALTER TYPE public.td_ordered ADD VALUE 'b' BEFORE 'a'");
            var oid = Convert.ToInt64(
                await postgres.ScalarAsync("SELECT 'public.td_ordered'::pg_catalog.regtype::pg_catalog.oid"),
                CultureInfo.InvariantCulture
            );

            var answer = await AskAsync(live =>
                live.TypesAsync(new PostgresTypesRequest([oid]), TestContext.Current.CancellationToken)
            );

            answer.Error.ShouldBeNull(answer.Error?.Message);
            answer.Rows.ShouldNotBeNull().Single(row => row.Oid == oid).EnumLabels.ShouldBe(["b", "a"]);
        }
        finally
        {
            await postgres.ExecuteAsync("DROP TYPE public.td_ordered");
        }
    }

    private async Task<TAnswer> AskAsync<TAnswer>(Func<PostgresLive, Task<TAnswer>> ask)
    {
        await using var connection = new PostgresConnection(postgres.ConnectionString, NullRunLog.Instance);
        return await ask(new PostgresLive(connection));
    }

    private async Task<long> OidOfAsync(string relation) =>
        Convert.ToInt64(
            await postgres.ScalarAsync($"SELECT '{relation}'::pg_catalog.regclass::pg_catalog.oid"),
            CultureInfo.InvariantCulture
        );
}
