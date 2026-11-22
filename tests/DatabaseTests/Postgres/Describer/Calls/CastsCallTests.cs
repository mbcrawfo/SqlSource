using System.Threading.Tasks;
using Shouldly;
using SqlSource.Tool.Describing.Postgres;
using SqlSource.Tool.Describing.Postgres.Walk;
using SqlSource.Tool.Logging;
using Xunit;

namespace SqlSource.DatabaseTests.Postgres.Describer.Calls;

// "casts" against a real server: whether pg_cast holds a row for any of the ordered pairs asked.
[Collection(UsesPostgres.Name)]
public sealed class CastsCallTests(PostgresDatabases postgres)
{
    // What the walk's rule rests on, on every supported major: no cast joins two families.
    [Fact]
    public async Task CastsAsync_CrossFamilyPairsOfAStockServer_AnswersNone()
    {
        await using var connection = new PostgresConnection(postgres.ConnectionString, NullRunLog.Instance);
        var live = new PostgresLive(connection);

        var answer = await live.CastsAsync(SetOperationTypes.CrossFamilyPairs, TestContext.Current.CancellationToken);

        answer.Error.ShouldBeNull(answer.Error?.Message);
        answer.AnyCast.ShouldBe(false);
    }

    // The arrays are paired by index, each source with its target: integer to bigint has a row, text to bigint has
    // none, and boolean to text has one that text to boolean does not.
    [Theory]
    [InlineData(23, 20, true)]
    [InlineData(25, 20, false)]
    [InlineData(16, 25, true)]
    [InlineData(25, 16, false)]
    public async Task CastsAsync_PairAsked_AnswersWhetherItHasARow(long source, long target, bool expected)
    {
        await using var connection = new PostgresConnection(postgres.ConnectionString, NullRunLog.Instance);
        var live = new PostgresLive(connection);

        var answer = await live.CastsAsync(
            new PostgresCastsRequest([source, 1082], [target, 23]),
            TestContext.Current.CancellationToken
        );

        answer.Error.ShouldBeNull(answer.Error?.Message);
        answer.AnyCast.ShouldBe(expected);
    }

    [Fact]
    public async Task CastsAsync_UsersCastAcrossFamilies_AnswersIt()
    {
        try
        {
            await postgres.ExecuteAsync(ScratchSchema.ScanColumns);
            await postgres.ExecuteAsync(ScratchSchema.ScanColumnsCast);
            await using var connection = new PostgresConnection(postgres.ConnectionString, NullRunLog.Instance);
            var live = new PostgresLive(connection);

            var answer = await live.CastsAsync(
                SetOperationTypes.CrossFamilyPairs,
                TestContext.Current.CancellationToken
            );

            answer.Error.ShouldBeNull(answer.Error?.Message);
            answer.AnyCast.ShouldBe(true);
        }
        finally
        {
            await postgres.ExecuteAsync(ScratchSchema.DropScanColumns);
        }
    }
}
