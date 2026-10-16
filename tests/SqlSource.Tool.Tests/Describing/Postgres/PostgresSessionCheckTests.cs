using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Describing.Postgres;
using SqlSource.Tool.Logging;
using Xunit;
using static SqlSource.Tool.Tests.PostgresRequests;

namespace SqlSource.Tool.Tests.Describing.Postgres;

// The session over a replay the test writes: the Npgsql check and the bind names of the parameters.  A call the script
// does not hold is a CaptureMissException, so a describe that asks more than its test wrote fails.
public sealed class PostgresSessionCheckTests
{
    // The second describe saw another query: a column fewer.  The statement is freed all the same.
    [Fact]
    public async Task DescribeAsync_CheckWhoseColumnsDiffer_IsSqlsrc231SayingWhatDiffers()
    {
        var script = new PostgresScript().Open();
        _ = script.Prepares(1, "SELECT $1::int AS a, 'x' AS b", ["unknown"], new([23], null));
        _ = script.Columns(
            1,
            1,
            PostgresTables.ExpressionOf(0, "a", PostgresRows.Int4.Oid),
            PostgresTables.ExpressionOf(1, "b", PostgresRows.Text.Oid)
        );
        _ = script.Resolves([23, 25], PostgresRows.Int4, PostgresRows.Text);
        _ = script.Checks(
            "SELECT @p::int AS a, 'x' AS b",
            [("p", "integer")],
            PostgresTables.ExpressionOf(0, "a", PostgresRows.Int4.Oid)
        );
        var exchange = script.Frees(1).Play();
        await using var session = await PostgresScript.SessionAsync(exchange);

        var described = await session.DescribeAsync(
            Of("SELECT @p::int AS a, 'x' AS b;", Inferred("p")),
            TestContext.Current.CancellationToken
        );

        described.Value.ShouldBeNull();
        var failure = described.Failure.ShouldNotBeNull();
        failure.Descriptor.Id.ShouldBe("SQLSRC231");
        failure.Arguments.ShouldBe(["Q", "the result has 1 columns for Npgsql and 2 for SqlSource"]);
        failure.Step.ShouldBe(DescribeStep.CheckParameters);
        exchange.Unused.ShouldBeEmpty();
    }

    // The columns agree and Npgsql sent another text: the session compares it with its own rewrite of the request's
    // SQL, numbered as Npgsql numbered the parameters.
    [Fact]
    public async Task DescribeAsync_CheckWhoseTextDiffers_IsSqlsrc231SayingWhere()
    {
        var script = new PostgresScript().Open();
        _ = script.Prepares(1, "SELECT $1::int AS a, $2 AS b", ["unknown", "unknown"], new([23, 25], null));
        _ = script.Columns(
            1,
            2,
            PostgresTables.ExpressionOf(0, "a", PostgresRows.Int4.Oid),
            PostgresTables.ExpressionOf(1, "b", PostgresRows.Text.Oid)
        );
        _ = script.Resolves([23, 25], PostgresRows.Int4, PostgresRows.Text);
        _ = script.Answer(
            "check",
            new PostgresCheckRequest("SELECT @p::int AS a, @q AS b", [new("p", "integer"), new("q", "text")]),
            new PostgresCheckAnswer(
                [new("a", 23, "integer"), new("b", 25, "text")],
                null,
                null,
                new PostgresCheckSent("SELECT $2::int AS a, $2 AS b", ["q", "p"])
            )
        );
        var exchange = script.Frees(1).Play();
        await using var session = await PostgresScript.SessionAsync(exchange);

        var described = await session.DescribeAsync(
            Of("SELECT @p::int AS a, @q AS b", Inferred("p"), Inferred("q")),
            TestContext.Current.CancellationToken
        );

        var failure = described.Failure.ShouldNotBeNull();
        failure.Descriptor.Id.ShouldBe("SQLSRC231");
        failure.Arguments.ShouldBe([
            "Q",
            "the SQL with $n parts at character 22: '$2 AS b' for Npgsql and '$1 AS b' for SqlSource",
        ]);
        failure.Step.ShouldBe(DescribeStep.CheckParameters);
        failure.ServerLines.ShouldBeEmpty();
        failure.Help.ShouldBe(PostgresErrors.PlacementHelp);
        exchange.Unused.ShouldBeEmpty();
    }

