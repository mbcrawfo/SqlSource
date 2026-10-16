using System;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Testing.Snapshot;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Describing.Postgres;
using SqlSource.Tool.Describing.Postgres.Walk;
using SqlSource.Tool.Logging;
using SqlSource.Tool.Testing;
using Xunit;
using static SqlSource.Tool.Testing.NullabilityAnswers;
using static SqlSource.Tool.Tests.PostgresPlans;
using static SqlSource.Tool.Tests.PostgresRequests;

namespace SqlSource.Tool.Tests.Describing.Postgres;

// The session over a replay the test writes: what it asks, in which order, and what it makes of each answer.  A
// call the script does not hold is a CaptureMissException, so a describe that asks more than its test wrote fails.
public class PostgresSessionTests
{
    [Theory]
    [InlineData("18.4", 18)]
    [InlineData("13.12", 13)]
    [InlineData("9.6.24", 9)]
    [InlineData("18beta1", 18)]
    [InlineData("devel", null)]
    public void MajorVersionOf_VersionAsRecorded_IsItsLeadingDigits(string version, int? expected) =>
        PostgresSession.MajorVersionOf(version).ShouldBe(expected);

    // The script holds "open" alone: the server is refused before any query is described.
    [Theory]
    [InlineData("13.12")]
    [InlineData("9.6.24")]
    public async Task OpenAsync_ServerOlderThanTheFloor_IsSqlsrc235(string version)
    {
        var exchange = new PostgresScript()
            .Answer("open", PostgresOpenRequest.Instance, PostgresScript.Opened with { ServerVersion = version })
            .Play();

        var opened = await PostgresScript.OpenAsync(exchange);

        opened.Value.ShouldBeNull();
        var failure = opened.Failure.ShouldNotBeNull();
        failure.Descriptor.Id.ShouldBe("SQLSRC235");
        failure.Arguments.ShouldBe([PostgresScript.Database, version, "14"]);
        failure.Step.ShouldBeNull();
        failure.ServerLines.ShouldBeEmpty();
    }

    // A version with no leading digits is not refused: the tool cannot tell what it is.
    [Theory]
    [InlineData("14.0")]
    [InlineData("18beta1")]
    [InlineData("devel")]
    public async Task OpenAsync_ServerAtOrAboveTheFloor_Opens(string version)
    {
        var exchange = new PostgresScript()
            .Answer("open", PostgresOpenRequest.Instance, PostgresScript.Opened with { ServerVersion = version })
            .Play();

        await using var session = await PostgresScript.SessionAsync(exchange);

        session.Server.Version.ShouldBe(version);
    }

    // The script holds "open" alone: nothing may be asked for a text of two statements.
    [Theory]
    [InlineData("DELETE FROM users WHERE id = @id; DELETE FROM orders")]
    [InlineData("SELECT 1;;")]
    public async Task DescribeAsync_SeveralStatements_IsSqlsrc230AndAsksNothing(string sql)
    {
        await using var session = await PostgresScript.SessionAsync(new PostgresScript().Open().Play());

        var described = await session.DescribeAsync(Of(sql, Inferred("id")), TestContext.Current.CancellationToken);

        described.Value.ShouldBeNull();
        described.Failure.ShouldNotBeNull().Descriptor.Id.ShouldBe("SQLSRC230");
    }

    // The script holds "open" alone: a statement that PREPARE does not take is refused before anything is asked.
    [Fact]
    public async Task DescribeAsync_CallStatement_IsSqlsrc233AndAsksNothing()
    {
        await using var session = await PostgresScript.SessionAsync(new PostgresScript().Open().Play());

        var described = await session.DescribeAsync(
            Of("CALL archive(@id)", Inferred("id")),
            TestContext.Current.CancellationToken
        );

        described.Value.ShouldBeNull();
        var failure = described.Failure.ShouldNotBeNull();
        failure.Descriptor.Id.ShouldBe("SQLSRC233");
        failure.Arguments.ShouldBe(["Q", "CALL"]);
    }

    // The script holds "open" alone: a "$n" that the SQL writes is refused before anything is asked.
    [Fact]
    public async Task DescribeAsync_PositionalParameterInTheSql_IsSqlsrc234AndAsksNothing()
    {
        await using var session = await PostgresScript.SessionAsync(new PostgresScript().Open().Play());

        var described = await session.DescribeAsync(
            Of("SELECT 1 FROM users WHERE id = @id AND name = $2", Inferred("id")),
            TestContext.Current.CancellationToken
        );

        described.Value.ShouldBeNull();
        var failure = described.Failure.ShouldNotBeNull();
        failure.Descriptor.Id.ShouldBe("SQLSRC234");
        failure.Arguments.ShouldBe(["Q", "$2"]);
    }

    // The statements are counted before the kind is read.
    [Fact]
    public async Task DescribeAsync_SeveralStatementsThatStartWithCall_IsSqlsrc230()
    {
        await using var session = await PostgresScript.SessionAsync(new PostgresScript().Open().Play());

        var described = await session.DescribeAsync(Of("CALL a(); CALL b()"), TestContext.Current.CancellationToken);

        described.Failure.ShouldNotBeNull().Descriptor.Id.ShouldBe("SQLSRC230");
    }

    [Fact]
    public async Task DescribeAsync_DeclaredTypeThatIsNoTypeName_IsSqlsrc228AndAsksNothing()
    {
        await using var session = await PostgresScript.SessionAsync(new PostgresScript().Open().Play());

        var described = await session.DescribeAsync(
            Of("SELECT 1 FROM users WHERE id = @id", Declared("id", "int) AS SELECT 1; DROP TABLE users; --")),
            TestContext.Current.CancellationToken
        );

        var failure = described.Failure.ShouldNotBeNull();
        failure.Descriptor.Id.ShouldBe("SQLSRC228");
        failure.Arguments[1].ShouldBe("@id");
        failure.Arguments[3].ShouldBe(PostgresErrors.NotATypeName);
    }

    // The statements are counted before the types are read: the first thing wrong is the first reported.
    [Fact]
    public async Task DescribeAsync_SeveralStatementsAndABadType_IsSqlsrc230()
    {
        await using var session = await PostgresScript.SessionAsync(new PostgresScript().Open().Play());

        var described = await session.DescribeAsync(
            Of("SELECT @id; SELECT 2", Declared("id", "int foo")),
            TestContext.Current.CancellationToken
        );

        described.Failure.ShouldNotBeNull().Descriptor.Id.ShouldBe("SQLSRC230");
    }

    // The trailing ";" is not sent, and the statement is freed when the describe is done.
    [Fact]
    public async Task DescribeAsync_Query_AsksPrepareThenDeallocateForTheStatement()
    {
        var script = new PostgresScript().Open();
        _ = script.PreparesWithNoColumns(1, "DELETE FROM samples WHERE flag", [], new PostgresPrepareAnswer([], null));
        _ = script.Checks("DELETE FROM samples WHERE flag", []);
        var exchange = script.Frees(1).Play();
        await using var session = await PostgresScript.SessionAsync(exchange);

        var described = await session.DescribeAsync(
            Of("DELETE FROM samples WHERE flag;"),
            TestContext.Current.CancellationToken
        );

        _ = described.Value.ShouldNotBeNull();
        exchange.Unused.ShouldBeEmpty();
    }

    // A name is a counter within its session, so that a capture is the same each time it is recorded.
    [Fact]
    public async Task DescribeAsync_SecondQuery_TakesTheNextName()
    {
        var script = new PostgresScript().Open();
        _ = script.PreparesWithNoColumns(1, "DELETE FROM samples", [], new PostgresPrepareAnswer([], null));
        _ = script.Checks("DELETE FROM samples", []);
        _ = script.Frees(1);
        _ = script.PreparesWithNoColumns(2, "DELETE FROM orders", [], new PostgresPrepareAnswer([], null));
        _ = script.Checks("DELETE FROM orders", []);
        var exchange = script.Frees(2).Play();
        await using var session = await PostgresScript.SessionAsync(exchange);

        _ = await session.DescribeAsync(Of("DELETE FROM samples"), TestContext.Current.CancellationToken);
        var second = await session.DescribeAsync(Of("DELETE FROM orders"), TestContext.Current.CancellationToken);

        _ = second.Value.ShouldNotBeNull();
        exchange.Unused.ShouldBeEmpty();
    }

    // There is no statement to free, so "deallocate" is not asked: the script holds none for the first name.  The
    // counter advances all the same.
    [Fact]
    public async Task DescribeAsync_PrepareThatFails_IsTheFailureFreesNothingAndTheNextTakesTheNextName()
    {
        // "PREPARE sqlsource_1 AS " is 23 characters, and nope is the 13th of the SQL.
        var rejected = new PostgresServerError("42P01", "relation \"nope\" does not exist", null, null, 36);
        var script = new PostgresScript().Open();
        _ = script.PreparesWithNoColumns(1, "DELETE FROM nope", [], new PostgresPrepareAnswer(null, rejected));
        _ = script.PreparesWithNoColumns(2, "DELETE FROM orders", [], new PostgresPrepareAnswer([], null));
        _ = script.Checks("DELETE FROM orders", []);
        var exchange = script.Frees(2).Play();
        await using var session = await PostgresScript.SessionAsync(exchange);

        var first = await session.DescribeAsync(Of("DELETE FROM nope"), TestContext.Current.CancellationToken);
        var second = await session.DescribeAsync(Of("DELETE FROM orders"), TestContext.Current.CancellationToken);

        var failure = first.Failure.ShouldNotBeNull();
        failure.Descriptor.Id.ShouldBe("SQLSRC225");
        failure.Arguments.ShouldBe(["Q", "relation \"nope\" does not exist"]);
        failure.ServerLines.ShouldBe(["42P01: relation \"nope\" does not exist", "at character 13"]);
        _ = second.Value.ShouldNotBeNull();
        exchange.Unused.ShouldBeEmpty();
    }

    // The error of "deallocate" is logged and never reported: not even 26000, which anywhere else is SQLSRC236.
    [Fact]
    public async Task DescribeAsync_DeallocateAnswers26000_IsDescribed()
    {
        var script = new PostgresScript().Open();
        _ = script.PreparesWithNoColumns(1, "DELETE FROM samples", [], new PostgresPrepareAnswer([], null));
        var gone = new PostgresServerError(
            "26000",
            "prepared statement \"sqlsource_1\" does not exist",
            null,
            null,
            null
        );
        _ = script.Checks("DELETE FROM samples", []);
        var exchange = script.Frees(1, new PostgresDeallocateAnswer(gone)).Play();
        var log = new RecordingLog();
        await using var session = await PostgresScript.SessionAsync(exchange, log);

        var described = await session.DescribeAsync(Of("DELETE FROM samples"), TestContext.Current.CancellationToken);

        _ = described.Value.ShouldNotBeNull();
        described.Failure.ShouldBeNull();
        var (_, members) = log.Events.Last(written => written.Name == "deallocate");
        var error = members.Single(member => member.Name == "error").Value.ShouldBeOfType<LogValue.Members>();
        error.Values[0].ShouldBe(new LogMember("sqlState", "26000"));
    }

