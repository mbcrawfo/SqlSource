using System;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Parsing;
using SqlSource.Snapshot;
using SqlSource.Tests.Snapshot;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Describing.Postgres;
using Xunit;

namespace SqlSource.Tool.Tests.Describing.Postgres;

// The session over a replay the test writes: what it asks, in which order, and what it makes of each answer.  A
// call the script does not hold is a CaptureMissException, so a describe that asks more than its test wrote fails.
public class PostgresSessionTests
{
    private static DescribeRequest Request(string sql, params SqlQueryParameter[] parameters) =>
        new("Q", sql, new EquatableArray<SqlQueryParameter>([.. parameters]));

    private static SqlQueryParameter Inferred(string name) => new(name, null, null, false);

    private static SqlQueryParameter Declared(string name, string type) => new(name, type, null, true);

    // The script holds "open" alone: nothing may be asked for a text of two statements.
    [Theory]
    [InlineData("DELETE FROM users WHERE id = @id; DELETE FROM orders")]
    [InlineData("SELECT 1;;")]
    public async Task DescribeAsync_SeveralStatements_IsSqlsrc230AndAsksNothing(string sql)
    {
        await using var session = await PostgresScript.SessionAsync(new PostgresScript().Open().Play());

        var described = await session.DescribeAsync(
            Request(sql, Inferred("id")),
            TestContext.Current.CancellationToken
        );

        described.Value.ShouldBeNull();
        described.Failure.ShouldNotBeNull().Descriptor.Id.ShouldBe("SQLSRC230");
    }

    [Fact]
    public async Task DescribeAsync_DeclaredTypeThatIsNoTypeName_IsSqlsrc228AndAsksNothing()
    {
        await using var session = await PostgresScript.SessionAsync(new PostgresScript().Open().Play());

        var described = await session.DescribeAsync(
            Request("SELECT 1 FROM users WHERE id = @id", Declared("id", "int) AS SELECT 1; DROP TABLE users; --")),
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
            Request("SELECT @id; SELECT 2", Declared("id", "int foo")),
            TestContext.Current.CancellationToken
        );

        described.Failure.ShouldNotBeNull().Descriptor.Id.ShouldBe("SQLSRC230");
    }

    private static readonly PostgresDeallocateAnswer Freed = new(null);

    private static PostgresScript Prepares(
        PostgresScript script,
        int number,
        string sql,
        string[] types,
        PostgresPrepareAnswer answer
    ) => script.Answer("prepare", new PostgresPrepareRequest($"sqlsource_{number}", sql, types), answer);

    private static PostgresScript Frees(PostgresScript script, int number, PostgresDeallocateAnswer? answer = null) =>
        script.Answer("deallocate", new PostgresDeallocateRequest($"sqlsource_{number}"), answer ?? Freed);

