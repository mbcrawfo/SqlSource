using System;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Testing.Snapshot;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Describing.Postgres;
using Xunit;
using static SqlSource.Tool.Testing.NullabilityAnswers;
using static SqlSource.Tool.Tests.PostgresRequests;

namespace SqlSource.Tool.Tests.Describing.Postgres;

// The session over a replay the test writes: the types of the parameters, each OID asked once, and a value of a
// pseudo-type.  A call the script does not hold is a CaptureMissException, so a describe that asks more than its test
// wrote fails.
public sealed class PostgresSessionParameterTests
{
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
}