    // 42P05 at "prepare": a backend that a past run left a statement on.  The statement is not ours to free, so
    // "deallocate" is not asked: the script holds none.
    [Fact]
    public async Task DescribeAsync_PrepareAnswers42P05_IsSqlsrc236AndFreesNothing()
    {
        var exists = new PostgresServerError(
            "42P05",
            "prepared statement \"sqlsource_1\" already exists",
            null,
            null,
            null
        );
        var exchange = new PostgresScript()
            .Open()
            .Prepares(1, "DELETE FROM samples", [], new PostgresPrepareAnswer(null, exists))
            .Play();
        await using var session = await PostgresScript.SessionAsync(exchange);

        var described = await session.DescribeAsync(Of("DELETE FROM samples"), TestContext.Current.CancellationToken);

        var failure = described.Failure.ShouldNotBeNull();
        failure.Descriptor.Id.ShouldBe("SQLSRC236");
        failure.Arguments.ShouldBe([PostgresScript.Database]);
        failure.Step.ShouldBe(DescribeStep.DescribeParameters);
        exchange.Unused.ShouldBeEmpty();
    }

    // The statement that was prepared is not listed: PostgresLive answers the fixed message, with no SQLSTATE.
    [Fact]
    public async Task DescribeAsync_PrepareAnswersStatementNotListed_IsSqlsrc236()
    {
        var unlisted = new PostgresServerError(null, PostgresLive.StatementNotListed, null, null, null);
        var exchange = new PostgresScript()
            .Open()
            .Prepares(1, "DELETE FROM samples", [], new PostgresPrepareAnswer(null, unlisted))
            .Play();
        await using var session = await PostgresScript.SessionAsync(exchange);

        var described = await session.DescribeAsync(Of("DELETE FROM samples"), TestContext.Current.CancellationToken);

        var failure = described.Failure.ShouldNotBeNull();
        failure.Descriptor.Id.ShouldBe("SQLSRC236");
        failure.ServerLines.ShouldBe([PostgresLive.StatementNotListed]);
        exchange.Unused.ShouldBeEmpty();
    }

    // The statement went with the backend that held it: the free is still asked.
    [Fact]
    public async Task DescribeAsync_ColumnsAnswers26000_IsSqlsrc236AtDescribeColumns()
    {
        var gone = new PostgresServerError(
            "26000",
            "prepared statement \"sqlsource_1\" does not exist",
            null,
            null,
            null
        );
        var script = new PostgresScript().Open();
        _ = script.Prepares(1, "DELETE FROM samples", [], new PostgresPrepareAnswer([], null));
        _ = script.Answer(
            "columns",
            new PostgresColumnsRequest("sqlsource_1", 0),
            new PostgresColumnsAnswer(null, gone)
        );
        var exchange = script.Frees(1).Play();
        await using var session = await PostgresScript.SessionAsync(exchange);

        var described = await session.DescribeAsync(Of("DELETE FROM samples"), TestContext.Current.CancellationToken);

        var failure = described.Failure.ShouldNotBeNull();
        failure.Descriptor.Id.ShouldBe("SQLSRC236");
        failure.Arguments.ShouldBe([PostgresScript.Database]);
        failure.Step.ShouldBe(DescribeStep.DescribeColumns);
        exchange.Unused.ShouldBeEmpty();
    }

    // "explain" is the one call after "check" that names the statement and reports, so it finds out too.  A session
    // that lost its statement is no reason for the heuristic: the describe fails, and the statement is freed.
    [Fact]
    public async Task DescribeAsync_ExplainAnswers26000_IsSqlsrc236AtExplain()
    {
        var gone = new PostgresServerError(
            "26000",
            "prepared statement \"sqlsource_1\" does not exist",
            null,
            null,
            null
        );
        var users = PostgresTables.Users;
        var script = new PostgresScript().Open();
        _ = script.Prepares(1, GatedUsersId, [], new([], null));
        _ = script.Columns(1, 0, PostgresTables.UsersId, PostgresTables.ExpressionOf(1, "n", PostgresRows.Int8.Oid));
        _ = script.Tables([users.Oid], users);
        _ = script.Resolves([20, 23], PostgresRows.Int8, PostgresRows.Int4);
        _ = script.Checks(
            GatedUsersId,
            [],
            PostgresTables.ExpressionOf(0, "id", PostgresRows.Int4.Oid),
            PostgresTables.ExpressionOf(1, "n", PostgresRows.Int8.Oid)
        );
        _ = script.Explains(1, 0, new PostgresExplainAnswer(null, gone));
        var exchange = script.Frees(1).Play();
        await using var session = await PostgresScript.SessionAsync(exchange);

        var described = await session.DescribeAsync(Of(GatedUsersId), TestContext.Current.CancellationToken);

        described.Value.ShouldBeNull();
        var failure = described.Failure.ShouldNotBeNull();
        failure.Descriptor.Id.ShouldBe("SQLSRC236");
        failure.Arguments.ShouldBe([PostgresScript.Database]);
        failure.Step.ShouldBe(DescribeStep.Explain);
        failure.ServerLines.ShouldBe(["26000: prepared statement \"sqlsource_1\" does not exist"]);
        exchange.Unused.ShouldBeEmpty();
    }

    // A query planned for its expression alone finds out the same way.
    [Fact]
    public async Task DescribeAsync_UngatedQueryWithAnExpressionExplainAnswers26000_IsSqlsrc236AtExplain()
    {
        var gone = new PostgresServerError(
            "26000",
            "prepared statement \"sqlsource_1\" does not exist",
            null,
            null,
            null
        );
        var users = PostgresTables.Users;
        var script = new PostgresScript().Open();
        _ = script.Prepares(1, UngatedUsersId, [], new([], null));
        _ = script.Columns(1, 0, PostgresTables.UsersId, PostgresTables.ExpressionOf(1, "n", PostgresRows.Int8.Oid));
        _ = script.Tables([users.Oid], users);
        _ = script.Resolves([20, 23], PostgresRows.Int8, PostgresRows.Int4);
        _ = script.Checks(
            UngatedUsersId,
            [],
            PostgresTables.ExpressionOf(0, "id", PostgresRows.Int4.Oid),
            PostgresTables.ExpressionOf(1, "n", PostgresRows.Int8.Oid)
        );
        _ = script.Explains(1, 0, new PostgresExplainAnswer(null, gone));
        var exchange = script.Frees(1).Play();
        await using var session = await PostgresScript.SessionAsync(exchange);

        var described = await session.DescribeAsync(Of(UngatedUsersId), TestContext.Current.CancellationToken);

        var failure = described.Failure.ShouldNotBeNull();
        failure.Descriptor.Id.ShouldBe("SQLSRC236");
        failure.Step.ShouldBe(DescribeStep.Explain);
        exchange.Unused.ShouldBeEmpty();
    }

    // The declared type goes into the list as written, and the rest is "unknown".
    [Fact]
    public async Task DescribeAsync_DeclaredAndInferredParameters_AskWithTheTypeListOfTheQuery()
    {
        var rejected = new PostgresServerError("42704", "type \"varchar\" does not exist", null, null, 31);
        var script = new PostgresScript().Open();
        var exchange = script
            .PreparesWithNoColumns(
                1,
                "SELECT 1 FROM users WHERE id = $1 AND name = $2",
                ["unknown", "varchar(50)", "nope_t"],
                new PostgresPrepareAnswer(null, rejected)
            )
            .Play();
        await using var session = await PostgresScript.SessionAsync(exchange);

        var described = await session.DescribeAsync(
            Of(
                "SELECT 1 FROM users WHERE id = @id AND name = @name",
                Inferred("id"),
                Declared("name", "varchar(50)"),
                Declared("tenant", "nope_t")
            ),
            TestContext.Current.CancellationToken
        );

        var failure = described.Failure.ShouldNotBeNull();
        failure.Descriptor.Id.ShouldBe("SQLSRC228");
        failure.Arguments.ShouldBe(["varchar(50)", "@name", "Q", PostgresErrors.NoSuchType]);
        exchange.Unused.ShouldBeEmpty();
    }

    [Fact]
    public async Task DescribeAsync_InferredAndDeclaredParameters_AreTypedInTheListsOrder()
    {
        var script = new PostgresScript().Open();
        _ = script.PreparesWithNoColumns(
            1,
            "SELECT id FROM users WHERE id = $1 AND name = $2",
            ["unknown", "varchar(50)"],
            new PostgresPrepareAnswer([23, 1043], null)
        );
        _ = script.Resolves([23, 1043], PostgresRows.Int4, PostgresRows.Varchar);
        _ = script.Checks(
            "SELECT id FROM users WHERE id = @id AND name = @name",
            [("id", "integer"), ("name", "character varying")]
        );
        var exchange = script.Frees(1).Play();
        await using var session = await PostgresScript.SessionAsync(exchange);

        var described = await session.DescribeAsync(
            Of(
                "SELECT id FROM users WHERE id = @id AND name = @name;",
                Inferred("id"),
                Declared("name", "varchar(50)")
            ),
            TestContext.Current.CancellationToken
        );

        described
            .Value.ShouldNotBeNull()
            .ShouldBe(
                new QueryDescription(
                    SidecarResultKind.None,
                    TestSidecars.Of(
                        new DescribedParameter(
                            "id",
                            TestSidecars.Postgres(bindName: "integer"),
                            SidecarValues.TypeSource.Inferred
                        ),
                        new DescribedParameter(
                            "name",
                            TestSidecars.Postgres(
                                "character varying",
                                internalName: "varchar",
                                bindName: "character varying"
                            ),
                            SidecarValues.TypeSource.Declared
                        )
                    ),
                    null,
                    null,
                    null,
                    null
                )
            );
        exchange.Unused.ShouldBeEmpty();
    }

