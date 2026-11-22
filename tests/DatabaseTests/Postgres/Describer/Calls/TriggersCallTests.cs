using System.Threading.Tasks;
using Shouldly;
using SqlSource.Tool.Describing.Postgres;
using SqlSource.Tool.Logging;
using Xunit;

namespace SqlSource.DatabaseTests.Postgres.Describer.Calls;

// "triggers" against a real server: whether a table or a partition in its tree has a BEFORE INSERT row trigger.
[Collection(UsesPostgres.Name)]
public sealed class TriggersCallTests(PostgresDatabases postgres)
{
    [Theory]
    [InlineData("t", false)]
    [InlineData("fired", true)]
    [InlineData("disabled", true)]
    [InlineData("after_insert", false)]
    [InlineData("per_statement", false)]
    [InlineData("before_update", false)]
    [InlineData("p", true)]
    [InlineData("p_high", true)]
    [InlineData("p_deep", true)]
    [InlineData("p_low", false)]
    [InlineData("q", false)]
    [InlineData("no_such_table", false)]
    public async Task TriggersAsync_Table_AnswersWhetherARowTriggerMayChangeTheRow(string table, bool expected)
    {
        try
        {
            await postgres.ExecuteAsync(ScratchSchema.Written);
            await using var connection = new PostgresConnection(postgres.ConnectionString, NullRunLog.Instance);
            var live = new PostgresLive(connection);

            var answer = await live.TriggersAsync(
                new PostgresTriggersRequest(new PostgresRelationName("written", table)),
                TestContext.Current.CancellationToken
            );

            answer.Error.ShouldBeNull(answer.Error?.Message);
            answer.AnyBeforeInsert.ShouldBe(expected);
        }
        finally
        {
            await postgres.ExecuteAsync(ScratchSchema.DropWritten);
        }
    }

    // A table of the name in another schema is another table.
    [Fact]
    public async Task TriggersAsync_NameInAnotherSchema_IsNotTheTable()
    {
        try
        {
            await postgres.ExecuteAsync(ScratchSchema.Written);
            await using var connection = new PostgresConnection(postgres.ConnectionString, NullRunLog.Instance);
            var live = new PostgresLive(connection);

            var answer = await live.TriggersAsync(
                new PostgresTriggersRequest(new PostgresRelationName("public", "fired")),
                TestContext.Current.CancellationToken
            );

            answer.Error.ShouldBeNull(answer.Error?.Message);
            answer.AnyBeforeInsert.ShouldBe(false);
        }
        finally
        {
            await postgres.ExecuteAsync(ScratchSchema.DropWritten);
        }
    }
}