    // The trailing ";" is not sent, the parameter is $1, and the statement is freed when the describe is done.
    [Fact]
    public async Task DescribeAsync_Query_AsksPrepareThenDeallocateForTheStatement()
    {
        var script = new PostgresScript().Open();
        _ = Prepares(script, 1, "DELETE FROM samples WHERE flag", [], new PostgresPrepareAnswer([], null));
        var exchange = Frees(script, 1).Play();
        await using var session = await PostgresScript.SessionAsync(exchange);

        var described = await session.DescribeAsync(
            Request("DELETE FROM samples WHERE flag;"),
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
        _ = Prepares(script, 1, "DELETE FROM samples", [], new PostgresPrepareAnswer([], null));
        _ = Frees(script, 1);
        _ = Prepares(script, 2, "DELETE FROM orders", [], new PostgresPrepareAnswer([], null));
        var exchange = Frees(script, 2).Play();
        await using var session = await PostgresScript.SessionAsync(exchange);

        _ = await session.DescribeAsync(Request("DELETE FROM samples"), TestContext.Current.CancellationToken);
        var second = await session.DescribeAsync(Request("DELETE FROM orders"), TestContext.Current.CancellationToken);

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
        _ = Prepares(script, 1, "DELETE FROM nope", [], new PostgresPrepareAnswer(null, rejected));
        _ = Prepares(script, 2, "DELETE FROM orders", [], new PostgresPrepareAnswer([], null));
        var exchange = Frees(script, 2).Play();
        await using var session = await PostgresScript.SessionAsync(exchange);

        var first = await session.DescribeAsync(Request("DELETE FROM nope"), TestContext.Current.CancellationToken);
        var second = await session.DescribeAsync(Request("DELETE FROM orders"), TestContext.Current.CancellationToken);

        var failure = first.Failure.ShouldNotBeNull();
        failure.Descriptor.Id.ShouldBe("SQLSRC225");
        failure.Arguments.ShouldBe(["Q", "relation \"nope\" does not exist"]);
        failure.ServerLines.ShouldBe(["42P01: relation \"nope\" does not exist", "at character 13"]);
        _ = second.Value.ShouldNotBeNull();
        exchange.Unused.ShouldBeEmpty();
    }

    // The error of "deallocate" is logged and never reported.
    [Fact]
    public async Task DescribeAsync_DeallocateThatFails_IsStillDescribed()
    {
        var script = new PostgresScript().Open();
        _ = Prepares(script, 1, "DELETE FROM samples", [], new PostgresPrepareAnswer([], null));
        var gone = new PostgresServerError(
            "26000",
            "prepared statement \"sqlsource_1\" does not exist",
            null,
            null,
            null
        );
        var exchange = Frees(script, 1, new PostgresDeallocateAnswer(gone)).Play();
        await using var session = await PostgresScript.SessionAsync(exchange);

        var described = await session.DescribeAsync(
            Request("DELETE FROM samples"),
            TestContext.Current.CancellationToken
        );

        _ = described.Value.ShouldNotBeNull();
        described.Failure.ShouldBeNull();
    }

    // The declared type goes into the list as written, and the rest is "unknown".
    [Fact]
    public async Task DescribeAsync_DeclaredAndInferredParameters_AskWithTheTypeListOfTheQuery()
    {
        var rejected = new PostgresServerError("42704", "type \"varchar\" does not exist", null, null, 31);
        var script = new PostgresScript().Open();
        var exchange = Prepares(
                script,
                1,
                "SELECT 1 FROM users WHERE id = $1 AND name = $2",
                ["unknown", "varchar(50)", "nope_t"],
                new PostgresPrepareAnswer(null, rejected)
            )
            .Play();
        await using var session = await PostgresScript.SessionAsync(exchange);

        var described = await session.DescribeAsync(
            Request(
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

    private static PostgresScript Resolves(PostgresScript script, long[] oids, params PostgresTypeRow[] rows) =>
        script.Answer("types", new PostgresTypesRequest(oids), new PostgresTypesAnswer(rows, null));

    [Fact]
    public async Task DescribeAsync_InferredAndDeclaredParameters_AreTypedInTheListsOrder()
    {
        var script = new PostgresScript().Open();
        _ = Prepares(
            script,
            1,
            "SELECT id FROM users WHERE id = $1 AND name = $2",
            ["unknown", "varchar(50)"],
            new PostgresPrepareAnswer([23, 1043], null)
        );
        _ = Resolves(script, [23, 1043], PostgresRows.Int4, PostgresRows.Varchar);
        var exchange = Frees(script, 1).Play();
        await using var session = await PostgresScript.SessionAsync(exchange);

        var described = await session.DescribeAsync(
            Request(
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
                        new DescribedParameter("id", TestSidecars.Postgres(), SidecarValues.TypeSource.Inferred),
                        new DescribedParameter(
                            "name",
                            TestSidecars.Postgres("character varying", internalName: "varchar"),
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
        _ = Prepares(
            script,
            1,
            "UPDATE users SET name = $1 WHERE id = $2",
            ["unknown", "unknown", "int"],
            new PostgresPrepareAnswer([1043, 23, 23], null)
        );
        _ = Resolves(script, [23, 1043], PostgresRows.Int4, PostgresRows.Varchar);
        var exchange = Frees(script, 1).Play();
        await using var session = await PostgresScript.SessionAsync(exchange);

        var described = await session.DescribeAsync(
            Request(
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
        parameters[2].Type.ShouldBe(TestSidecars.Postgres());
        exchange.Unused.ShouldBeEmpty();
    }

    // A "$2" that the SQL writes itself is typed by the server after the query's own: it is no parameter of the
    // query, and "types" is not asked for it.
    [Fact]
    public async Task DescribeAsync_MoreTypesThanParameters_TypesTheListAlone()
    {
        var script = new PostgresScript().Open();
        _ = Prepares(
            script,
            1,
            "SELECT 1 FROM users WHERE id = $1 AND name = $2",
            ["unknown"],
            new PostgresPrepareAnswer([23, 25], null)
        );
        _ = Resolves(script, [23], PostgresRows.Int4);
        var exchange = Frees(script, 1).Play();
        await using var session = await PostgresScript.SessionAsync(exchange);

        var described = await session.DescribeAsync(
            Request("SELECT 1 FROM users WHERE id = @id AND name = $2", Inferred("id")),
            TestContext.Current.CancellationToken
        );

        described.Value.ShouldNotBeNull().Parameters.ShouldHaveSingleItem().Name.ShouldBe("id");
        exchange.Unused.ShouldBeEmpty();
    }

    // A bug of the describer or of the server, and the statement is freed all the same.
    [Fact]
    public async Task DescribeAsync_FewerTypesThanParameters_ThrowsAndFreesTheStatement()
    {
        var script = new PostgresScript().Open();
        _ = Prepares(script, 1, "SELECT $1, $2", ["unknown", "unknown"], new PostgresPrepareAnswer([23], null));
        var exchange = Frees(script, 1).Play();
        await using var session = await PostgresScript.SessionAsync(exchange);

        _ = await Should.ThrowAsync<InvalidOperationException>(() =>
            session.DescribeAsync(
                Request("SELECT @a, @b", Inferred("a"), Inferred("b")),
                TestContext.Current.CancellationToken
            )
        );

        exchange.Unused.ShouldBeEmpty();
    }

    [Fact]
    public async Task DescribeAsync_InferredPseudoType_IsSqlsrc226NamingIt()
    {
        var script = new PostgresScript().Open();
        _ = Prepares(script, 1, "SELECT row_to_json($1)", ["unknown"], new PostgresPrepareAnswer([2249], null));
        _ = Resolves(script, [2249], PostgresRows.Record);
        var exchange = Frees(script, 1).Play();
        await using var session = await PostgresScript.SessionAsync(exchange);

        var described = await session.DescribeAsync(
            Request("SELECT row_to_json(@p)", Inferred("p")),
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
        _ = Prepares(script, 1, "SELECT $1", ["record"], new PostgresPrepareAnswer([2249], null));
        _ = Resolves(script, [2249], PostgresRows.Record);
        var exchange = Frees(script, 1).Play();
        await using var session = await PostgresScript.SessionAsync(exchange);

        var described = await session.DescribeAsync(
            Request("SELECT @p", Declared("p", "record")),
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
        _ = Prepares(script, 1, "SELECT $1::int", ["unknown"], new PostgresPrepareAnswer([23], null));
        _ = script.Answer("types", new PostgresTypesRequest([23]), new PostgresTypesAnswer(null, dropped));
        var exchange = Frees(script, 1, new PostgresDeallocateAnswer(dropped)).Play();
        await using var session = await PostgresScript.SessionAsync(exchange);

        var described = await session.DescribeAsync(
            Request("SELECT @p::int", Inferred("p")),
            TestContext.Current.CancellationToken
        );

        var failure = described.Failure.ShouldNotBeNull();
        failure.Descriptor.Id.ShouldBe("SQLSRC225");
        failure.Step.ShouldBe(DescribeStep.ResolveType);
        exchange.Unused.ShouldBeEmpty();
    }
}