    // The request of "types" is sorted and holds each OID once, so that it is the same however the query orders
    // its parameters.  A declared parameter that the SQL does not hold is typed as it was declared.
    [Fact]
    public async Task DescribeAsync_ParametersOfOneTypeAndADeclaredOnlyOne_AskForEachOidOnce()
    {
        var script = new PostgresScript().Open();
        _ = script.PreparesWithNoColumns(
            1,
            "UPDATE users SET name = $1 WHERE id = $2",
            ["unknown", "unknown", "int"],
            new PostgresPrepareAnswer([1043, 23, 23], null)
        );
        _ = script.Resolves([23, 1043], PostgresRows.Int4, PostgresRows.Varchar);
        _ = script.Checks(
            "UPDATE users SET name = @name WHERE id = @id",
            [("name", "character varying"), ("id", "integer"), ("tenant", "integer")]
        );
        var exchange = script.Frees(1).Play();
        await using var session = await PostgresScript.SessionAsync(exchange);

        var described = await session.DescribeAsync(
            Of(
                "UPDATE users SET name = @name WHERE id = @id",
                Inferred("name"),
                Inferred("id"),
                Declared("tenant", "int")
            ),
            TestContext.Current.CancellationToken
        );

        var parameters = described.Value.ShouldNotBeNull().Parameters;
        parameters.Select(parameter => parameter.Name).ShouldBe(["name", "id", "tenant"]);
        parameters.Select(parameter => parameter.TypeSource).ShouldBe(["inferred", "inferred", "declared"]);
        parameters[2].Type.ShouldBe(TestSidecars.Postgres(bindName: "integer"));
        exchange.Unused.ShouldBeEmpty();
    }

    // The SQL holds no "$n" of its own, so the server resolves no more types than the list: a text that the tool's
    // lexer and the server read differently could still make it list more.  The extra type is left out of what is
    // asked and described.
    [Fact]
    public async Task DescribeAsync_MoreTypesThanParameters_AsksAndDescribesTheListsTypesAlone()
    {
        var script = new PostgresScript().Open();
        _ = script.PreparesWithNoColumns(
            1,
            "DELETE FROM samples",
            ["int"],
            new PostgresPrepareAnswer([23, 1043], null)
        );
        _ = script.Resolves([23], PostgresRows.Int4);
        _ = script.Checks("DELETE FROM samples", [("tenant", "integer")]);
        var exchange = script.Frees(1).Play();
        await using var session = await PostgresScript.SessionAsync(exchange);

        var described = await session.DescribeAsync(
            Of("DELETE FROM samples", Declared("tenant", "int")),
            TestContext.Current.CancellationToken
        );

        var parameters = described.Value.ShouldNotBeNull().Parameters;
        parameters.Select(parameter => parameter.Name).ShouldBe(["tenant"]);
        exchange.Unused.ShouldBeEmpty();
    }

    // A bug of the describer or of the server, and the statement is freed all the same.
    [Fact]
    public async Task DescribeAsync_FewerTypesThanParameters_ThrowsAndFreesTheStatement()
    {
        var script = new PostgresScript().Open();
        _ = script.PreparesWithNoColumns(
            1,
            "SELECT $1, $2",
            ["unknown", "unknown"],
            new PostgresPrepareAnswer([23], null)
        );
        var exchange = script.Frees(1).Play();
        await using var session = await PostgresScript.SessionAsync(exchange);

        _ = await Should.ThrowAsync<InvalidOperationException>(() =>
            session.DescribeAsync(
                Of("SELECT @a, @b", Inferred("a"), Inferred("b")),
                TestContext.Current.CancellationToken
            )
        );

        exchange.Unused.ShouldBeEmpty();
    }

    [Fact]
    public async Task DescribeAsync_InferredPseudoType_IsSqlsrc226NamingIt()
    {
        var script = new PostgresScript().Open();
        _ = script.PreparesWithNoColumns(
            1,
            "SELECT row_to_json($1)",
            ["unknown"],
            new PostgresPrepareAnswer([2249], null)
        );
        _ = script.Resolves([2249], PostgresRows.Record);
        var exchange = script.Frees(1).Play();
        await using var session = await PostgresScript.SessionAsync(exchange);

        var described = await session.DescribeAsync(
            Of("SELECT row_to_json(@p)", Inferred("p")),
            TestContext.Current.CancellationToken
        );

        var failure = described.Failure.ShouldNotBeNull();
        failure.Descriptor.Id.ShouldBe("SQLSRC226");
        failure.Arguments.ShouldBe(["@p", "Q"]);
        failure.Step.ShouldBe(DescribeStep.ResolveType);
        failure.Help.ShouldNotBeNull().ShouldContain("'record'");
        exchange.Unused.ShouldBeEmpty();
    }

    [Fact]
    public async Task DescribeAsync_DeclaredPseudoType_IsSqlsrc228()
    {
        var script = new PostgresScript().Open();
        _ = script.PreparesWithNoColumns(1, "SELECT $1", ["record"], new PostgresPrepareAnswer([2249], null));
        _ = script.Resolves([2249], PostgresRows.Record);
        var exchange = script.Frees(1).Play();
        await using var session = await PostgresScript.SessionAsync(exchange);

        var described = await session.DescribeAsync(
            Of("SELECT @p", Declared("p", "record")),
            TestContext.Current.CancellationToken
        );

        var failure = described.Failure.ShouldNotBeNull();
        failure.Descriptor.Id.ShouldBe("SQLSRC228");
        failure.Arguments.ShouldBe(["record", "@p", "Q", PostgresErrors.PseudoTypeReason]);
        exchange.Unused.ShouldBeEmpty();
    }

    // A connection that dropped between the two calls.  The statement is still asked to be freed.
    [Fact]
    public async Task DescribeAsync_TypesThatFails_IsSqlsrc225AtResolveType()
    {
        var dropped = new PostgresServerError(null, "Exception while reading from stream", null, null, null);
        var script = new PostgresScript().Open();
        _ = script.PreparesWithNoColumns(1, "SELECT $1::int", ["unknown"], new PostgresPrepareAnswer([23], null));
        _ = script.Answer("types", new PostgresTypesRequest([23]), new PostgresTypesAnswer(null, dropped));
        var exchange = script.Frees(1, new PostgresDeallocateAnswer(dropped)).Play();
        await using var session = await PostgresScript.SessionAsync(exchange);

        var described = await session.DescribeAsync(
            Of("SELECT @p::int", Inferred("p")),
            TestContext.Current.CancellationToken
        );

        var failure = described.Failure.ShouldNotBeNull();
        failure.Descriptor.Id.ShouldBe("SQLSRC225");
        failure.Step.ShouldBe(DescribeStep.ResolveType);
        exchange.Unused.ShouldBeEmpty();
    }

    [Fact]
    public async Task DescribeAsync_StatementWithColumns_IsRowsWithEachColumnsTypeAndFacets()
    {
        var script = new PostgresScript().Open();
        _ = script.Prepares(1, "SELECT 1, 'a'::varchar(100), 2::numeric(18,2)", [], new([], null));
        _ = script.Columns(
            1,
            0,
            PostgresTables.ExpressionOf(0, "?column?", PostgresRows.Int4.Oid),
            PostgresTables.ExpressionOf(1, "varchar", PostgresRows.Varchar.Oid, length: 100),
            PostgresTables.ExpressionOf(2, "numeric", PostgresRows.Numeric.Oid, precision: 18, scale: 2)
        );
        _ = script.Resolves([23, 1043, 1700], PostgresRows.Int4, PostgresRows.Varchar, PostgresRows.Numeric);
        _ = script.Checks(
            "SELECT 1, 'a'::varchar(100), 2::numeric(18,2)",
            [],
            PostgresTables.ExpressionOf(0, "?column?", PostgresRows.Int4.Oid),
            PostgresTables.ExpressionOf(1, "varchar", PostgresRows.Varchar.Oid),
            PostgresTables.ExpressionOf(2, "numeric", PostgresRows.Numeric.Oid)
        );
        _ = script.Explains(
            1,
            0,
            PostgresScript.PlanOf(
                Scan("x") with
                {
                    NodeType = "Result",
                    Alias = null,
                    Output = ["1", "'a'::character varying(100)", "2.00"],
                }
            )
        );
        var exchange = script.Frees(1).Play();
        await using var session = await PostgresScript.SessionAsync(exchange);

        var described = await session.DescribeAsync(
            Of("SELECT 1, 'a'::varchar(100), 2::numeric(18,2)"),
            TestContext.Current.CancellationToken
        );

        var description = described.Value.ShouldNotBeNull();
        description.ResultKind.ShouldBe(SidecarResultKind.Rows);
        description.MatchesTable.ShouldBeNull();
        description.Plan.ShouldBe(SidecarValues.Plan.Walked);
        description.TableMatch.ShouldBe(SidecarValues.TableMatch.NoOrigin);
        description
            .Columns.ShouldNotBeNull()
            .Select(column => (column.Ordinal, column.Name, column.Type.Name))
            .ShouldBe([
                (0, "?column?", "integer"),
                (1, "varchar", "character varying(100)"),
                (2, "numeric", "numeric(18,2)"),
            ]);
        exchange.Unused.ShouldBeEmpty();
    }

    // A statement without rows has no columns, no table and no provenance.
    [Fact]
    public async Task DescribeAsync_StatementWithoutColumns_IsNoneWithNoProvenance()
    {
        var script = new PostgresScript().Open();
        _ = script.PreparesWithNoColumns(1, "DELETE FROM samples WHERE id = $1", ["unknown"], new([23], null));
        _ = script.Resolves([23], PostgresRows.Int4);
        _ = script.Checks("DELETE FROM samples WHERE id = @id", [("id", "integer")]);
        var exchange = script.Frees(1).Play();
        await using var session = await PostgresScript.SessionAsync(exchange);

        var described = await session.DescribeAsync(
            Of("DELETE FROM samples WHERE id = @id", Inferred("id")),
            TestContext.Current.CancellationToken
        );

        var description = described.Value.ShouldNotBeNull();
        description.ResultKind.ShouldBe(SidecarResultKind.None);
        description.Columns.ShouldBeNull();
        description.MatchesTable.ShouldBeNull();
        description.Plan.ShouldBeNull();
        description.TableMatch.ShouldBeNull();
        exchange.Unused.ShouldBeEmpty();
    }