    [Fact]
    public async Task DescribeAsync_CheckThatIsRefused_IsSqlsrc231WithTheLinesOfTheAnswer()
    {
        var refused = new PostgresServerError(
            null,
            "Specifying multiple SQL statements in a single NpgsqlBatchCommand isn't supported, "
                + "please remove all semicolons.",
            null,
            null,
            null
        );
        var script = new PostgresScript().Open();
        _ = script.PreparesWithNoColumns(1, "DELETE FROM samples WHERE id = $1", ["unknown"], new([23], null));
        _ = script.Resolves([23], PostgresRows.Int4);
        _ = script.Answer(
            "check",
            new PostgresCheckRequest("DELETE FROM samples WHERE id = @id", [new("id", "integer")]),
            new PostgresCheckAnswer(null, refused, null, null, true)
        );
        var exchange = script.Frees(1).Play();
        await using var session = await PostgresScript.SessionAsync(exchange);

        var described = await session.DescribeAsync(
            Of("DELETE FROM samples WHERE id = @id", Inferred("id")),
            TestContext.Current.CancellationToken
        );

        var failure = described.Failure.ShouldNotBeNull();
        failure.Descriptor.Id.ShouldBe("SQLSRC231");
        failure.Arguments.ShouldBe(["Q", refused.Message]);
        failure.ServerLines.ShouldBe([refused.Message]);
        exchange.Unused.ShouldBeEmpty();
    }

    // The server did not answer, which says nothing of how Npgsql reads the text: it is the server rejecting the query,
    // and the check event says error.  An answer recorded before textRefused existed is the same.
    [Theory]
    [InlineData(false)]
    [InlineData(null)]
    public async Task DescribeAsync_CheckThatTheServerCancels_IsSqlsrc225AtCheckParameters(bool? textRefused)
    {
        var canceled = new PostgresServerError(
            "57014",
            "canceling statement due to statement timeout",
            null,
            null,
            null
        );
        var script = new PostgresScript().Open();
        _ = script.PreparesWithNoColumns(1, "DELETE FROM samples WHERE id = $1", ["unknown"], new([23], null));
        _ = script.Resolves([23], PostgresRows.Int4);
        _ = script.Answer(
            "check",
            new PostgresCheckRequest("DELETE FROM samples WHERE id = @id", [new("id", "integer")]),
            new PostgresCheckAnswer(null, canceled, null, null, textRefused)
        );
        var exchange = script.Frees(1).Play();
        var log = new RecordingLog();
        await using var session = await PostgresScript.SessionAsync(exchange, log);

        var described = await session.DescribeAsync(
            Of("DELETE FROM samples WHERE id = @id", Inferred("id")),
            TestContext.Current.CancellationToken
        );

        var failure = described.Failure.ShouldNotBeNull();
        failure.Descriptor.Id.ShouldBe("SQLSRC225");
        failure.Arguments.ShouldBe(["Q", canceled.Message]);
        failure.Step.ShouldBe(DescribeStep.CheckParameters);
        failure.ServerLines.ShouldBe(["57014: canceling statement due to statement timeout"]);
        var (_, check) = log.Events.Single(written => written.Name == "check");
        check.Single(member => member.Name == "result").Value.ShouldBe((LogValue)NpgsqlCheck.Error);
        exchange.Unused.ShouldBeEmpty();
    }

