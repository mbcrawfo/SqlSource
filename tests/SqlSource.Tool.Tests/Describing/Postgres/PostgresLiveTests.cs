using System;
using System.Threading.Tasks;
using Npgsql;
using Shouldly;
using SqlSource.Tool.Describing.Postgres;
using Xunit;

namespace SqlSource.Tool.Tests.Describing.Postgres;

// The texts the live functions make themselves.  What they do with a server is in tests/SqlSource.Tests.Postgres.
public sealed class PostgresLiveTests
{
    [Theory]
    [InlineData("18.4 (Debian 18.4-1.pgdg13+1)", "18.4")]
    [InlineData("14.24", "14.24")]
    [InlineData("14.10 (Ubuntu 14.10-1)", "14.10")]
    [InlineData("19beta1 (Ubuntu 19~beta1-1)", "19beta1")]
    public void VersionOf_ServerVersion_IsCutAtItsFirstSpace(string reported, string expected) =>
        PostgresLive.VersionOf(reported).ShouldBe(expected);

    [Fact]
    public void ExecuteText_StatementWithoutParameters_HasNoList() =>
        PostgresLive.ExecuteText(new PostgresColumnsRequest("sqlsource_1", 0)).ShouldBe("EXECUTE sqlsource_1");

    [Fact]
    public void ExecuteText_StatementWithParameters_HasANullForEach() =>
        PostgresLive
            .ExecuteText(new PostgresColumnsRequest("sqlsource_7", 3))
            .ShouldBe("EXECUTE sqlsource_7(NULL, NULL, NULL)");

    [Fact]
    public void ExplainText_StatementWithoutParameters_ExplainsTheExecuteAlone() =>
        PostgresLive
            .ExplainText(new PostgresExplainRequest("sqlsource_1", 0, []))
            .ShouldBe("EXPLAIN (VERBOSE, FORMAT JSON) EXECUTE sqlsource_1");

    [Fact]
    public void ExplainText_StatementWithParameters_HasANullForEach() =>
        PostgresLive
            .ExplainText(new PostgresExplainRequest("sqlsource_7", 2, []))
            .ShouldBe("EXPLAIN (VERBOSE, FORMAT JSON) EXECUTE sqlsource_7(NULL, NULL)");

    [Fact]
    public void SetTexts_RequestWithSettings_ForcesTheGenericPlanWithoutPruningThenSetsEach() =>
        PostgresLive
            .SetTexts(new PostgresExplainRequest("sqlsource_1", 0, ["enable_nestloop=off", "enable_hashjoin=off"]))
            .ShouldBe([
                "SET LOCAL plan_cache_mode = force_generic_plan",
                "SET LOCAL enable_partition_pruning = off",
                "SET LOCAL enable_nestloop = off",
                "SET LOCAL enable_hashjoin = off",
            ]);

    [Fact]
    public void SetTexts_SettingOutsideTheList_Throws() =>
        Should.Throw<ArgumentException>(() =>
            PostgresLive.SetTexts(new PostgresExplainRequest("sqlsource_1", 0, ["work_mem=1"]))
        );

    // An NpgsqlException, whatever its cause, is the answer's error, made by errorOf: even one thrown after a
    // cancellation, since a cancelled command is an error of the server's own.
    [Fact]
    public async Task AnswerAsync_CallThatThrowsAnNpgsqlException_IsTheFailedAnswer()
    {
        var thrown = new NpgsqlException("the connection failed");
        NpgsqlException? seen = null;

        var answer = await PostgresLive.AnswerAsync(
            () => throw thrown,
            exception =>
            {
                seen = exception;
                return new PostgresServerError("08006", "made by errorOf", null, null, null);
            },
            error => new PostgresDeallocateAnswer(error)
        );

        seen.ShouldBeSameAs(thrown);
        answer.ShouldBe(
            new PostgresDeallocateAnswer(new PostgresServerError("08006", "made by errorOf", null, null, null))
        );
    }

    [Fact]
    public async Task AnswerAsync_CallThatIsCancelled_Throws() =>
        await Should.ThrowAsync<OperationCanceledException>(() =>
            PostgresLive.AnswerAsync<PostgresDeallocateAnswer>(
                () => throw new OperationCanceledException(),
                _ => throw new InvalidOperationException("errorOf must not run"),
                _ => throw new InvalidOperationException("failed must not run")
            )
        );

    [Fact]
    public async Task AnswerAsync_CallThatAnswers_IsItsAnswer()
    {
        var answer = await PostgresLive.AnswerAsync(
            () => Task.FromResult(new PostgresDeallocateAnswer(null)),
            _ => throw new InvalidOperationException("errorOf must not run"),
            _ => throw new InvalidOperationException("failed must not run")
        );

        answer.ShouldBe(new PostgresDeallocateAnswer(null));
    }
}