    // "types" is asked once, for the parameters and the columns together, sorted and each OID once.
    [Fact]
    public async Task DescribeAsync_ParametersAndColumns_AskTypesOnceForBoth()
    {
        var script = new PostgresScript().Open();
        _ = script.Prepares(1, "SELECT $1::text AS a, 1 AS b, 2 AS c", ["unknown"], new([25], null));
        _ = script.Columns(
            1,
            1,
            PostgresTables.ExpressionOf(0, "a", PostgresRows.Text.Oid),
            PostgresTables.ExpressionOf(1, "b", PostgresRows.Int4.Oid),
            PostgresTables.ExpressionOf(2, "c", PostgresRows.Int4.Oid)
        );
        _ = script.Resolves([23, 25], PostgresRows.Int4, PostgresRows.Text);
        _ = script.Checks(
            "SELECT @p::text AS a, 1 AS b, 2 AS c",
            [("p", "text")],
            PostgresTables.ExpressionOf(0, "a", PostgresRows.Text.Oid),
            PostgresTables.ExpressionOf(1, "b", PostgresRows.Int4.Oid),
            PostgresTables.ExpressionOf(2, "c", PostgresRows.Int4.Oid)
        );
        _ = script.PlansNothing(1, 1);
        var exchange = script.Frees(1).Play();
        await using var session = await PostgresScript.SessionAsync(exchange);

        var described = await session.DescribeAsync(
            Of("SELECT @p::text AS a, 1 AS b, 2 AS c", Inferred("p")),
            TestContext.Current.CancellationToken
        );

        var description = described.Value.ShouldNotBeNull();
        description
            .Parameters.ShouldHaveSingleItem()
            .Type.ShouldBe(TestSidecars.Postgres("text", internalName: "text", bindName: "text"));
        description
            .Columns.ShouldNotBeNull()
            .Select(column => column.Type.Name)
            .ShouldBe(["text", "integer", "integer"]);
        exchange.Unused.ShouldBeEmpty();
    }

    // The position the server gives is in the tool's own EXECUTE, so there is no "at character" line.
    [Fact]
    public async Task DescribeAsync_ColumnsThatFails_IsSqlsrc225AtDescribeColumnsAndFreesTheStatement()
    {
        var dropped = new PostgresServerError(
            "57P01",
            "terminating connection due to administrator command",
            null,
            null,
            9
        );
        var script = new PostgresScript().Open();
        _ = script.Prepares(1, "SELECT 1", [], new([], null));
        _ = script.Answer(
            "columns",
            new PostgresColumnsRequest("sqlsource_1", 0),
            new PostgresColumnsAnswer(null, dropped)
        );
        var exchange = script.Frees(1).Play();
        await using var session = await PostgresScript.SessionAsync(exchange);

        var described = await session.DescribeAsync(Of("SELECT 1"), TestContext.Current.CancellationToken);

        var failure = described.Failure.ShouldNotBeNull();
        failure.Descriptor.Id.ShouldBe("SQLSRC225");
        failure.Step.ShouldBe(DescribeStep.DescribeColumns);
        failure.ServerLines.ShouldBe(["57P01: terminating connection due to administrator command"]);
        exchange.Unused.ShouldBeEmpty();
    }

    // "tables" is asked once, for each table once and sorted, whatever the order of the columns.  "types" is then
    // asked for the types of the origin attributes too, which is where a domain is found.
    [Fact]
    public async Task DescribeAsync_ColumnsOfTables_AskTablesOnceAndTypesForTheOriginsToo()
    {
        var users = PostgresTables.Users;
        var orders = PostgresTables.Orders;
        var script = new PostgresScript().Open();
        _ = script.Prepares(1, "SELECT o.total, u.email, u.id FROM orders o, users u", [], new([], null));
        _ = script.Columns(
            1,
            0,
            PostgresTables.ColumnOf(0, "total", PostgresRows.Numeric.Oid, orders, 3, precision: 18, scale: 2),
            PostgresTables.ColumnOf(1, "email", PostgresRows.Varchar.Oid, users, 3, length: 254),
            PostgresTables.ColumnOf(2, "id", PostgresRows.Int4.Oid, users, 1)
        );
        _ = script.Tables([users.Oid, orders.Oid], users, orders);
        _ = script.Resolves(
            [23, 1043, 1700, PostgresRows.EmailT.Oid],
            PostgresRows.Int4,
            PostgresRows.Varchar,
            PostgresRows.Numeric,
            PostgresRows.EmailT
        );
        _ = script.Checks(
            "SELECT o.total, u.email, u.id FROM orders o, users u",
            [],
            PostgresTables.ExpressionOf(0, "total", PostgresRows.Numeric.Oid),
            PostgresTables.ExpressionOf(1, "email", PostgresRows.Varchar.Oid),
            PostgresTables.ExpressionOf(2, "id", PostgresRows.Int4.Oid)
        );
        var exchange = script.Frees(1).Play();
        await using var session = await PostgresScript.SessionAsync(exchange);

        var described = await session.DescribeAsync(
            Of("SELECT o.total, u.email, u.id FROM orders o, users u"),
            TestContext.Current.CancellationToken
        );

        var columns = described.Value.ShouldNotBeNull().Columns.ShouldNotBeNull();
        columns.Select(column => column.Type.Name).ShouldBe(["numeric(18,2)", "public.email_t", "integer"]);
        columns
            .Select(column => column.Origin)
            .ShouldBe([
                new SidecarOrigin("public", "orders", "total"),
                new SidecarOrigin("public", "users", "email"),
                new SidecarOrigin("public", "users", "id"),
            ]);
        columns.Select(column => column.Identity).ShouldBe([false, false, true]);
        exchange.Unused.ShouldBeEmpty();
    }

    // A system column names its table, which is asked for, and has no origin.
    [Fact]
    public async Task DescribeAsync_SystemColumn_HasNoOrigin()
    {
        var users = PostgresTables.Users;
        var script = new PostgresScript().Open();
        _ = script.Prepares(1, "SELECT ctid FROM users", [], new([], null));
        _ = script.Columns(1, 0, new PostgresColumnRow(0, "ctid", 27, users.Oid, -1, null, null, null));
        _ = script.Tables([users.Oid], users);
        _ = script.Resolves([27], PostgresRows.Tid);
        _ = script.Checks("SELECT ctid FROM users", [], PostgresTables.ExpressionOf(0, "ctid", PostgresRows.Tid.Oid));
        _ = script.PlansNothing(1, 0);
        var exchange = script.Frees(1).Play();
        await using var session = await PostgresScript.SessionAsync(exchange);

        var described = await session.DescribeAsync(
            Of("SELECT ctid FROM users"),
            TestContext.Current.CancellationToken
        );

        var column = described.Value.ShouldNotBeNull().Columns.ShouldNotBeNull().ShouldHaveSingleItem();
        column.Origin.ShouldBeNull();
        column.Type.Name.ShouldBe("tid");
        exchange.Unused.ShouldBeEmpty();
    }

    [Fact]
    public async Task DescribeAsync_TablesThatFails_IsSqlsrc225AtCatalog()
    {
        var refused = new PostgresServerError("42501", "permission denied for table pg_attribute", null, null, 300);
        var users = PostgresTables.Users;
        var script = new PostgresScript().Open();
        _ = script.Prepares(1, "SELECT id FROM users", [], new([], null));
        _ = script.Columns(1, 0, PostgresTables.UsersId);
        _ = script.Answer("tables", new PostgresTablesRequest([users.Oid]), new PostgresTablesAnswer(null, refused));
        var exchange = script.Frees(1).Play();
        await using var session = await PostgresScript.SessionAsync(exchange);

        var described = await session.DescribeAsync(Of("SELECT id FROM users"), TestContext.Current.CancellationToken);

        var failure = described.Failure.ShouldNotBeNull();
        failure.Descriptor.Id.ShouldBe("SQLSRC225");
        failure.Step.ShouldBe(DescribeStep.Catalog);
        failure.ServerLines.ShouldBe(["42501: permission denied for table pg_attribute"]);
        exchange.Unused.ShouldBeEmpty();
    }

    // Another session dropped the table, or a column of it, between "columns" and "tables": the answer lacks what
    // "columns" named.  It is SQLSRC225 at the catalog and not a throw, nothing is asked after it, and the statement
    // is still freed.
    [Fact]
    public async Task DescribeAsync_TableThatTablesLacks_IsSqlsrc225AtCatalogAndAsksNoTypes()
    {
        var users = PostgresTables.Users;
        var script = new PostgresScript().Open();
        _ = script.Prepares(1, "SELECT id FROM users", [], new([], null));
        _ = script.Columns(1, 0, PostgresTables.UsersId);
        _ = script.Tables([users.Oid]);
        var exchange = script.Frees(1).Play();
        await using var session = await PostgresScript.SessionAsync(exchange);

        var described = await session.DescribeAsync(Of("SELECT id FROM users"), TestContext.Current.CancellationToken);

        var failure = described.Failure.ShouldNotBeNull();
        failure.Descriptor.Id.ShouldBe("SQLSRC225");
        failure.Step.ShouldBe(DescribeStep.Catalog);
        failure.Arguments.ShouldBe(["Q", "the table of the column 'id' no longer exists"]);
        failure.ServerLines.ShouldBeEmpty();
        failure.Help.ShouldBe(PostgresErrors.SchemaChangedHelp);
        exchange.Unused.ShouldBeEmpty();
    }

    [Fact]
    public async Task DescribeAsync_AttributeThatTablesLacks_IsSqlsrc225AtCatalogAndAsksNoTypes()
    {
        var users = PostgresTables.Users;
        var script = new PostgresScript().Open();
        _ = script.Prepares(1, "SELECT id FROM users", [], new([], null));
        _ = script.Columns(1, 0, PostgresTables.UsersId);
        _ = script.Tables([users.Oid], PostgresTables.TableOf(users.Oid, "users"));
        var exchange = script.Frees(1).Play();
        await using var session = await PostgresScript.SessionAsync(exchange);

        var described = await session.DescribeAsync(Of("SELECT id FROM users"), TestContext.Current.CancellationToken);

        var failure = described.Failure.ShouldNotBeNull();
        failure.Descriptor.Id.ShouldBe("SQLSRC225");
        failure.Step.ShouldBe(DescribeStep.Catalog);
        failure.Arguments.ShouldBe(["Q", "the column 'id' no longer exists in its table 'public.users'"]);
        failure.ServerLines.ShouldBeEmpty();
        failure.Help.ShouldBe(PostgresErrors.SchemaChangedHelp);
        exchange.Unused.ShouldBeEmpty();
    }

