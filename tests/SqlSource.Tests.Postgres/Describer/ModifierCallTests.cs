using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Tool.Describing.Postgres;
using SqlSource.Tool.Logging;
using Xunit;

namespace SqlSource.Tests.Postgres.Describer;

// "columns", "types" and "spellings" against a real server, for what they say of a type modifier.
[Collection(UsesPostgres.Name)]
public sealed class ModifierCallTests(PostgresContainer postgres)
{
    private const string Statement = "modifier_columns";

    private const string Query =
        "SELECT 'a'::varchar(32) AS v, 1.5::numeric(10, 2) AS n, 1::int4 AS i, now()::timestamp(3) AS t";

    [Fact]
    public async Task ColumnsAsync_ColumnsWithAndWithoutAModifier_GiveTheModifierOfTheRowDescription()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = new PostgresConnection(postgres.ConnectionString, NullRunLog.Instance);
        var live = new PostgresLive(connection);
        var prepared = await live.PrepareAsync(new PostgresPrepareRequest(Statement, Query, []), token);
        prepared.Error.ShouldBeNull(prepared.Error?.Message);

        var answer = await live.ColumnsAsync(new PostgresColumnsRequest(Statement, 0), token);

        answer.Error.ShouldBeNull(answer.Error?.Message);
        // varchar(32) is 32 and the header, numeric(10, 2) is its precision above sixteen bits and its scale plus
        // the header, timestamp(3) is its precision.
        answer.Columns.ShouldNotBeNull().Select(static column => column.TypeModifier).ShouldBe([36, 655366, -1, 3]);
    }

    [Fact]
    public async Task TypesAsync_DomainWithAndWithoutAModifier_GivesItsTyptypmod()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = new PostgresConnection(postgres.ConnectionString, NullRunLog.Instance);
        var live = new PostgresLive(connection);
        // The domain of the fixture schema: varchar(254), which is 254 and the header.  The closure holds its base too.
        await using var ask = await connection.CommandAsync("SELECT 'email_t'::regtype::oid::int8", token);
        var domain = (long)(await ask.ExecuteScalarAsync(token)).ShouldNotBeNull();

        var answer = await live.TypesAsync(new PostgresTypesRequest([23, domain]), token);

        answer.Error.ShouldBeNull(answer.Error?.Message);
        answer
            .Rows.ShouldNotBeNull()
            .Select(static row => (row.Oid, row.Typtypmod))
            .ShouldBe([(23L, -1), (1043L, -1), (domain, 258)]);
    }

    [Fact]
    public async Task SpellingsAsync_PairsOfTypeAndModifier_GivesTheServersSpellingInOrder()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = new PostgresConnection(postgres.ConnectionString, NullRunLog.Instance);
        var live = new PostgresLive(connection);

        var answer = await live.SpellingsAsync(
            new PostgresSpellingsRequest([
                new PostgresTypmodPair(23, -1),
                new PostgresTypmodPair(1043, 36),
                new PostgresTypmodPair(1700, 655366),
            ]),
            token
        );

        answer.Error.ShouldBeNull(answer.Error?.Message);
        answer
            .Rows.ShouldNotBeNull()
            .ShouldBe([
                new PostgresSpellingRow(23, -1, "integer"),
                new PostgresSpellingRow(1043, 36, "character varying(32)"),
                new PostgresSpellingRow(1700, 655366, "numeric(10,2)"),
            ]);
    }

    [Fact]
    public async Task SpellingsAsync_NoPairs_GivesNoRows()
    {
        await using var connection = new PostgresConnection(postgres.ConnectionString, NullRunLog.Instance);
        var live = new PostgresLive(connection);

        var answer = await live.SpellingsAsync(new PostgresSpellingsRequest([]), TestContext.Current.CancellationToken);

        answer.Error.ShouldBeNull(answer.Error?.Message);
        answer.Rows.ShouldNotBeNull().ShouldBeEmpty();
    }
}