    // The parameter is the second of the list, and the type is the name the describer spelled for it.
    [Fact]
    public async Task DescribeAsync_CheckWithAParameterNpgsqlCannotBind_IsSqlsrc232NamingIt()
    {
        var script = new PostgresScript().Open();
        _ = script.PreparesWithNoColumns(
            1,
            "DELETE FROM t WHERE a = $1 AND b = $2",
            ["unknown", "check_pair"],
            new([23, PostgresRows.CheckPair.Oid], null)
        );
        _ = script.Resolves([23, PostgresRows.CheckPair.Oid], PostgresRows.Int4, PostgresRows.CheckPair);
        _ = script.Answer(
            "check",
            new PostgresCheckRequest(
                "DELETE FROM t WHERE a = @a AND b = @b",
                [new("a", "integer"), new("b", "public.check_pair")]
            ),
            new PostgresCheckAnswer(null, null, new PostgresBindError("b", "Writing is not supported"))
        );
        var exchange = script.Frees(1).Play();
        await using var session = await PostgresScript.SessionAsync(exchange);

        var described = await session.DescribeAsync(
            Of("DELETE FROM t WHERE a = @a AND b = @b", Inferred("a"), Declared("b", "check_pair")),
            TestContext.Current.CancellationToken
        );

        var failure = described.Failure.ShouldNotBeNull();
        failure.Descriptor.Id.ShouldBe("SQLSRC232");
        failure.Arguments.ShouldBe(["public.check_pair", "@b", "Q"]);
        failure.ServerLines.ShouldBe(["Writing is not supported"]);
        failure.Help.ShouldBe(PostgresErrors.BindHelp);
        exchange.Unused.ShouldBeEmpty();
    }

    // The parameter's type keeps the server's quoted name and binds by the unquoted one; the column of the same type
    // has no name to bind by.
    [Fact]
    public async Task DescribeAsync_ParameterOfAQuotedType_HasItsBindNameAndChecksByIt()
    {
        const string Sql = "SELECT @s::\"My Schema\".\"Status\" AS s";
        var script = new PostgresScript().Open();
        _ = script.Prepares(
            1,
            "SELECT $1::\"My Schema\".\"Status\" AS s",
            ["unknown"],
            new([PostgresRows.MySchemaStatus.Oid], null)
        );
        _ = script.Columns(1, 1, PostgresTables.ExpressionOf(0, "s", PostgresRows.MySchemaStatus.Oid));
        _ = script.Resolves([PostgresRows.MySchemaStatus.Oid], PostgresRows.MySchemaStatus);
        _ = script.Checks(
            Sql,
            [("s", "My Schema.Status")],
            PostgresTables.ExpressionOf(0, "s", PostgresRows.MySchemaStatus.Oid)
        );
        _ = script.PlansNothing(1, 1);
        var exchange = script.Frees(1).Play();
        await using var session = await PostgresScript.SessionAsync(exchange);

        var described = await session.DescribeAsync(Of(Sql, Inferred("s")), TestContext.Current.CancellationToken);

        var description = described.Value.ShouldNotBeNull();
        var type = description.Parameters[0].Type.ShouldNotBeNull();
        type.Name.ShouldBe("\"My Schema\".\"Status\"");
        type.BindName.ShouldBe("My Schema.Status");
        description.Columns.ShouldNotBeNull()[0].Type.BindName.ShouldBeNull();
        exchange.Unused.ShouldBeEmpty();
    }

    // The type has no name that Npgsql could be given, so nothing is asked of "check".
    [Fact]
    public async Task DescribeAsync_ParameterTypeWithADotInItsName_IsSqlsrc232AndAsksNoCheck()
    {
        var script = new PostgresScript().Open();
        _ = script.PreparesWithNoColumns(
            1,
            "DELETE FROM t WHERE a = $1 AND s = $2",
            ["unknown", "unknown"],
            new([23, PostgresRows.WithDottedName.Oid], null)
        );
        _ = script.Resolves([23, PostgresRows.WithDottedName.Oid], PostgresRows.Int4, PostgresRows.WithDottedName);
        var exchange = script.Frees(1).Play();
        await using var session = await PostgresScript.SessionAsync(exchange);
        var request = Of("DELETE FROM t WHERE a = @a AND s = @s", Inferred("a"), Inferred("s"));

        var described = await session.DescribeAsync(request, TestContext.Current.CancellationToken);

        described.Failure.ShouldBe(PostgresErrors.Unnamed(request, 1, "public.\"x.y\""));
        exchange.Unused.ShouldBeEmpty();
    }
}