    // A type went between "tables" and "types": the answer lacks the row of a type that was asked for.  The first
    // that has none is named, in the order of the request: the parameters, the columns, the origin attributes.  The
    // script holds no "check", so none is asked.
    [Fact]
    public async Task DescribeAsync_TypeOfAColumnThatTypesLacks_IsSqlsrc225AtResolveTypeAndAsksNoCheck()
    {
        var script = new PostgresScript().Open();
        _ = script.Prepares(1, "SELECT 1 AS n", [], new([], null));
        _ = script.Columns(1, 0, PostgresTables.ExpressionOf(0, "n", PostgresRows.Int4.Oid));
        _ = script.Resolves([23]);
        var exchange = script.Frees(1).Play();
        await using var session = await PostgresScript.SessionAsync(exchange);

        var described = await session.DescribeAsync(Of("SELECT 1 AS n"), TestContext.Current.CancellationToken);

        var failure = described.Failure.ShouldNotBeNull();
        failure.Descriptor.Id.ShouldBe("SQLSRC225");
        failure.Step.ShouldBe(DescribeStep.ResolveType);
        failure.Arguments.ShouldBe(["Q", "the type of the column 'n' no longer exists"]);
        failure.ServerLines.ShouldBeEmpty();
        failure.Help.ShouldBe(PostgresErrors.SchemaChangedHelp);
        exchange.Unused.ShouldBeEmpty();
    }

    [Fact]
    public async Task DescribeAsync_TypeOfAParameterThatTypesLacks_IsSqlsrc225AtResolveTypeAndAsksNoCheck()
    {
        var script = new PostgresScript().Open();
        _ = script.PreparesWithNoColumns(
            1,
            "SELECT 1 WHERE $1 = 1",
            ["unknown"],
            new PostgresPrepareAnswer([23], null)
        );
        _ = script.Resolves([23]);
        var exchange = script.Frees(1).Play();
        await using var session = await PostgresScript.SessionAsync(exchange);

        var described = await session.DescribeAsync(
            Of("SELECT 1 WHERE @id = 1", Inferred("id")),
            TestContext.Current.CancellationToken
        );

        var failure = described.Failure.ShouldNotBeNull();
        failure.Descriptor.Id.ShouldBe("SQLSRC225");
        failure.Step.ShouldBe(DescribeStep.ResolveType);
        failure.Arguments.ShouldBe(["Q", "the type of the parameter '@id' no longer exists"]);
        failure.ServerLines.ShouldBeEmpty();
        failure.Help.ShouldBe(PostgresErrors.SchemaChangedHelp);
        exchange.Unused.ShouldBeEmpty();
    }

    // The column's own type is there and the attribute's is not: a domain that went after "tables" named it.
    [Fact]
    public async Task DescribeAsync_TypeOfAnOriginAttributeThatTypesLacks_IsSqlsrc225NamingTheColumn()
    {
        var users = PostgresTables.Users;
        var script = new PostgresScript().Open();
        _ = script.Prepares(1, "SELECT email FROM users", [], new([], null));
        _ = script.Columns(1, 0, PostgresTables.ColumnOf(0, "email", PostgresRows.Varchar.Oid, users, 3, length: 254));
        _ = script.Tables([users.Oid], users);
        _ = script.Resolves([1043, PostgresRows.EmailT.Oid], PostgresRows.Varchar);
        var exchange = script.Frees(1).Play();
        await using var session = await PostgresScript.SessionAsync(exchange);

        var described = await session.DescribeAsync(
            Of("SELECT email FROM users"),
            TestContext.Current.CancellationToken
        );

        var failure = described.Failure.ShouldNotBeNull();
        failure.Descriptor.Id.ShouldBe("SQLSRC225");
        failure.Step.ShouldBe(DescribeStep.ResolveType);
        failure.Arguments.ShouldBe(["Q", "the type of the column 'email' no longer exists in its table"]);
        exchange.Unused.ShouldBeEmpty();
    }

    // One script for a query of users.id and an aggregate, whatever its SQL says of a join.
    private static async Task<QueryDescription> DescribeUsersIdAsync(string sql, PostgresExplainAnswer? explain = null)
    {
        var users = PostgresTables.Users;
        var script = new PostgresScript().Open();
        _ = script.Prepares(1, sql, [], new([], null));
        _ = script.Columns(1, 0, PostgresTables.UsersId, PostgresTables.ExpressionOf(1, "n", PostgresRows.Int8.Oid));
        _ = script.Tables([users.Oid], users);
        _ = script.Resolves([20, 23], PostgresRows.Int8, PostgresRows.Int4);
        _ = script.Checks(
            sql,
            [],
            PostgresTables.ExpressionOf(0, "id", PostgresRows.Int4.Oid),
            PostgresTables.ExpressionOf(1, "n", PostgresRows.Int8.Oid)
        );
        if (explain is not null)
        {
            _ = script.Explains(1, 0, explain);
        }

        var exchange = script.Frees(1).Play();
        await using var session = await PostgresScript.SessionAsync(exchange);

        var described = await session.DescribeAsync(Of(sql), TestContext.Current.CancellationToken);

        exchange.Unused.ShouldBeEmpty();
        return described.Value.ShouldNotBeNull();
    }

    // A column with no origin is answered from the plan, so the plan is asked for, and the column of the table keeps
    // the catalog's answer: without a word of the gate no outer join can have touched it.
    [Fact]
    public async Task DescribeAsync_QueryWithoutAGateWordWithAnExpression_AsksForThePlanAndKeepsTheCatalogsAnswer()
    {
        var plan = Over("WindowAgg", Scan("users")) with { Output = ["users.id", "count(*) OVER w1"] };

        var description = await DescribeUsersIdAsync(UngatedUsersId, PostgresScript.PlanOf(plan));

        description.Plan.ShouldBe(SidecarValues.Plan.Walked);
        AnswersOf(description).ShouldBe([NotNullCatalog, NotNullExpression]);
    }

    // A count the session's path may find in another schema than pg_catalog is no count the walk knows.
    [Fact]
    public async Task DescribeAsync_SearchPathThatPutsTheCatalogLast_LeavesAFunctionUnknown()
    {
        var users = PostgresTables.Users;
        var plan = Over("WindowAgg", Scan("users")) with { Output = ["users.id", "count(*) OVER w1"] };
        var script = new PostgresScript().Answer(
            "open",
            PostgresOpenRequest.Instance,
            PostgresScript.Opened with
            {
                Settings =
                [
                    new PostgresSetting("search_path", "public, pg_catalog"),
                    new PostgresSetting("plan_cache_mode", "auto"),
                    new PostgresSetting("standard_conforming_strings", "on"),
                ],
            }
        );
        _ = script.Prepares(1, UngatedUsersId, [], new([], null));
        _ = script.Columns(1, 0, PostgresTables.UsersId, PostgresTables.ExpressionOf(1, "n", PostgresRows.Int8.Oid));
        _ = script.Tables([users.Oid], users);
        _ = script.Resolves([20, 23], PostgresRows.Int8, PostgresRows.Int4);
        _ = script.Checks(
            UngatedUsersId,
            [],
            PostgresTables.ExpressionOf(0, "id", PostgresRows.Int4.Oid),
            PostgresTables.ExpressionOf(1, "n", PostgresRows.Int8.Oid)
        );
        _ = script.Explains(1, 0, PostgresScript.PlanOf(plan));
        var exchange = script.Frees(1).Play();
        await using var session = await PostgresScript.SessionAsync(exchange);

        var described = await session.DescribeAsync(Of(UngatedUsersId), TestContext.Current.CancellationToken);

        exchange.Unused.ShouldBeEmpty();
        AnswersOf(described.Value.ShouldNotBeNull()).ShouldBe([NotNullCatalog, UnknownNoOrigin]);
    }

    [Fact]
    public async Task DescribeAsync_QueryWhoseEveryColumnHasAnOrigin_NeedsNoPlan()
    {
        const string Sql = "SELECT id FROM users";
        var users = PostgresTables.Users;
        var script = new PostgresScript().Open();
        _ = script.Prepares(1, Sql, [], new([], null));
        _ = script.Columns(1, 0, PostgresTables.UsersId);
        _ = script.Tables([users.Oid], users);
        _ = script.Resolves([23], PostgresRows.Int4);
        _ = script.Checks(Sql, [], PostgresTables.ExpressionOf(0, "id", PostgresRows.Int4.Oid));
        var exchange = script.Frees(1).Play();
        await using var session = await PostgresScript.SessionAsync(exchange);

        var described = await session.DescribeAsync(Of(Sql), TestContext.Current.CancellationToken);

        exchange.Unused.ShouldBeEmpty();
        var description = described.Value.ShouldNotBeNull();
        description.Plan.ShouldBe(SidecarValues.Plan.NotNeeded);
        AnswersOf(description).ShouldBe([NotNullCatalog]);
    }

    private const string UngatedUsersId = "SELECT id, count(*) OVER () AS n FROM users";

    private const string GatedUsersId =
        "SELECT u.id, count(*) OVER () AS n FROM users u LEFT JOIN orders o ON o.user_id = u.id";

    private static (bool? Nullable, string? Source)[] AnswersOf(QueryDescription description) =>
        Of(description.Columns.ShouldNotBeNull());

    // users is the side the join keeps: its NOT NULL column stays so, by the catalog.
    [Fact]
    public async Task DescribeAsync_GatedQueryWhoseJoinKeepsTheColumnsSide_TakesTheCatalogsAnswerAndIsWalked()
    {
        var plan = Join("Left", Scan("u"), Scan("o")) with { Output = ["u.id", "count(*) OVER (?)"] };

        var description = await DescribeUsersIdAsync(GatedUsersId, PostgresScript.PlanOf(plan));

        description.Plan.ShouldBe(SidecarValues.Plan.Walked);
        AnswersOf(description).ShouldBe([NotNullCatalog, UnknownNoOrigin]);
    }

    // The settings of the describer are in the request, so a capture recorded with them is replayed with them.
    [Fact]
    public async Task DescribeAsync_DescriberWithSettings_AsksThePlanWithThem()
    {
        var users = PostgresTables.Users;
        var plan = Join("Left", Scan("u"), Scan("o")) with { Output = ["u.id", "count(*) OVER (?)"] };
        var script = new PostgresScript().Open();
        _ = script.Prepares(1, GatedUsersId, [], new([], null));
        _ = script.Columns(1, 0, PostgresTables.UsersId, PostgresTables.ExpressionOf(1, "n", PostgresRows.Int8.Oid));
        _ = script.Tables([users.Oid], users);
        _ = script.Resolves([20, 23], PostgresRows.Int8, PostgresRows.Int4);
        _ = script.Checks(
            GatedUsersId,
            [],
            PostgresTables.ExpressionOf(0, "id", PostgresRows.Int4.Oid),
            PostgresTables.ExpressionOf(1, "n", PostgresRows.Int8.Oid)
        );
        _ = script.Answer(
            "explain",
            new PostgresExplainRequest("sqlsource_1", 0, ["enable_hashjoin=off", "enable_mergejoin=off"]),
            PostgresScript.PlanOf(plan)
        );
        var exchange = script.Frees(1).Play();
        await using var session = await PostgresScript.SessionAsync(
            exchange,
            explainSettings: ["enable_hashjoin=off", "enable_mergejoin=off"]
        );

        var described = await session.DescribeAsync(Of(GatedUsersId), TestContext.Current.CancellationToken);

        exchange.Unused.ShouldBeEmpty();
        described.Value.ShouldNotBeNull().Plan.ShouldBe(SidecarValues.Plan.Walked);
    }

