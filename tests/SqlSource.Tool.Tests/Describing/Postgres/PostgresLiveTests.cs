using System;
using System.IO;
using System.Threading.Tasks;
using Npgsql;
using Shouldly;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Describing.Postgres;
using Xunit;

namespace SqlSource.Tool.Tests.Describing.Postgres;

// The texts the live functions make themselves.  What they do with a server is in tests/DatabaseTests/Postgres.
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

    // PgBouncer in statement mode refuses a BEGIN with 08P01: the answer of "prepare" keeps the SQLSTATE and the tool's
    // own text in place of the pooler's.
    [Fact]
    public void RefusedTransaction_ProtocolViolation_IsTheAnswerOfARefusedTransaction() =>
        PostgresLive
            .RefusedTransaction(
                new PostgresException(
                    "transaction blocks not allowed in statement pooling mode",
                    "ERROR",
                    "ERROR",
                    "08P01"
                )
            )
            .ShouldBe(
                new PostgresPrepareAnswer(
                    null,
                    new PostgresServerError("08P01", PostgresLive.TransactionRefused, null, null, null)
                )
            );

    // Another error of the pooler has the same SQLSTATE, as a full pool's query_wait_timeout in transaction mode: it is
    // no refusal of the transaction, and is answered as any other server error.
    [Theory]
    [InlineData("57P01", "terminating connection")]
    [InlineData("08006", "terminating connection")]
    [InlineData("08P01", "query_wait_timeout")]
    [InlineData("08P01", "server login failed: wrong password type")]
    public void RefusedTransaction_OtherServerError_IsNull(string state, string message) =>
        PostgresLive.RefusedTransaction(new PostgresException(message, "FATAL", "FATAL", state)).ShouldBeNull();

    [Fact]
    public void RefusedTransaction_LostConnection_IsNull() =>
        PostgresLive
            .RefusedTransaction(new PostgresConnection.UnusableException(ConnectionTexts.LostConnection))
            .ShouldBeNull();

    // An NpgsqlException the call throws, whatever its cause, is the answer's error, made by errorOf.  AnswerAsync has
    // no cancellation filter: it catches by the exception's type alone, and a cancellation that is no NpgsqlException
    // passes through, as the next test pins.
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

    public static TheoryData<string> OwnTexts =>
        [
            ConnectionErrors.UnreadableConnectionString,
            ConnectionTexts.LostConnection,
            ConnectionErrors.MultiplexingRefused,
            ConnectionErrors.UnreadableFile,
            ConnectionErrors.UnusableFile,
            ConnectionErrors.UnsupportedSettings,
            ConnectionErrors.GssUnavailable,
            ConnectionErrors.UnreadableTypes,
        ];

    // The describer's own error passes as it is, whatever errorOf would make of it: at "open" and at a later call.
    [Theory]
    [MemberData(nameof(OwnTexts))]
    public async Task AnswerAsync_UnusableException_PassesItsTextUnchanged(string text)
    {
        var atOpen = await PostgresLive.AnswerAsync(
            () => throw new PostgresConnection.UnusableException(text),
            ConnectionErrors.AtOpen,
            static error => new PostgresDeallocateAnswer(error)
        );
        var later = await PostgresLive.AnswerAsync(
            () => throw new PostgresConnection.UnusableException(text),
            ConnectionErrors.Of,
            static error => new PostgresDeallocateAnswer(error)
        );

        atOpen.ShouldBe(new PostgresDeallocateAnswer(new PostgresServerError(null, text, null, null, null)));
        later.ShouldBe(new PostgresDeallocateAnswer(new PostgresServerError(null, text, null, null, null)));
    }

    // The driver's error is the driver's even when its message is one of the describer's own texts.
    [Fact]
    public async Task AnswerAsync_DriverErrorWithAnOwnText_IsAnsweredAsTheDrivers()
    {
        var atOpen = await PostgresLive.AnswerAsync(
            () => throw new NpgsqlException(ConnectionErrors.MultiplexingRefused),
            ConnectionErrors.AtOpen,
            static error => new PostgresDeallocateAnswer(error)
        );
        var later = await PostgresLive.AnswerAsync(
            () => throw new NpgsqlException(ConnectionErrors.MultiplexingRefused),
            ConnectionErrors.Of,
            static error => new PostgresDeallocateAnswer(error)
        );

        atOpen.Error!.Message.ShouldBe("Npgsql could not open a connection to the server (NpgsqlException)");
        later.Error!.Message.ShouldBe(ConnectionTexts.LostConnection);
    }

    // The exception keeps no inner exception, whose text may quote the value or a path.
    [Fact]
    public void UnusableException_InnerException_IsDropped() =>
        new PostgresConnection.UnusableException(
            ConnectionErrors.UnreadableFile,
            new IOException("/secret/path")
        ).InnerException.ShouldBeNull();

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
