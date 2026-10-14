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
using static SqlSource.Tool.Tests.PostgresPlans;

namespace SqlSource.Tool.Tests.Describing.Postgres;

// The session over a replay the test writes: what it asks, in which order, and what it makes of each answer.  A
// call the script does not hold is a CaptureMissException, so a describe that asks more than its test wrote fails.
public class PostgresSessionTests
{
    private static DescribeRequest Request(string sql, params SqlQueryParameter[] parameters) =>
        new("Q", sql, new EquatableArray<SqlQueryParameter>([.. parameters]));

    private static SqlQueryParameter Inferred(string name) => new(name, null, null, false);

    private static SqlQueryParameter Declared(string name, string type) => new(name, type, null, true);

    // A column of an expression: it names no table.
    private static PostgresColumnRow Expression(
        int ordinal,
        string name,
        long typeOid,
        int? length = null,
        int? precision = null,
        int? scale = null
    ) => new(ordinal, name, typeOid, 0, 0, length, precision, scale);

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

    // "prepare" alone: for a describe that ends there, and for one whose columns the test writes.
    private static PostgresScript PreparesOnly(
        PostgresScript script,
        int number,
        string sql,
        string[] types,
        PostgresPrepareAnswer answer
    ) => script.Answer("prepare", new PostgresPrepareRequest($"sqlsource_{number}", sql, types), answer);

    // "prepare", and "columns" answering no column when the statement was prepared with a type for each parameter
    // of the list: most of these tests are about the parameters.
    private static PostgresScript Prepares(
        PostgresScript script,
        int number,
        string sql,
        string[] types,
        PostgresPrepareAnswer answer
    )
    {
        _ = PreparesOnly(script, number, sql, types, answer);
        return answer.ParameterTypes is { } resolved && resolved.Count >= types.Length
            ? Columns(script, number, types.Length)
            : script;
    }

    private static PostgresScript Columns(
        PostgresScript script,
        int number,
        int parameters,
        params PostgresColumnRow[] columns
    ) =>
        script.Answer(
            "columns",
            new PostgresColumnsRequest($"sqlsource_{number}", parameters),
            new PostgresColumnsAnswer(columns, null)
        );

    private static PostgresScript Frees(PostgresScript script, int number, PostgresDeallocateAnswer? answer = null) =>
        script.Answer("deallocate", new PostgresDeallocateRequest($"sqlsource_{number}"), answer ?? Freed);

    // "check" answering the columns the test gives, each with the type "columns" answered: the two describes agree.
    private static PostgresScript Checks(
        PostgresScript script,
        string sql,
        (string Name, string Type)[] parameters,
        params PostgresColumnRow[] columns
    ) =>
        script.Answer(
            "check",
            new PostgresCheckRequest(
                sql,
                [.. parameters.Select(static parameter => new PostgresCheckParameter(parameter.Name, parameter.Type))]
            ),
            new PostgresCheckAnswer(
                [.. columns.Select(static column => new PostgresCheckColumn(column.Name, column.TypeOid, "a type"))],
                null,
                null
            )
        );

    // "explain" for the statement, with no setting, answering what the test gives.
    private static PostgresScript Explains(
        PostgresScript script,
        int number,
        int parameters,
        PostgresExplainAnswer answer
    ) => script.Answer("explain", new PostgresExplainRequest($"sqlsource_{number}", parameters, []), answer);

    private static PostgresExplainAnswer PlanOf(PlanNode root) => new(Text(root), null);

