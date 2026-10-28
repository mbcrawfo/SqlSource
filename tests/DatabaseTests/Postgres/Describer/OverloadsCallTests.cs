using System.Threading.Tasks;
using Shouldly;
using SqlSource.Tool.Describing.Postgres;
using SqlSource.Tool.Logging;
using Xunit;

namespace SqlSource.DatabaseTests.Postgres.Describer;

// "overloads" against a real server: which of the names asked for a schema other than pg_catalog holds, or
// pg_catalog holds as an object that is not built in.
[Collection(UsesPostgres.Name)]
public sealed class OverloadsCallTests(PostgresContainer postgres)
{
    // Every test that makes the schema "shadow" drops it, so the server holds none of these names but in pg_catalog.
    [Fact]
    public async Task OverloadsAsync_NamesOnlyTheCatalogHolds_AnswersNone()
    {
        await using var connection = new PostgresConnection(postgres.ConnectionString, NullRunLog.Instance);
        var live = new PostgresLive(connection);

        var answer = await live.OverloadsAsync(
            new PostgresOverloadsRequest(["count", "jsonb_build_object", "tstzrange"], ["*"]),
            TestContext.Current.CancellationToken
        );

        answer.Error.ShouldBeNull(answer.Error?.Message);
        answer.Functions.ShouldNotBeNull().ShouldBeEmpty();
        answer.Operators.ShouldNotBeNull().ShouldBeEmpty();
    }

    // The schema is not on the connection's search path: another schema holds a name whatever the path says.
    [Fact]
    public async Task OverloadsAsync_NamesAnotherSchemaHolds_AnswersThem()
    {
        try
        {
            await postgres.ExecuteAsync(ScratchSchema.ShadowCount);
            await postgres.ExecuteAsync(ScratchSchema.ShadowOperators);
            await using var connection = new PostgresConnection(postgres.ConnectionString, NullRunLog.Instance);
            var live = new PostgresLive(connection);

            var answer = await live.OverloadsAsync(
                new PostgresOverloadsRequest(["array_agg", "count", "max"], ["*", "+"]),
                TestContext.Current.CancellationToken
            );

            answer.Error.ShouldBeNull(answer.Error?.Message);
            answer.Functions.ShouldBe(["count", "max"]);
            answer.Operators.ShouldBe(["*"]);
        }
        finally
        {
            await postgres.ExecuteAsync(ScratchSchema.DropShadow);
        }
    }

    // A superuser, or an extension made in pg_catalog, adds an overload there: it is not the stock one the walk pins.
    [Fact]
    public async Task OverloadsAsync_NameAddedToTheCatalog_AnswersIt()
    {
        try
        {
            await postgres.ExecuteAsync(ScratchSchema.CatalogCount);
            await using var connection = new PostgresConnection(postgres.ConnectionString, NullRunLog.Instance);
            var live = new PostgresLive(connection);

            var answer = await live.OverloadsAsync(
                new PostgresOverloadsRequest(["array_agg", "count", "max"], ["*", "+"]),
                TestContext.Current.CancellationToken
            );

            answer.Error.ShouldBeNull(answer.Error?.Message);
            answer.Functions.ShouldBe(["count"]);
            answer.Operators.ShouldNotBeNull().ShouldBeEmpty();
        }
        finally
        {
            await postgres.ExecuteAsync(ScratchSchema.DropCatalogCount);
        }
    }

    [Fact]
    public async Task OverloadsAsync_OperatorAddedToTheCatalog_AnswersIt()
    {
        try
        {
            await postgres.ExecuteAsync(ScratchSchema.CatalogOperator);
            await using var connection = new PostgresConnection(postgres.ConnectionString, NullRunLog.Instance);
            var live = new PostgresLive(connection);

            var answer = await live.OverloadsAsync(
                new PostgresOverloadsRequest([], ["*", "+"]),
                TestContext.Current.CancellationToken
            );

            answer.Error.ShouldBeNull(answer.Error?.Message);
            answer.Functions.ShouldNotBeNull().ShouldBeEmpty();
            answer.Operators.ShouldBe(["*"]);
        }
        finally
        {
            await postgres.ExecuteAsync(ScratchSchema.DropCatalogOperator);
        }
    }

    // The call is made only when the session finds pg_catalog first, yet a user's operator of a comparison's exact
    // argument types, wherever it stands on the path, beats one of pg_catalog's that needs a cast: each comparison
    // must have the exact types of one of pg_catalog's.
    [Fact]
    public async Task OverloadsAsync_OperatorsOfExactTypesAfterTheCatalog_AnswersTheNamesHeld()
    {
        try
        {
            await postgres.ExecuteAsync(ScratchSchema.OperatorsAfterTheCatalog);
            await using var connection = new PostgresConnection(
                $"{postgres.ConnectionString};{ScratchSchema.OperatorsAfterTheCatalogPath}",
                NullRunLog.Instance
            );
            var live = new PostgresLive(connection);

            var answer = await live.OverloadsAsync(
                new PostgresOverloadsRequest(["count"], ["<>"]),
                TestContext.Current.CancellationToken
            );

            answer.Error.ShouldBeNull(answer.Error?.Message);
            answer.Functions.ShouldBe(["count"]);
            answer.Operators.ShouldBe(["<>"]);
        }
        finally
        {
            await postgres.ExecuteAsync(ScratchSchema.DropOperatorsAfterTheCatalog);
        }
    }

    [Fact]
    public async Task OverloadsAsync_NoNames_AnswersNone()
    {
        await using var connection = new PostgresConnection(postgres.ConnectionString, NullRunLog.Instance);
        var live = new PostgresLive(connection);

        var answer = await live.OverloadsAsync(
            new PostgresOverloadsRequest([], []),
            TestContext.Current.CancellationToken
        );

        answer.Error.ShouldBeNull(answer.Error?.Message);
        answer.Functions.ShouldNotBeNull().ShouldBeEmpty();
        answer.Operators.ShouldNotBeNull().ShouldBeEmpty();
    }
}