    // users scanned as the partitions users_1 and users_2, on the side the join keeps.
    private static readonly PlanNode PartitionedUsers = Over(
        "WindowAgg",
        Join(
            "Left",
            Append(Member("u_1", "users_1", "u_1.id"), Member("u_2", "users_2", "u_2.id")),
            Scan("o", "o.user_id")
        ) with
        {
            Output = ["u.id"],
        }
    ) with
    {
        Output = ["u.id", "count(*) OVER w1"],
    };

    private static readonly PostgresPartitionsRequest PartitionsOfUsers = new([
        new PostgresRelationName("public", "users_1"),
        new PostgresRelationName("public", "users_2"),
    ]);

    // The query of users.id and an aggregate, planned over the partitions, with "partitions" answering what the test
    // gives.
    private static async Task<QueryDescription> DescribeUsersIdOverPartitionsAsync(PostgresPartitionsAnswer partitions)
    {
        var users = PostgresTables.Users;
        var script = new PostgresScript().Open();
        _ = script.Prepares(1, GatedUsersId, [], new([], null));
        _ = script.Columns(1, 0, PostgresTables.UsersId, PostgresTables.ExpressionOf(1, "n", PostgresRows.Int8.Oid));
        _ = script.Tables([users.Oid], users);
        _ = script.Resolves([20, 23], PostgresRows.Int8, PostgresRows.Int4);
        _ = script.Checks(
            GatedUsersId,
            [],
            PostgresTables.ExpressionOf(0, "id", PostgresRows.Int4.Oid),
            PostgresTables.ExpressionOf(1, "n", PostgresRows.Int8.Oid)
        );
        _ = script.Explains(1, 0, PostgresScript.PlanOf(PartitionedUsers));
        _ = script.Partitions(PartitionsOfUsers, partitions);
        var exchange = script.Frees(1).Play();
        await using var session = await PostgresScript.SessionAsync(exchange);

        var described = await session.DescribeAsync(Of(GatedUsersId), TestContext.Current.CancellationToken);

        exchange.Unused.ShouldBeEmpty();
        return described.Value.ShouldNotBeNull();
    }

    // A plan with an Append of scans under one alias and a number asks the ancestors of their relations, and the walk
    // reads the Append as the table they are partitions of.
    [Fact]
    public async Task DescribeAsync_PlanWithAnAppendOfPartitions_AsksTheirAncestorsAndWalksThroughThem()
    {
        var users = PostgresTables.Users;

        var description = await DescribeUsersIdOverPartitionsAsync(
            new PostgresPartitionsAnswer(
                [new("public", "users_1", [users.Oid]), new("public", "users_2", [users.Oid])],
                null
            )
        );

        description.Plan.ShouldBe(SidecarValues.Plan.Walked);
        AnswersOf(description).ShouldBe([NotNullCatalog, NotNullExpression]);
    }

    // A "partitions" that fails is logged and never reported: the Append is read as a set operation.
    [Fact]
    public async Task DescribeAsync_PartitionsThatFails_ReadsTheAppendAsASetOperation()
    {
        var description = await DescribeUsersIdOverPartitionsAsync(
            new PostgresPartitionsAnswer(null, new PostgresServerError("XX000", "no catalog", null, null, null))
        );

        description.Plan.ShouldBe(SidecarValues.Plan.Walked);
        AnswersOf(description).ShouldBe([NullableHeuristic, UnknownNoOrigin]);
    }

    // A UNION ALL of users and of orders, or of orders' partitions, whose first column has users.id as its origin in
    // the row description the test writes, and whose second has none: the plan answers it from its branches.
    private const string UnionOfUsersAndOrders =
        "SELECT id, user_id FROM users UNION ALL SELECT user_id, user_id FROM o";

    private static PlanNode UnionPlan(bool partitioned)
    {
        var orders = partitioned
            ? Append(
                Member("o_1", "orders_1", "o_1.user_id", "o_1.user_id"),
                Member("o_2", "orders_2", "o_2.user_id", "o_2.user_id")
            )
            : Scan("orders", "orders.user_id", "orders.user_id");
        return Append(Member(Scan("users", "users.id", "users.id")), Member(orders));
    }

    private static readonly PostgresTableRow Orders1 = PostgresTables.Orders with { Oid = 16420, Name = "orders_1" };

    private static readonly PostgresTableRow Orders2 = PostgresTables.Orders with { Oid = 16421, Name = "orders_2" };

    // The describe of the UNION ALL, with each call after "explain" the script writes.
    private static async Task<(QueryDescription Description, RecordingLog Log)> DescribeUnionAsync(
        PlanNode plan,
        Action<PostgresScript> afterExplain
    )
    {
        var users = PostgresTables.Users;
        var script = new PostgresScript().Open();
        _ = script.Prepares(1, UnionOfUsersAndOrders, [], new([], null));
        _ = script.Columns(
            1,
            0,
            PostgresTables.UsersId,
            PostgresTables.ExpressionOf(1, "user_id", PostgresRows.Int4.Oid)
        );
        _ = script.Tables([users.Oid], users);
        _ = script.Resolves([23], PostgresRows.Int4);
        _ = script.Checks(
            UnionOfUsersAndOrders,
            [],
            PostgresTables.ExpressionOf(0, "id", PostgresRows.Int4.Oid),
            PostgresTables.ExpressionOf(1, "user_id", PostgresRows.Int4.Oid)
        );
        _ = script.Explains(1, 0, PostgresScript.PlanOf(plan));
        afterExplain(script);
        var exchange = script.Frees(1).Play();
        var log = new RecordingLog();
        await using var session = await PostgresScript.SessionAsync(exchange, log);

        var described = await session.DescribeAsync(Of(UnionOfUsersAndOrders), TestContext.Current.CancellationToken);

        exchange.Unused.ShouldBeEmpty();
        return (described.Value.ShouldNotBeNull(), log);
    }

    // "relations" is asked after "partitions", for the relations that "tables" did not answer, and the walk reads
    // their NOT NULL.
    [Fact]
    public async Task DescribeAsync_SetOperationWithAColumnOfNoOrigin_AsksTheRelationsItsBranchesScan()
    {
        var (description, log) = await DescribeUnionAsync(
            UnionPlan(partitioned: true),
            script =>
            {
                _ = script.Answer(
                    "partitions",
                    new PostgresPartitionsRequest([
                        new PostgresRelationName("public", "orders_1"),
                        new PostgresRelationName("public", "orders_2"),
                    ]),
                    new PostgresPartitionsAnswer(
                        [
                            new("public", "orders_1", [PostgresTables.Orders.Oid]),
                            new("public", "orders_2", [PostgresTables.Orders.Oid]),
                        ],
                        null
                    )
                );
                _ = script.Answer(
                    "relations",
                    new PostgresRelationsRequest([
                        new PostgresRelationName("public", "orders_1"),
                        new PostgresRelationName("public", "orders_2"),
                    ]),
                    new PostgresTablesAnswer([Orders1, Orders2], null)
                );
            }
        );

        description.Plan.ShouldBe(SidecarValues.Plan.Walked);
        AnswersOf(description).ShouldBe([NotNullCatalog, NotNullSetOperation]);
        log.Events.Select(written => written.Name)
            .Where(name => name is "explain" or "partitions" or "relations" or "walk")
            .ShouldBe(["explain", "partitions", "relations", "walk"]);
    }

    // Every relation the plan scans is one "tables" answered: nothing more is asked, and the script holds nothing.
    [Fact]
    public async Task DescribeAsync_SetOperationOfRelationsTablesAnswered_AsksNoRelations()
    {
        var plan = Append(Member(Scan("users", "users.id", "users.id")), Member("u2", "users", "u2.id", "u2.id"));

        var (description, _) = await DescribeUnionAsync(plan, static _ => { });

        AnswersOf(description).ShouldBe([NotNullCatalog, NotNullSetOperation]);
    }

    // The root reaches no set operation: the one under the join is not taken on, and nothing more is asked.
    [Fact]
    public async Task DescribeAsync_SetOperationUnderAJoin_AsksNoRelations()
    {
        var plan = Join("Left", Scan("users", "users.id"), UnionPlan(partitioned: false)) with
        {
            Output = ["users.id", "orders.user_id"],
        };

        var (description, _) = await DescribeUnionAsync(plan, static _ => { });

        AnswersOf(description).ShouldBe([NotNullCatalog, UnknownNoOrigin]);
    }

    // A gated query whose every column has an origin: the plan answers no column of no origin, and nothing more is
    // asked.
    [Fact]
    public async Task DescribeAsync_SetOperationWhoseEveryColumnHasAnOrigin_AsksNoRelations()
    {
        const string Sql = "SELECT u.id FROM users u LEFT JOIN orders o ON o.user_id = u.id UNION ALL SELECT 1";
        var users = PostgresTables.Users;
        var script = new PostgresScript().Open();
        _ = script.Prepares(1, Sql, [], new([], null));
        _ = script.Columns(1, 0, PostgresTables.UsersId);
        _ = script.Tables([users.Oid], users);
        _ = script.Resolves([23], PostgresRows.Int4);
        _ = script.Checks(Sql, [], PostgresTables.ExpressionOf(0, "id", PostgresRows.Int4.Oid));
        _ = script.Explains(1, 0, PostgresScript.PlanOf(UnionPlan(partitioned: false)));
        var exchange = script.Frees(1).Play();
        await using var session = await PostgresScript.SessionAsync(exchange);

        var described = await session.DescribeAsync(Of(Sql), TestContext.Current.CancellationToken);

        exchange.Unused.ShouldBeEmpty();
        AnswersOf(described.Value.ShouldNotBeNull()).ShouldBe([NullableHeuristic]);
    }

    // A "relations" that fails is logged and never reported: the walk goes on without its rows.
    [Fact]
    public async Task DescribeAsync_RelationsThatFails_IsLoggedAndTheColumnIsKept()
    {
        var (description, log) = await DescribeUnionAsync(
            UnionPlan(partitioned: false),
            static script =>
                script.Answer(
                    "relations",
                    new PostgresRelationsRequest([new PostgresRelationName("public", "orders")]),
                    new PostgresTablesAnswer(null, new PostgresServerError("XX000", "no catalog", null, null, null))
                )
        );

        description.Plan.ShouldBe(SidecarValues.Plan.Walked);
        AnswersOf(description).ShouldBe([NotNullCatalog, UnknownNoOrigin]);
        var (_, members) = log.Events.Single(written => written.Name == "relations");
        members.ShouldContain(member => member.Name == "error");
    }