    // The trailing ";" is not sent, the parameter is $1, and the statement is freed when the describe is done.
    [Fact]
    public async Task DescribeAsync_Query_AsksPrepareThenDeallocateForTheStatement()
    {
        var script = new PostgresScript().Open();
        _ = Prepares(script, 1, "DELETE FROM samples WHERE flag", [], new PostgresPrepareAnswer([], null));
        _ = Checks(script, "DELETE FROM samples WHERE flag", []);
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
        _ = Checks(script, "DELETE FROM samples", []);
        _ = Frees(script, 1);
        _ = Prepares(script, 2, "DELETE FROM orders", [], new PostgresPrepareAnswer([], null));
        _ = Checks(script, "DELETE FROM orders", []);
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
        _ = Checks(script, "DELETE FROM orders", []);
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
        _ = Checks(script, "DELETE FROM samples", []);
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

    private static PostgresScript Tables(PostgresScript script, long[] oids, params PostgresTableRow[] tables) =>
        script.Answer("tables", new PostgresTablesRequest(oids), new PostgresTablesAnswer(tables, null));

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
        _ = Checks(
            script,
            "SELECT id FROM users WHERE id = @id AND name = @name",
            [("id", "integer"), ("name", "character varying")]
        );
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
        _ = Checks(
            script,
            "UPDATE users SET name = @name WHERE id = @id",
            [("name", "character varying"), ("id", "integer"), ("tenant", "integer")]
        );
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
        _ = Checks(script, "SELECT 1 FROM users WHERE id = @id AND name = $2", [("id", "integer")]);
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

    [Fact]
    public async Task DescribeAsync_StatementWithColumns_IsRowsWithEachColumnsTypeAndFacets()
    {
        var script = new PostgresScript().Open();
        _ = PreparesOnly(script, 1, "SELECT 1, 'a'::varchar(100), 2::numeric(18,2)", [], new([], null));
        _ = Columns(
            script,
            1,
            0,
            Expression(0, "?column?", 23),
            Expression(1, "varchar", 1043, length: 100),
            Expression(2, "numeric", 1700, precision: 18, scale: 2)
        );
        _ = Resolves(script, [23, 1043, 1700], PostgresRows.Int4, PostgresRows.Varchar, PostgresRows.Numeric);
        _ = Checks(
            script,
            "SELECT 1, 'a'::varchar(100), 2::numeric(18,2)",
            [],
            Expression(0, "?column?", 23),
            Expression(1, "varchar", 1043),
            Expression(2, "numeric", 1700)
        );
        var exchange = Frees(script, 1).Play();
        await using var session = await PostgresScript.SessionAsync(exchange);

        var described = await session.DescribeAsync(
            Request("SELECT 1, 'a'::varchar(100), 2::numeric(18,2)"),
            TestContext.Current.CancellationToken
        );

        var description = described.Value.ShouldNotBeNull();
        description.ResultKind.ShouldBe(SidecarResultKind.Rows);
        description.MatchesTable.ShouldBeNull();
        description.Plan.ShouldBe(SidecarValues.Plan.NotNeeded);
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
        _ = Prepares(script, 1, "DELETE FROM samples WHERE id = $1", ["unknown"], new([23], null));
        _ = Resolves(script, [23], PostgresRows.Int4);
        _ = Checks(script, "DELETE FROM samples WHERE id = @id", [("id", "integer")]);
        var exchange = Frees(script, 1).Play();
        await using var session = await PostgresScript.SessionAsync(exchange);

        var described = await session.DescribeAsync(
            Request("DELETE FROM samples WHERE id = @id", Inferred("id")),
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
        _ = PreparesOnly(script, 1, "SELECT $1::text AS a, 1 AS b, 2 AS c", ["unknown"], new([25], null));
        _ = Columns(script, 1, 1, Expression(0, "a", 25), Expression(1, "b", 23), Expression(2, "c", 23));
        _ = Resolves(script, [23, 25], PostgresRows.Int4, PostgresRows.Text);
        _ = Checks(
            script,
            "SELECT @p::text AS a, 1 AS b, 2 AS c",
            [("p", "text")],
            Expression(0, "a", 25),
            Expression(1, "b", 23),
            Expression(2, "c", 23)
        );
        var exchange = Frees(script, 1).Play();
        await using var session = await PostgresScript.SessionAsync(exchange);

        var described = await session.DescribeAsync(
            Request("SELECT @p::text AS a, 1 AS b, 2 AS c", Inferred("p")),
            TestContext.Current.CancellationToken
        );

        var description = described.Value.ShouldNotBeNull();
        description
            .Parameters.ShouldHaveSingleItem()
            .Type.ShouldBe(TestSidecars.Postgres("text", internalName: "text"));
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
        _ = PreparesOnly(script, 1, "SELECT 1", [], new([], null));
        _ = script.Answer(
            "columns",
            new PostgresColumnsRequest("sqlsource_1", 0),
            new PostgresColumnsAnswer(null, dropped)
        );
        var exchange = Frees(script, 1).Play();
        await using var session = await PostgresScript.SessionAsync(exchange);

        var described = await session.DescribeAsync(Request("SELECT 1"), TestContext.Current.CancellationToken);

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
        _ = PreparesOnly(script, 1, "SELECT o.total, u.email, u.id FROM orders o, users u", [], new([], null));
        _ = Columns(
            script,
            1,
            0,
            PostgresTables.ColumnOf(0, "total", 1700, orders, 3, precision: 18, scale: 2),
            PostgresTables.ColumnOf(1, "email", 1043, users, 3, length: 254),
            PostgresTables.ColumnOf(2, "id", 23, users, 1)
        );
        _ = Tables(script, [users.Oid, orders.Oid], users, orders);
        _ = Resolves(
            script,
            [23, 1043, 1700, PostgresRows.EmailT.Oid],
            PostgresRows.Int4,
            PostgresRows.Varchar,
            PostgresRows.Numeric,
            PostgresRows.EmailT
        );
        _ = Checks(
            script,
            "SELECT o.total, u.email, u.id FROM orders o, users u",
            [],
            Expression(0, "total", 1700),
            Expression(1, "email", 1043),
            Expression(2, "id", 23)
        );
        var exchange = Frees(script, 1).Play();
        await using var session = await PostgresScript.SessionAsync(exchange);

        var described = await session.DescribeAsync(
            Request("SELECT o.total, u.email, u.id FROM orders o, users u"),
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
        _ = PreparesOnly(script, 1, "SELECT ctid FROM users", [], new([], null));
        _ = Columns(script, 1, 0, new PostgresColumnRow(0, "ctid", 27, users.Oid, -1, null, null, null));
        _ = Tables(script, [users.Oid], users);
        _ = Resolves(script, [27], PostgresRows.Tid);
        _ = Checks(script, "SELECT ctid FROM users", [], Expression(0, "ctid", 27));
        var exchange = Frees(script, 1).Play();
        await using var session = await PostgresScript.SessionAsync(exchange);

        var described = await session.DescribeAsync(
            Request("SELECT ctid FROM users"),
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
        _ = PreparesOnly(script, 1, "SELECT id FROM users", [], new([], null));
        _ = Columns(script, 1, 0, PostgresTables.ColumnOf(0, "id", 23, users, 1));
        _ = script.Answer("tables", new PostgresTablesRequest([users.Oid]), new PostgresTablesAnswer(null, refused));
        var exchange = Frees(script, 1).Play();
        await using var session = await PostgresScript.SessionAsync(exchange);

        var described = await session.DescribeAsync(
            Request("SELECT id FROM users"),
            TestContext.Current.CancellationToken
        );

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
        _ = PreparesOnly(script, 1, "SELECT id FROM users", [], new([], null));
        _ = Columns(script, 1, 0, PostgresTables.ColumnOf(0, "id", 23, users, 1));
        _ = Tables(script, [users.Oid]);
        var exchange = Frees(script, 1).Play();
        await using var session = await PostgresScript.SessionAsync(exchange);

        var described = await session.DescribeAsync(
            Request("SELECT id FROM users"),
            TestContext.Current.CancellationToken
        );

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
        _ = PreparesOnly(script, 1, "SELECT id FROM users", [], new([], null));
        _ = Columns(script, 1, 0, PostgresTables.ColumnOf(0, "id", 23, users, 1));
        _ = Tables(script, [users.Oid], PostgresTables.TableOf(users.Oid, "users"));
        var exchange = Frees(script, 1).Play();
        await using var session = await PostgresScript.SessionAsync(exchange);

        var described = await session.DescribeAsync(
            Request("SELECT id FROM users"),
            TestContext.Current.CancellationToken
        );

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
        _ = PreparesOnly(script, 1, "SELECT 1 AS n", [], new([], null));
        _ = Columns(script, 1, 0, Expression(0, "n", 23));
        _ = Resolves(script, [23]);
        var exchange = Frees(script, 1).Play();
        await using var session = await PostgresScript.SessionAsync(exchange);

        var described = await session.DescribeAsync(Request("SELECT 1 AS n"), TestContext.Current.CancellationToken);

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
        _ = Prepares(script, 1, "SELECT 1 WHERE $1 = 1", ["unknown"], new PostgresPrepareAnswer([23], null));
        _ = Resolves(script, [23]);
        var exchange = Frees(script, 1).Play();
        await using var session = await PostgresScript.SessionAsync(exchange);

        var described = await session.DescribeAsync(
            Request("SELECT 1 WHERE @id = 1", Inferred("id")),
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
        _ = PreparesOnly(script, 1, "SELECT email FROM users", [], new([], null));
        _ = Columns(script, 1, 0, PostgresTables.ColumnOf(0, "email", 1043, users, 3, length: 254));
        _ = Tables(script, [users.Oid], users);
        _ = Resolves(script, [1043, PostgresRows.EmailT.Oid], PostgresRows.Varchar);
        var exchange = Frees(script, 1).Play();
        await using var session = await PostgresScript.SessionAsync(exchange);

        var described = await session.DescribeAsync(
            Request("SELECT email FROM users"),
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
        _ = PreparesOnly(script, 1, sql, [], new([], null));
        _ = Columns(script, 1, 0, PostgresTables.ColumnOf(0, "id", 23, users, 1), Expression(1, "n", 20));
        _ = Tables(script, [users.Oid], users);
        _ = Resolves(script, [20, 23], PostgresRows.Int8, PostgresRows.Int4);
        _ = Checks(script, sql, [], Expression(0, "id", 23), Expression(1, "n", 20));
        if (explain is not null)
        {
            _ = Explains(script, 1, 0, explain);
        }

        var exchange = Frees(script, 1).Play();
        await using var session = await PostgresScript.SessionAsync(exchange);

        var described = await session.DescribeAsync(Request(sql), TestContext.Current.CancellationToken);

        exchange.Unused.ShouldBeEmpty();
        return described.Value.ShouldNotBeNull();
    }

    [Fact]
    public async Task DescribeAsync_QueryWithoutAGateWord_TakesNullabilityFromTheCatalogAndNeedsNoPlan()
    {
        var description = await DescribeUsersIdAsync("SELECT id, count(*) OVER () AS n FROM users");

        description.Plan.ShouldBe(SidecarValues.Plan.NotNeeded);
        description
            .Columns.ShouldNotBeNull()
            .Select(column => (column.Nullable, column.NullableSource))
            .ShouldBe([(false, "catalog"), (null, "no-origin")]);
    }

    private const string GatedUsersId =
        "SELECT u.id, count(*) OVER () AS n FROM users u LEFT JOIN orders o ON o.user_id = u.id";

    private static (bool? Nullable, string? Source)[] AnswersOf(QueryDescription description) =>
        [.. description.Columns.ShouldNotBeNull().Select(static column => (column.Nullable, column.NullableSource))];

    // users is the side the join keeps: its NOT NULL column stays so, by the catalog.
    [Fact]
    public async Task DescribeAsync_GatedQueryWhoseJoinKeepsTheColumnsSide_TakesTheCatalogsAnswerAndIsWalked()
    {
        var plan = Join("Left", Scan("u"), Scan("o")) with { Output = ["u.id", "count(*) OVER (?)"] };

        var description = await DescribeUsersIdAsync(GatedUsersId, PlanOf(plan));

        description.Plan.ShouldBe(SidecarValues.Plan.Walked);
        AnswersOf(description).ShouldBe([(false, "catalog"), (null, "no-origin")]);
    }

    // The same query planned with users as the side the join null-extends.
    [Fact]
    public async Task DescribeAsync_GatedQueryWhoseJoinExtendsTheColumnsSide_IsNullableByTheOuterJoin()
    {
        var plan = Join("Right", Scan("u"), Scan("o")) with { Output = ["u.id", "count(*) OVER (?)"] };

        var description = await DescribeUsersIdAsync(GatedUsersId, PlanOf(plan));

        description.Plan.ShouldBe(SidecarValues.Plan.Walked);
        AnswersOf(description).ShouldBe([(true, "outer-join"), (null, "no-origin")]);
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
        AnswersOf(description).ShouldBe([(true, "heuristic"), (null, "no-origin")]);
    }

    // A check that fails ends the describe before the plan is asked for: the script holds no "explain".
    [Fact]
    public async Task DescribeAsync_GatedQueryWhoseCheckDiffers_IsTheFailureAndAsksNoPlan()
    {
        var users = PostgresTables.Users;
        var script = new PostgresScript().Open();
        _ = PreparesOnly(script, 1, GatedUsersId, [], new([], null));
        _ = Columns(script, 1, 0, PostgresTables.ColumnOf(0, "id", 23, users, 1), Expression(1, "n", 20));
        _ = Tables(script, [users.Oid], users);
        _ = Resolves(script, [20, 23], PostgresRows.Int8, PostgresRows.Int4);
        _ = Checks(script, GatedUsersId, [], Expression(0, "id", 23));
        var exchange = Frees(script, 1).Play();
        await using var session = await PostgresScript.SessionAsync(exchange);

        var described = await session.DescribeAsync(Request(GatedUsersId), TestContext.Current.CancellationToken);

        described.Failure.ShouldNotBeNull().Descriptor.Id.ShouldBe("SQLSRC231");
        exchange.Unused.ShouldBeEmpty();
    }

    // The plan is asked with a NULL for each parameter of the query's list, as "columns" is.
    [Fact]
    public async Task DescribeAsync_GatedQueryWithAParameter_AsksThePlanWithTheNumberOfItsParameters()
    {
        var sql = "SELECT u.id FROM users u LEFT JOIN orders o ON o.user_id = u.id WHERE u.id = @id";
        var users = PostgresTables.Users;
        var id = PostgresTables.ColumnOf(0, "id", 23, users, 1);
        var script = new PostgresScript().Open();
        _ = PreparesOnly(
            script,
            1,
            "SELECT u.id FROM users u LEFT JOIN orders o ON o.user_id = u.id WHERE u.id = $1",
            ["unknown"],
            new([23], null)
        );
        _ = Columns(script, 1, 1, id);
        _ = Tables(script, [users.Oid], users);
        _ = Resolves(script, [23], PostgresRows.Int4);
        _ = Checks(script, sql, [("id", "integer")], Expression(0, "id", 23));
        _ = Explains(script, 1, 1, PlanOf(Join("Left", Scan("u"), Scan("o")) with { Output = ["u.id"] }));
        var exchange = Frees(script, 1).Play();
        await using var session = await PostgresScript.SessionAsync(exchange);

        var described = await session.DescribeAsync(
            Request(sql, Inferred("id")),
            TestContext.Current.CancellationToken
        );

        exchange.Unused.ShouldBeEmpty();
        AnswersOf(described.Value.ShouldNotBeNull()).ShouldBe([(false, "catalog")]);
    }

    // A statement without rows has no provenance and asks for no plan, gate word or not.
    [Fact]
    public async Task DescribeAsync_GatedStatementWithoutRows_HasNoPlan()
    {
        var script = new PostgresScript().Open();
        _ = Prepares(script, 1, "DELETE FROM users WHERE left(name, 1) = 'a'", [], new([], null));
        _ = Checks(script, "DELETE FROM users WHERE left(name, 1) = 'a'", []);
        var exchange = Frees(script, 1).Play();
        await using var session = await PostgresScript.SessionAsync(exchange);

        var described = await session.DescribeAsync(
            Request("DELETE FROM users WHERE left(name, 1) = 'a'"),
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
        _ = PreparesOnly(script, 1, sql, [], new([], null));
        _ = Columns(
            script,
            1,
            0,
            PostgresTables.ColumnOf(0, "id", 23, Labels, 1),
            PostgresTables.ColumnOf(1, second, 25, Labels, 2)
        );
        _ = Tables(script, [Labels.Oid], Labels);
        _ = Resolves(script, [23, 25], PostgresRows.Int4, PostgresRows.Text);
        _ = Checks(script, sql, [], Expression(0, "id", 23), Expression(1, second, 25));
        if (explain is not null)
        {
            _ = Explains(script, 1, 0, explain);
        }

        var exchange = Frees(script, 1).Play();
        await using var session = await PostgresScript.SessionAsync(exchange);

        var described = await session.DescribeAsync(Request(sql), TestContext.Current.CancellationToken);

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
            explain: PlanOf(plan)
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
            explain: PlanOf(plan)
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
        _ = PreparesOnly(script, 1, "SELECT 1 AS one, ROW(1, 2) AS pair, ROW(3) AS other", [], new([], null));
        _ = Columns(
            script,
            1,
            0,
            Expression(0, "one", 23),
            Expression(1, "pair", pseudo.Oid),
            Expression(2, "other", pseudo.Oid)
        );

        // The request names the types of the columns; the closure the server answers holds an array's element too.
        PostgresTypeRow[] closure = array
            ? [PostgresRows.Int4, PostgresRows.Record, pseudo]
            : [PostgresRows.Int4, pseudo];
        _ = Resolves(script, [23, pseudo.Oid], closure);
        var exchange = Frees(script, 1).Play();
        await using var session = await PostgresScript.SessionAsync(exchange);

        var described = await session.DescribeAsync(
            Request("SELECT 1 AS one, ROW(1, 2) AS pair, ROW(3) AS other"),
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
        _ = PreparesOnly(script, 1, "SELECT $1", ["record"], new([2249], null));
        _ = Columns(script, 1, 1, Expression(0, "?column?", 2249));
        _ = Resolves(script, [2249], PostgresRows.Record);
        var exchange = Frees(script, 1).Play();
        await using var session = await PostgresScript.SessionAsync(exchange);

        var described = await session.DescribeAsync(
            Request("SELECT @p", Declared("p", "record")),
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
        _ = PreparesOnly(script, 1, "SELECT $1::int AS a, 'x' AS b", ["unknown"], new([23], null));
        _ = Columns(script, 1, 1, Expression(0, "a", 23), Expression(1, "b", 25));
        _ = Resolves(script, [23, 25], PostgresRows.Int4, PostgresRows.Text);
        _ = Checks(script, "SELECT @p::int AS a, 'x' AS b", [("p", "integer")], Expression(0, "a", 23));
        var exchange = Frees(script, 1).Play();
        await using var session = await PostgresScript.SessionAsync(exchange);

        var described = await session.DescribeAsync(
            Request("SELECT @p::int AS a, 'x' AS b;", Inferred("p")),
            TestContext.Current.CancellationToken
        );

        described.Value.ShouldBeNull();
        var failure = described.Failure.ShouldNotBeNull();
        failure.Descriptor.Id.ShouldBe("SQLSRC231");
        failure.Arguments.ShouldBe(["Q", "the result has 1 columns for Npgsql and 2 for SqlSource"]);
        failure.Step.ShouldBe(DescribeStep.CheckParameters);
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
        _ = Prepares(script, 1, "DELETE FROM samples WHERE id = $1", ["unknown"], new([23], null));
        _ = Resolves(script, [23], PostgresRows.Int4);
        _ = script.Answer(
            "check",
            new PostgresCheckRequest("DELETE FROM samples WHERE id = @id", [new("id", "integer")]),
            new PostgresCheckAnswer(null, refused, null)
        );
        var exchange = Frees(script, 1).Play();
        await using var session = await PostgresScript.SessionAsync(exchange);

        var described = await session.DescribeAsync(
            Request("DELETE FROM samples WHERE id = @id", Inferred("id")),
            TestContext.Current.CancellationToken
        );

        var failure = described.Failure.ShouldNotBeNull();
        failure.Descriptor.Id.ShouldBe("SQLSRC231");
        failure.Arguments.ShouldBe(["Q", refused.Message]);
        failure.ServerLines.ShouldBe([refused.Message]);
        exchange.Unused.ShouldBeEmpty();
    }

    // The parameter is the second of the list, and the type is the name the describer spelled for it.
    [Fact]
    public async Task DescribeAsync_CheckWithAParameterNpgsqlCannotBind_IsSqlsrc232NamingIt()
    {
        var script = new PostgresScript().Open();
        _ = Prepares(
            script,
            1,
            "DELETE FROM t WHERE a = $1 AND b = $2",
            ["unknown", "d2"],
            new([23, PostgresRows.D2.Oid], null)
        );

        // The request names the two types of the parameters; the closure holds the bases of the domain too.
        _ = Resolves(
            script,
            [23, PostgresRows.D2.Oid],
            PostgresRows.Int4,
            PostgresRows.Varchar,
            PostgresRows.EmailT,
            PostgresRows.D2
        );
        _ = script.Answer(
            "check",
            new PostgresCheckRequest(
                "DELETE FROM t WHERE a = @a AND b = @b",
                [new("a", "integer"), new("b", "public.d2")]
            ),
            new PostgresCheckAnswer(null, null, new PostgresBindError("b", "Writing is not supported"))
        );
        var exchange = Frees(script, 1).Play();
        await using var session = await PostgresScript.SessionAsync(exchange);

        var described = await session.DescribeAsync(
            Request("DELETE FROM t WHERE a = @a AND b = @b", Inferred("a"), Declared("b", "d2")),
            TestContext.Current.CancellationToken
        );

        var failure = described.Failure.ShouldNotBeNull();
        failure.Descriptor.Id.ShouldBe("SQLSRC232");
        failure.Arguments.ShouldBe(["public.d2", "@b", "Q"]);
        failure.ServerLines.ShouldBe(["Writing is not supported"]);
        failure.Help.ShouldBe(PostgresErrors.BindHelp);
        exchange.Unused.ShouldBeEmpty();
    }
}