    // The gated query of users.id and a window's count, planned with users as the side the join null-extends.
    [Fact]
    public async Task DescribeAsync_GatedQueryWhoseJoinExtendsTheColumnsSide_IsNullableByTheOuterJoin()
    {
        var plan = Join("Right", Scan("u"), Scan("o")) with { Output = ["u.id", "count(*) OVER (?)"] };

        var description = await DescribeUsersIdAsync(GatedUsersId, PostgresScript.PlanOf(plan));

        description.Plan.ShouldBe(SidecarValues.Plan.Walked);
        AnswersOf(description).ShouldBe([NullableOuterJoin, UnknownNoOrigin]);
    }

    public static TheoryData<string?, string?> PlansThatCannotBeUsed =>
        new()
        {
            // The server gave no plan: the error is logged and never reported.
            { null, "canceling statement due to statement timeout" },
            // The text is no plan.
            { "not json", null },
            { "[]", null },
            // An answer that holds neither.
            { null, null },
            // A plan that is read and cannot be used.
            { Text(Join("Sideways", Scan("u"), Scan("o")) with { Output = ["u.id", "n"] }), null },
        };

    [Theory]
    [MemberData(nameof(PlansThatCannotBeUsed))]
    public async Task DescribeAsync_GatedQueryWithoutAPlanTheWalkCanUse_IsDescribedByTheHeuristicAndUnavailable(
        string? plan,
        string? error
    )
    {
        var answer = new PostgresExplainAnswer(
            plan,
            error is null ? null : new PostgresServerError("57014", error, null, null, null)
        );

        var description = await DescribeUsersIdAsync(GatedUsersId, answer);

        description.Plan.ShouldBe(SidecarValues.Plan.Unavailable);
        description.TableMatch.ShouldBe(SidecarValues.TableMatch.NoOrigin);
        AnswersOf(description).ShouldBe([NullableHeuristic, UnknownNoOrigin]);
    }

    // A check that fails ends the describe before the plan is asked for: the script holds no "explain".
    [Fact]
    public async Task DescribeAsync_GatedQueryWhoseCheckDiffers_IsTheFailureAndAsksNoPlan()
    {
        var users = PostgresTables.Users;
        var script = new PostgresScript().Open();
        _ = script.Prepares(1, GatedUsersId, [], new([], null));
        _ = script.Columns(1, 0, PostgresTables.UsersId, PostgresTables.ExpressionOf(1, "n", PostgresRows.Int8.Oid));
        _ = script.Tables([users.Oid], users);
        _ = script.Resolves([20, 23], PostgresRows.Int8, PostgresRows.Int4);
        _ = script.Checks(GatedUsersId, [], PostgresTables.ExpressionOf(0, "id", PostgresRows.Int4.Oid));
        var exchange = script.Frees(1).Play();
        await using var session = await PostgresScript.SessionAsync(exchange);

        var described = await session.DescribeAsync(Of(GatedUsersId), TestContext.Current.CancellationToken);

        described.Failure.ShouldNotBeNull().Descriptor.Id.ShouldBe("SQLSRC231");
        exchange.Unused.ShouldBeEmpty();
    }

    // The plan is asked with a NULL for each parameter the server resolved, which for this query are those of its
    // list.
    [Fact]
    public async Task DescribeAsync_GatedQueryWithAParameter_AsksThePlanWithTheNumberOfItsParameters()
    {
        var sql = "SELECT u.id FROM users u LEFT JOIN orders o ON o.user_id = u.id WHERE u.id = @id";
        var users = PostgresTables.Users;
        var id = PostgresTables.UsersId;
        var script = new PostgresScript().Open();
        _ = script.Prepares(
            1,
            "SELECT u.id FROM users u LEFT JOIN orders o ON o.user_id = u.id WHERE u.id = $1",
            ["unknown"],
            new([23], null)
        );
        _ = script.Columns(1, 1, id);
        _ = script.Tables([users.Oid], users);
        _ = script.Resolves([23], PostgresRows.Int4);
        _ = script.Checks(sql, [("id", "integer")], PostgresTables.ExpressionOf(0, "id", PostgresRows.Int4.Oid));
        _ = script.Explains(1, 1, PostgresScript.PlanOf(Join("Left", Scan("u"), Scan("o")) with { Output = ["u.id"] }));
        var exchange = script.Frees(1).Play();
        await using var session = await PostgresScript.SessionAsync(exchange);

        var described = await session.DescribeAsync(Of(sql, Inferred("id")), TestContext.Current.CancellationToken);

        exchange.Unused.ShouldBeEmpty();
        AnswersOf(described.Value.ShouldNotBeNull()).ShouldBe([NotNullCatalog]);
    }

    // A statement without rows has no provenance and asks for no plan, gate word or not.
    [Fact]
    public async Task DescribeAsync_GatedStatementWithoutRows_HasNoPlan()
    {
        var script = new PostgresScript().Open();
        _ = script.PreparesWithNoColumns(1, "DELETE FROM users WHERE left(name, 1) = 'a'", [], new([], null));
        _ = script.Checks("DELETE FROM users WHERE left(name, 1) = 'a'", []);
        var exchange = script.Frees(1).Play();
        await using var session = await PostgresScript.SessionAsync(exchange);

        var described = await session.DescribeAsync(
            Of("DELETE FROM users WHERE left(name, 1) = 'a'"),
            TestContext.Current.CancellationToken
        );

        described.Value.ShouldNotBeNull().Plan.ShouldBeNull();
    }

    // A table of two columns, whole in the answer of "tables".
    private static readonly PostgresTableRow Labels = PostgresTables.TableOf(
        16500,
        "labels",
        PostgresTables.Attribute(1, "id", PostgresRows.Int4.Oid, notNull: true),
        PostgresTables.Attribute(2, "label", PostgresRows.Text.Oid)
    );

    // One script for a query whose columns are the two of labels in their order, the second under the name given.
    private static async Task<QueryDescription> DescribeLabelsAsync(
        string sql,
        string second = "label",
        PostgresExplainAnswer? explain = null
    )
    {
        var script = new PostgresScript().Open();
        _ = script.Prepares(1, sql, [], new([], null));
        _ = script.Columns(
            1,
            0,
            PostgresTables.ColumnOf(0, "id", PostgresRows.Int4.Oid, Labels, 1),
            PostgresTables.ColumnOf(1, second, 25, Labels, 2)
        );
        _ = script.Tables([Labels.Oid], Labels);
        _ = script.Resolves([23, 25], PostgresRows.Int4, PostgresRows.Text);
        _ = script.Checks(
            sql,
            [],
            PostgresTables.ExpressionOf(0, "id", PostgresRows.Int4.Oid),
            PostgresTables.ExpressionOf(1, second, 25)
        );
        if (explain is not null)
        {
            _ = script.Explains(1, 0, explain);
        }

        var exchange = script.Frees(1).Play();
        await using var session = await PostgresScript.SessionAsync(exchange);

        var described = await session.DescribeAsync(Of(sql), TestContext.Current.CancellationToken);

        exchange.Unused.ShouldBeEmpty();
        return described.Value.ShouldNotBeNull();
    }

    // The match asks nothing: the script holds the calls of any query with columns, and no other.
    [Fact]
    public async Task DescribeAsync_EveryColumnOfATableInItsOrder_SaysTheTable()
    {
        var description = await DescribeLabelsAsync("SELECT * FROM labels");

        description.MatchesTable.ShouldBe(new SidecarTable("public", "labels"));
        description.TableMatch.ShouldBe(SidecarValues.TableMatch.Matched);
    }

    // The name is the one the server returned.
    [Fact]
    public async Task DescribeAsync_ColumnUnderAnotherName_MatchesNothingAndSaysNamesDiffer()
    {
        var description = await DescribeLabelsAsync("SELECT id, label AS \"label!\" FROM labels", "label!");

        description.MatchesTable.ShouldBeNull();
        description.TableMatch.ShouldBe(SidecarValues.TableMatch.NamesDiffer);
    }

    // The match is made after the walk and reads the nullability it set: labels is the side the join extends, so
    // its NOT NULL column is nullable and the result is not the table's.
    [Fact]
    public async Task DescribeAsync_GatedQueryThatExtendsATablesList_MatchesNothingAndSaysNullabilityDiffers()
    {
        var plan = Join("Left", Scan("o"), Scan("l")) with { Output = ["l.id", "l.label"] };

        var description = await DescribeLabelsAsync(
            "SELECT l.* FROM orders o LEFT JOIN labels l ON l.id = o.id",
            explain: PostgresScript.PlanOf(plan)
        );

        description.MatchesTable.ShouldBeNull();
        description.TableMatch.ShouldBe(SidecarValues.TableMatch.NullabilityDiffers);
        description.Plan.ShouldBe(SidecarValues.Plan.Walked);
    }

    // The other way round, labels keeps every row as it is, and the result is the table's.
    [Fact]
    public async Task DescribeAsync_GatedQueryThatKeepsATablesList_MatchesTheTable()
    {
        var plan = Join("Left", Scan("l"), Scan("o")) with { Output = ["l.id", "l.label"] };

        var description = await DescribeLabelsAsync(
            "SELECT l.* FROM labels l LEFT JOIN orders o ON o.id = l.id",
            explain: PostgresScript.PlanOf(plan)
        );

        description.MatchesTable.ShouldBe(new SidecarTable("public", "labels"));
        description.TableMatch.ShouldBe(SidecarValues.TableMatch.Matched);
        description.Plan.ShouldBe(SidecarValues.Plan.Walked);
    }

    // Without a plan the heuristic stands, and the match reads that.
    [Fact]
    public async Task DescribeAsync_GatedQueryOverATablesListWithoutAPlan_SaysNullabilityDiffers()
    {
        var description = await DescribeLabelsAsync(
            "SELECT l.* FROM labels l LEFT JOIN orders o ON o.id = l.id",
            explain: new PostgresExplainAnswer(null, new PostgresServerError(null, "lost", null, null, null))
        );

        description.TableMatch.ShouldBe(SidecarValues.TableMatch.NullabilityDiffers);
        description.Plan.ShouldBe(SidecarValues.Plan.Unavailable);
    }

    // No value has a pseudo-type, so the query is not described.  The script answers no call after "types".
    [Theory]
    [InlineData(false, "record")]
    [InlineData(true, "record[]")]
    public async Task DescribeAsync_ColumnOfAPseudoType_IsSqlsrc229NamingTheColumnAndTheType(bool array, string type)
    {
        var pseudo = array ? PostgresRows.RecordArray : PostgresRows.Record;
        var script = new PostgresScript().Open();
        _ = script.Prepares(1, "SELECT 1 AS one, ROW(1, 2) AS pair, ROW(3) AS other", [], new([], null));
        _ = script.Columns(
            1,
            0,
            PostgresTables.ExpressionOf(0, "one", PostgresRows.Int4.Oid),
            PostgresTables.ExpressionOf(1, "pair", pseudo.Oid),
            PostgresTables.ExpressionOf(2, "other", pseudo.Oid)
        );

        // The request names the types of the columns; the closure the server answers holds an array's element too.
        PostgresTypeRow[] closure = array
            ? [PostgresRows.Int4, PostgresRows.Record, pseudo]
            : [PostgresRows.Int4, pseudo];
        _ = script.Resolves([23, pseudo.Oid], closure);
        var exchange = script.Frees(1).Play();
        await using var session = await PostgresScript.SessionAsync(exchange);

        var described = await session.DescribeAsync(
            Of("SELECT 1 AS one, ROW(1, 2) AS pair, ROW(3) AS other"),
            TestContext.Current.CancellationToken
        );

        var failure = described.Failure.ShouldNotBeNull();
        failure.Descriptor.Id.ShouldBe("SQLSRC229");
        failure.Arguments.ShouldBe(["pair", "Q", type]);
        failure.Step.ShouldBe(DescribeStep.ResolveType);
        failure.ServerLines.ShouldBeEmpty();
        failure.Help.ShouldBe(PostgresErrors.ColumnTypeHelp);
        exchange.Unused.ShouldBeEmpty();
    }

    // The parameters are typed before the columns are built: a pseudo-type that is a parameter's and so a
    // column's is the parameter's failure, whose help says what to declare.
    [Fact]
    public async Task DescribeAsync_PseudoTypeOfAParameterAndOfAColumn_IsTheParametersFailure()
    {
        var script = new PostgresScript().Open();
        _ = script.Prepares(1, "SELECT $1", ["record"], new([2249], null));
        _ = script.Columns(1, 1, PostgresTables.ExpressionOf(0, "?column?", PostgresRows.Record.Oid));
        _ = script.Resolves([2249], PostgresRows.Record);
        var exchange = script.Frees(1).Play();
        await using var session = await PostgresScript.SessionAsync(exchange);

        var described = await session.DescribeAsync(
            Of("SELECT @p", Declared("p", "record")),
            TestContext.Current.CancellationToken
        );

        described.Failure.ShouldNotBeNull().Descriptor.Id.ShouldBe("SQLSRC228");
        exchange.Unused.ShouldBeEmpty();
    }

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

    // The pairs of the parameter's domain base and of the columns are asked in one request, after "types", sorted
    // and each once: two columns of varchar(10), one of them an array, give one pair.  A column without a modifier
    // gives none.
    [Fact]
    public async Task DescribeAsync_ColumnsWithModifiers_AsksSpellingsOnceAfterTypes()
    {
        const string Sql = "SELECT names, v, amount, n FROM t WHERE email = $1";
        PostgresColumnRow[] columns =
        [
            PostgresTables.ExpressionOf(0, "names", PostgresRows.VarcharArray.Oid, length: 10) with
            {
                TypeModifier = Typmods.Varchar10,
            },
            PostgresTables.ExpressionOf(1, "v", PostgresRows.Varchar.Oid, length: 10) with
            {
                TypeModifier = Typmods.Varchar10,
            },
            PostgresTables.ExpressionOf(2, "amount", PostgresRows.Numeric.Oid, precision: 6, scale: 2) with
            {
                TypeModifier = Typmods.Numeric6Scale2,
            },
            PostgresTables.ExpressionOf(3, "n", PostgresRows.Int4.Oid) with
            {
                TypeModifier = -1,
            },
        ];
        var script = new PostgresScript().Open();
        _ = script.Prepares(1, Sql, ["unknown"], new([PostgresRows.EmailT254.Oid], null));
        _ = script.Columns(1, 1, columns);
        _ = script.Resolves(
            [23, 1015, 1043, 1700, PostgresRows.EmailT254.Oid],
            PostgresRows.Int4,
            PostgresRows.Varchar,
            PostgresRows.VarcharArray,
            PostgresRows.Numeric,
            PostgresRows.EmailT254
        );
        _ = script.Spells(
            (1043, Typmods.Varchar10, "character varying(10)"),
            (1043, Typmods.Varchar254, "character varying(254)"),
            (1700, Typmods.Numeric6Scale2, "numeric(6,2)")
        );
        _ = script.Checks(
            "SELECT names, v, amount, n FROM t WHERE email = @email",
            [("email", "public.email_t")],
            columns
        );
        _ = script.PlansNothing(1, 1);
        var exchange = script.Frees(1).Play();
        var log = new RecordingLog();
        await using var session = await PostgresScript.SessionAsync(exchange, log);

        var described = await session.DescribeAsync(
            Of("SELECT names, v, amount, n FROM t WHERE email = @email", Inferred("email")),
            TestContext.Current.CancellationToken
        );

        var description = described.Value.ShouldNotBeNull();
        var parameter = description.Parameters.ShouldHaveSingleItem().Type.ShouldBeOfType<PostgresType>();
        parameter.Name.ShouldBe("public.email_t");
        parameter.Base.ShouldNotBeNull().Name.ShouldBe("character varying(254)");
        description
            .Columns.ShouldNotBeNull()
            .Select(column => column.Type.Name)
            .ShouldBe(["character varying(10)[]", "character varying(10)", "numeric(6,2)", "integer"]);
        log.Events.Select(written => written.Name)
            .Where(name => name is "types" or "spellings" or "column")
            .ShouldBe(["types", "spellings", "column", "column", "column", "column"]);
        exchange.Unused.ShouldBeEmpty();
    }

    // Columns and parameters whose types carry no modifier: "spellings" is not in the script, and asking it would
    // be a CaptureMissException.
    [Fact]
    public async Task DescribeAsync_NoModifierToSpell_AsksNoSpellings()
    {
        PostgresColumnRow[] columns =
        [
            PostgresTables.ExpressionOf(0, "a", PostgresRows.Text.Oid) with
            {
                TypeModifier = -1,
            },
        ];
        var script = new PostgresScript().Open();
        _ = script.Prepares(1, "SELECT $1::text AS a", ["unknown"], new([25], null));
        _ = script.Columns(1, 1, columns);
        _ = script.Resolves([25], PostgresRows.Text);
        _ = script.Checks("SELECT @p::text AS a", [("p", "text")], columns);
        _ = script.PlansNothing(1, 1);
        var exchange = script.Frees(1).Play();
        await using var session = await PostgresScript.SessionAsync(exchange);

        var described = await session.DescribeAsync(
            Of("SELECT @p::text AS a", Inferred("p")),
            TestContext.Current.CancellationToken
        );

        described.Value.ShouldNotBeNull().Columns.ShouldNotBeNull().ShouldHaveSingleItem().Type.Name.ShouldBe("text");
        exchange.Unused.ShouldBeEmpty();
    }

    // A capture recorded before the modifier was read: no TypeModifier, so Npgsql's numbers spell the columns as
    // they did, the array without its element's length, and nothing is spelled by the server.
    [Fact]
    public async Task DescribeAsync_OldCaptureWithoutModifiers_IsDescribedAsBefore()
    {
        PostgresColumnRow[] columns =
        [
            PostgresTables.ExpressionOf(0, "names", PostgresRows.VarcharArray.Oid, length: 10),
            PostgresTables.ExpressionOf(1, "d", PostgresRows.Interval.Oid, precision: 3),
        ];
        var script = new PostgresScript().Open();
        _ = script.Prepares(1, "SELECT names, d FROM t", [], new([], null));
        _ = script.Columns(1, 0, columns);
        _ = script.Resolves([1015, 1186], PostgresRows.Varchar, PostgresRows.VarcharArray, PostgresRows.Interval);
        _ = script.Checks("SELECT names, d FROM t", [], columns);
        _ = script.PlansNothing(1, 0);
        var exchange = script.Frees(1).Play();
        await using var session = await PostgresScript.SessionAsync(exchange);

        var described = await session.DescribeAsync(
            Of("SELECT names, d FROM t"),
            TestContext.Current.CancellationToken
        );

        var built = described.Value.ShouldNotBeNull().Columns.ShouldNotBeNull();
        built.Select(column => column.Type.Name).ShouldBe(["character varying[]", "interval(3)"]);
        built[0]
            .Type.ShouldBeOfType<PostgresType>()
            .Element.ShouldBe(TestSidecars.Postgres("character varying", internalName: "varchar"));
        built[1].Type.ShouldBeOfType<PostgresType>().Precision.ShouldBe(3);
        exchange.Unused.ShouldBeEmpty();
    }

    // An error of "spellings" is one of "types": the types could not be resolved.  The statement is still freed.
    [Fact]
    public async Task DescribeAsync_SpellingsRefused_IsSqlsrc225AtResolveType()
    {
        var dropped = new PostgresServerError(
            "57P01",
            "terminating connection due to administrator command",
            null,
            null,
            null
        );
        PostgresColumnRow[] columns =
        [
            PostgresTables.ExpressionOf(0, "v", PostgresRows.Varchar.Oid, length: 10) with
            {
                TypeModifier = Typmods.Varchar10,
            },
        ];
        var script = new PostgresScript().Open();
        _ = script.Prepares(1, "SELECT v FROM t", [], new([], null));
        _ = script.Columns(1, 0, columns);
        _ = script.Resolves([1043], PostgresRows.Varchar);
        _ = script.Answer(
            "spellings",
            new PostgresSpellingsRequest([new PostgresTypmodPair(1043, Typmods.Varchar10)]),
            new PostgresSpellingsAnswer(null, dropped)
        );
        var exchange = script.Frees(1).Play();
        await using var session = await PostgresScript.SessionAsync(exchange);

        var described = await session.DescribeAsync(Of("SELECT v FROM t"), TestContext.Current.CancellationToken);

        var failure = described.Failure.ShouldNotBeNull();
        failure.Descriptor.Id.ShouldBe("SQLSRC225");
        failure.Step.ShouldBe(DescribeStep.ResolveType);
        failure.ServerLines.ShouldBe(["57P01: terminating connection due to administrator command"]);
        exchange.Unused.ShouldBeEmpty();
    }
}
