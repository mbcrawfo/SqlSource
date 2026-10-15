using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;
using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Tests.Snapshot;
using SqlSource.Tool;
using SqlSource.Tool.Describing.Capture;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Describing.Postgres;
using Xunit;

namespace SqlSource.Tests.Postgres.Describer;

// A query's parameters against a real server.  Every describe here runs in a session of its own and ends with no
// prepared statement left in it.
[Collection(UsesPostgres.Name)]
public sealed class ParameterTests(PostgresContainer postgres)
{
    private Task<Outcome<QueryDescription, DescribeFailure>> DescribeAsync(
        DescribeRequest request,
        IDescribeExchange? exchange = null
    ) => Sessions.DescribeAsync(postgres.ConnectionString, request, exchange);

    // A table of these tests alone, which a describe must leave as it is.  The fixture schema has none of the name.
    private const string Canary = "describer_canary";

    private static string[] MethodsOf(DescribeCapture capture) => [.. capture.Calls.Select(call => call.Method)];

    [Fact]
    public async Task Describe_TwoStatements_IsSqlsrc230AndSendsNothing()
    {
        var capture = new DescribeCapture("postgres");

        var described = await DescribeAsync(
            Requests.Of("DELETE FROM orders WHERE user_id = @id; DELETE FROM users WHERE id = @id"),
            new CapturingExchange(LiveExchange.Instance, capture)
        );

        described.Failure.ShouldNotBeNull().Descriptor.Id.ShouldBe("SQLSRC230");
        MethodsOf(capture).ShouldBe(["open"]);
    }

    // PREPARE takes none of these, and SELECT ... INTO only from 15: the tool refuses each on every server, and asks
    // the server nothing.
    [Theory]
    [InlineData("CALL p(@a)", "CALL")]
    [InlineData("CREATE TEMP TABLE t AS SELECT @a::int", "CREATE")]
    [InlineData("SELECT @a::int AS a INTO TEMP t", "SELECT INTO")]
    [InlineData("SELECT 1. INTO TEMP t", "SELECT INTO")]
    [InlineData("(SELECT 1 INTO TEMP t)", "SELECT INTO")]
    public async Task Describe_StatementPrepareDoesNotTake_IsSqlsrc233AndSendsNothing(string sql, string kind)
    {
        var capture = new DescribeCapture("postgres");

        var described = await DescribeAsync(Requests.Of(sql), new CapturingExchange(LiveExchange.Instance, capture));

        var failure = described.Failure.ShouldNotBeNull();
        failure.Descriptor.Id.ShouldBe("SQLSRC233");
        failure.Arguments.ShouldBe([Requests.Query, kind]);
        MethodsOf(capture).ShouldBe(["open"]);
    }

    [Fact]
    public async Task Describe_DeclaredTypeThatIsNoTypeName_IsSqlsrc228AndSendsNothing()
    {
        var capture = new DescribeCapture("postgres");

        var described = await DescribeAsync(
            Requests.Of("SELECT 1 FROM users WHERE id = @id", ("id", "int) AS SELECT 1; DROP TABLE users; --")),
            new CapturingExchange(LiveExchange.Instance, capture)
        );

        var failure = described.Failure.ShouldNotBeNull();
        failure.Descriptor.Id.ShouldBe("SQLSRC228");
        failure.Arguments.ShouldBe([
            "int) AS SELECT 1; DROP TABLE users; --",
            "@id",
            Requests.Query,
            PostgresErrors.NotATypeName,
        ]);
        MethodsOf(capture).ShouldBe(["open"]);
    }

    // The live call itself, with a text no describe can bring it: ParameterPlaceholders.Rewrite refuses such a text
    // first, SQLSRC230.  The PREPARE is sent as one command that the driver does not split, so the server refuses the
    // whole of it and the table of the second statement is still there.
    [Fact]
    public async Task PrepareAsync_TextOfTwoStatements_AnswersTheServersErrorAndRunsNothing()
    {
        var token = TestContext.Current.CancellationToken;
        await using var own = NpgsqlDataSource.Create(postgres.ConnectionString);
        await using (var create = own.CreateCommand($"CREATE TABLE {Canary} (id int)"))
        {
            _ = await create.ExecuteNonQueryAsync(token);
        }

        try
        {
            var opened = await Sessions.OpenAsync(postgres.ConnectionString);
            await using var session = opened.Value.ShouldBeOfType<PostgresSession>();
            var live = new PostgresLive(session.Connection);

            var answer = await live.PrepareAsync(
                new PostgresPrepareRequest("sqlsource_canary", $"SELECT 1; DROP TABLE {Canary}", []),
                token
            );

            await using var find = own.CreateCommand($"SELECT pg_catalog.to_regclass('{Canary}') IS NOT NULL");
            (await find.ExecuteScalarAsync(token)).ShouldBe(true, "the call ran the second statement");
            answer.ParameterTypes.ShouldBeNull();
            var error = answer.Error.ShouldNotBeNull();
            error.SqlState.ShouldBe("42601");
            error.Message.ShouldBe("cannot insert multiple commands into a prepared statement");
        }
        finally
        {
            await using var drop = own.CreateCommand($"DROP TABLE IF EXISTS {Canary}");
            _ = await drop.ExecuteNonQueryAsync(token);
        }
    }

    private async Task<DescribeFailure> ShouldFailAsync(DescribeRequest request, string id)
    {
        var described = await DescribeAsync(request);

        described.Value.ShouldBeNull();
        var failure = described.Failure.ShouldNotBeNull();
        failure.Descriptor.Id.ShouldBe(id);
        failure.Step.ShouldBe(DescribeStep.DescribeParameters);
        return failure;
    }

    // The server checks a declared type against every use of its parameter.  The hint's words differ by version.
    [Fact]
    public async Task Describe_DeclaredTypeThatAUseRejects_IsSqlsrc225WithTheServersLines()
    {
        var failure = await ShouldFailAsync(
            Requests.Of("SELECT 1 FROM users WHERE name = @p", ("p", "int")),
            "SQLSRC225"
        );

        failure.Arguments.ShouldBe([Requests.Query, "operator does not exist: character varying = integer"]);
        failure.ServerLines.Count.ShouldBe(3);
        failure.ServerLines[0].ShouldBe("42883: operator does not exist: character varying = integer");
        failure.ServerLines[1].ShouldStartWith("hint: ");
        failure.ServerLines[2].ShouldBe("at character 32");
        failure.Help.ShouldBeNull();
    }

    [Fact]
    public async Task Describe_ParameterThatNoUseTypes_IsSqlsrc226()
    {
        var failure = await ShouldFailAsync(Requests.Of("SELECT @p IS NULL"), "SQLSRC226");

        failure.Arguments.ShouldBe(["@p", Requests.Query]);
        failure.ServerLines.ShouldBe(["42P18: could not determine data type of parameter $1"]);
    }

    [Fact]
    public async Task Describe_FirstUseThatDoesNotFixTheType_IsSqlsrc227AtTheUse()
    {
        var failure = await ShouldFailAsync(
            Requests.Of("SELECT 1 FROM users WHERE (@name IS NULL OR name = @name)"),
            "SQLSRC227"
        );

        failure.Arguments.ShouldBe(["@name", Requests.Query]);
        failure.ServerLines.ShouldBe(["42P08: could not determine data type of parameter $1", "at character 28"]);
    }

    [Theory]
    [InlineData("SELECT 1 FROM users WHERE (name = @name OR @name IS NULL)", null)]
    [InlineData("SELECT 1 FROM users WHERE (@name IS NULL OR name = @name)", "varchar")]
    public async Task Describe_OptionalFilterWithTheTypingUseFirstOrADeclaration_IsDescribed(string sql, string? type)
    {
        var request = type is null ? Requests.Of(sql) : Requests.Of(sql, ("name", type));

        _ = (await DescribeAsync(request)).Value.ShouldNotBeNull();
    }

    [Fact]
    public async Task Describe_DeclaredTypeTheDatabaseLacks_IsSqlsrc228NamingItAndItsParameter()
    {
        var failure = await ShouldFailAsync(
            Requests.Of("SELECT 1 FROM users WHERE id = @id", ("p", "nope_t")),
            "SQLSRC228"
        );

        failure.Arguments.ShouldBe(["nope_t", "@p", Requests.Query, PostgresErrors.NoSuchType]);
        failure.ServerLines.ShouldBe(["42704: type \"nope_t\" does not exist"]);
    }

    // The position is in the user's SQL: the PREPARE around it is not counted.
    [Theory]
    [InlineData("SELEC 1 WHERE @p = 1", "42601: syntax error at or near \"SELEC\"", "at character 1")]
    [InlineData("SELECT 1 FROM nope WHERE id = @id", "42P01: relation \"nope\" does not exist", "at character 15")]
    [InlineData(
        "SELECT 'hé\U0001F600' FROM nope WHERE id = @id",
        "42P01: relation \"nope\" does not exist",
        "at character 19"
    )]
    public async Task Describe_QueryTheServerRejects_IsSqlsrc225AtTheCharacterOfTheUsersSql(
        string sql,
        string message,
        string position
    )
    {
        var failure = await ShouldFailAsync(Requests.Of(sql), "SQLSRC225");

        failure.ServerLines.ShouldBe([message, position]);
    }

    // A rejected PREPARE leaves no statement, and the session goes on with the next name.
    [Fact]
    public async Task Describe_AfterAQueryTheServerRejected_DescribesTheNextOne()
    {
        var capture = new DescribeCapture("postgres");
        var opened = await Sessions.OpenAsync(
            postgres.ConnectionString,
            new CapturingExchange(LiveExchange.Instance, capture)
        );
        await using var session = opened.Value.ShouldBeOfType<PostgresSession>();
        var token = TestContext.Current.CancellationToken;

        var first = await session.DescribeAsync(Requests.Of("DELETE FROM nope WHERE id = @id"), token);
        var second = await session.DescribeAsync(Requests.Of("DELETE FROM users WHERE id = @id"), token);

        first.Failure.ShouldNotBeNull().Descriptor.Id.ShouldBe("SQLSRC225");
        _ = second.Value.ShouldNotBeNull();
        MethodsOf(capture)
            .Where(method => method is "prepare" or "deallocate")
            .ShouldBe(["prepare", "prepare", "deallocate"]);
        CaptureJson.Write(capture).ShouldContain("\"statement\": \"sqlsource_2\"");
        (await Sessions.PreparedStatementsAsync(session)).ShouldBe(0);
    }

    // A run that is cancelled ends as cancelled, and never as a failure that quotes the driver.
    [Fact]
    public async Task Describe_CancelledToken_ThrowsAndIsNoFailure()
    {
        var opened = await Sessions.OpenAsync(postgres.ConnectionString);
        await using var session = opened.Value.ShouldNotBeNull();
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(() =>
            session.DescribeAsync(Requests.Of("DELETE FROM users WHERE id = @id"), cancelled.Token)
        );
    }

    [Fact]
    public async Task Describe_StatementWithATrailingSemicolonAndNoParameter_IsDescribed() =>
        (await DescribeAsync(Requests.Of("DELETE FROM samples WHERE flag; -- all of them"))).Value.ShouldNotBeNull();

    private static readonly PostgresType CharacterVarying = TestSidecars.Postgres(
        "character varying",
        internalName: "varchar"
    );

    // The domain's own type has no modifier, as a parameter's never has: its base has the domain's, varchar(254).
    private static readonly PostgresType EmailT = TestSidecars.Postgres(
        "public.email_t",
        SidecarValues.Kind.Domain,
        "public",
        "email_t",
        baseType: TestSidecars.Postgres("character varying(254)", internalName: "varchar", length: 254)
    );

    private static readonly PostgresType UserStatus = TestSidecars.Postgres(
        "public.user_status",
        SidecarValues.Kind.Enum,
        "public",
        "user_status",
        labels: ["active", "disabled"]
    );

    private static readonly PostgresType TextArray = TestSidecars.Postgres(
        "text[]",
        SidecarValues.Kind.Array,
        internalName: "_text",
        element: TestSidecars.Postgres("text", internalName: "text")
    );

    // The parameters of a query that is described.  Its columns are ColumnTests' business.
    private async Task<EquatableArray<DescribedParameter>> ShouldDescribeAsync(DescribeRequest request)
    {
        var described = await DescribeAsync(request);

        described.Failure.ShouldBeNull(described.Failure?.Descriptor.Id);
        return described.Value.ShouldNotBeNull().Parameters;
    }

    // Each parameter's type of these tests binds by its name: NpgsqlCheckTests has the types that bind by another.
    private static DescribedParameter Inferred(string name, PostgresType type) =>
        new(name, type with { BindName = type.Name }, SidecarValues.TypeSource.Inferred);

    private static DescribedParameter Declared(string name, PostgresType type) =>
        new(name, type with { BindName = type.Name }, SidecarValues.TypeSource.Declared);

    // The server gives a parameter the type of the column it is compared with.
    [Fact]
    public async Task Describe_ParametersComparedWithColumns_AreInferredFromThem()
    {
        var parameters = await ShouldDescribeAsync(
            Requests.Of("SELECT id FROM users WHERE id = @id AND status = @status")
        );

        parameters.ShouldBe([Inferred("id", TestSidecars.Postgres()), Inferred("status", UserStatus)]);
    }

    // An assignment gives a parameter the column's own type, so every base type of the schema is reached through
    // one.  A parameter has no facets: numeric(18,2) is numeric and char(2) is bpchar.
    [Theory]
    [InlineData("samples", "id", "integer", "int4")]
    [InlineData("samples", "flag", "boolean", "bool")]
    [InlineData("samples", "small", "smallint", "int2")]
    [InlineData("samples", "big", "bigint", "int8")]
    [InlineData("samples", "ratio", "real", "float4")]
    [InlineData("samples", "amount", "double precision", "float8")]
    [InlineData("samples", "code", "bpchar", "bpchar")]
    [InlineData("samples", "label", "text", "text")]
    [InlineData("samples", "born", "date", "date")]
    [InlineData("samples", "at_time", "time without time zone", "time")]
    [InlineData("samples", "at_local", "timestamp without time zone", "timestamp")]
    [InlineData("samples", "span", "interval", "interval")]
    [InlineData("samples", "token", "uuid", "uuid")]
    [InlineData("samples", "payload", "bytea", "bytea")]
    [InlineData("samples", "document", "jsonb", "jsonb")]
    [InlineData("users", "name", "character varying", "varchar")]
    [InlineData("users", "balance", "numeric", "numeric")]
    [InlineData("users", "created_at", "timestamp with time zone", "timestamptz")]
    public async Task Describe_BaseTypeInferredFromAnAssignment_IsSpelledAsTheServerSpellsIt(
        string table,
        string column,
        string name,
        string internalName
    )
    {
        var parameters = await ShouldDescribeAsync(Requests.Of($"UPDATE {table} SET {column} = @p"));

        parameters.ShouldBe([Inferred("p", TestSidecars.Postgres(name, internalName: internalName))]);
    }

    // A parameter keeps its domain; a column is flattened to its base by the protocol.
    [Fact]
    public async Task Describe_DomainEnumArrayRangeAndMultirangeInferred_HaveTheirParts()
    {
        var users = await ShouldDescribeAsync(
            Requests.Of("UPDATE users SET email = @email, status = @status, tags = @tags")
        );
        var bookings = await ShouldDescribeAsync(Requests.Of("UPDATE bookings SET during = @during, slots = @slots"));

        users.ShouldBe([Inferred("email", EmailT), Inferred("status", UserStatus), Inferred("tags", TextArray)]);
        bookings.ShouldBe([
            Inferred(
                "during",
                TestSidecars.Postgres(
                    "tstzrange",
                    SidecarValues.Kind.Range,
                    internalName: "tstzrange",
                    subtype: TestSidecars.Postgres("timestamp with time zone", internalName: "timestamptz")
                )
            ),
            Inferred(
                "slots",
                TestSidecars.Postgres(
                    "int4multirange",
                    SidecarValues.Kind.Multirange,
                    internalName: "int4multirange",
                    subtype: TestSidecars.Postgres()
                )
            ),
        ]);
    }

    // A comparison with a varchar or a domain column infers text: the operator is text's.  The last is the
    // optional filter with the use that fixes the type first.
    [Theory]
    [InlineData("SELECT 1 FROM users WHERE name = @p")]
    [InlineData("SELECT 1 FROM users WHERE email = @p")]
    [InlineData("SELECT 1 FROM users WHERE (name = @p OR @p IS NULL)")]
    public async Task Describe_ParameterComparedWithATextualColumn_IsText(string sql) =>
        (await ShouldDescribeAsync(Requests.Of(sql))).ShouldBe([
            Inferred("p", TestSidecars.Postgres("text", internalName: "text")),
        ]);

    // The server parses each declared type: int is integer, varchar(50) loses its length, a domain stays a domain.
    [Fact]
    public async Task Describe_DeclaredTypes_AreResolvedByTheServer()
    {
        var parameters = await ShouldDescribeAsync(
            Requests.Of(
                "SELECT @a, @b, @c, @d, @e",
                ("a", "int"),
                ("b", "varchar(50)"),
                ("c", "public.user_status"),
                ("d", "email_t"),
                ("e", "text[]")
            )
        );

        parameters.ShouldBe([
            Declared("a", TestSidecars.Postgres()),
            Declared("b", CharacterVarying),
            Declared("c", UserStatus),
            Declared("d", EmailT),
            Declared("e", TextArray),
        ]);
    }

    // The domain's own type has no modifier, and its base has the domain's, decoded into a precision and a scale.
    // email_t's base, with its length, is in the tests of declared and inferred domains above.
    [Fact]
    public async Task Describe_ParameterOfADomain_HasItsBaseWithTheDomainsModifier() =>
        (await ShouldDescribeAsync(Requests.Of("UPDATE modifiers SET amount = @amount"))).ShouldBe([
            Inferred(
                "amount",
                TestSidecars.Postgres(
                    "public.money_t",
                    SidecarValues.Kind.Domain,
                    "public",
                    "money_t",
                    baseType: TestSidecars.Postgres("numeric(10,2)", internalName: "numeric", precision: 10, scale: 2)
                )
            ),
        ]);

    [Fact]
    public async Task Describe_OptionalFilterWithADeclaration_IsTypedAsDeclared() =>
        (
            await ShouldDescribeAsync(
                Requests.Of("SELECT 1 FROM users WHERE (@name IS NULL OR name = @name)", ("name", "varchar"))
            )
        ).ShouldBe([Declared("name", CharacterVarying)]);

    // The declared parameter is not in the SQL: it stands at the end of the list, and the server accepts a list
    // longer than the placeholders used.
    [Fact]
    public async Task Describe_ParameterThatOnlyAMarkerDeclares_IsTypedAsDeclared() =>
        (
            await ShouldDescribeAsync(Requests.Of("DELETE FROM users WHERE status = @status", ("tenant", "int")))
        ).ShouldBe([Inferred("status", UserStatus), Declared("tenant", TestSidecars.Postgres())]);

    [Fact]
    public async Task Describe_ParameterUsedTwice_IsOneParameter() =>
        (await ShouldDescribeAsync(Requests.Of("SELECT 1 FROM users WHERE id = @id OR id = @ID + 1"))).ShouldBe([
            Inferred("id", TestSidecars.Postgres()),
        ]);

    // They carry the category A and an element, and they are no arrays.  A describe passes only when Npgsql binds
    // the name, so that it succeeds proves that it does.
    [Theory]
    [InlineData("SELECT 1 FROM pg_index WHERE indkey = @k", "k", "int2vector")]
    [InlineData("SELECT 1 FROM pg_proc WHERE proargtypes = @a", "a", "oidvector")]
    public async Task Describe_ParameterComparedWithInt2vectorOrOidvector_IsABaseType(
        string sql,
        string name,
        string type
    ) =>
        (await ShouldDescribeAsync(Requests.Of(sql))).ShouldBe([
            Inferred(name, TestSidecars.Postgres(type, internalName: type)),
        ]);

    // The name is spelled bare, and so is the name it binds by: Npgsql takes user.status bare and refuses it quoted.
    // The describe succeeds only when Npgsql binds the name.
    [Fact]
    public async Task Describe_TypeOfASchemaNamedByAReservedWord_IsSpelledBare()
    {
        var token = TestContext.Current.CancellationToken;
        await using var own = NpgsqlDataSource.Create(postgres.ConnectionString);
        await using (var create = own.CreateCommand("CREATE SCHEMA \"user\""))
        {
            _ = await create.ExecuteNonQueryAsync(token);
        }

        try
        {
            await using (var type = own.CreateCommand("CREATE TYPE \"user\".status AS ENUM ('a')"))
            {
                _ = await type.ExecuteNonQueryAsync(token);
            }

            var parameters = await ShouldDescribeAsync(Requests.Of("""SELECT 1 WHERE @s = 'a'::"user".status"""));

            parameters.ShouldBe([
                Inferred(
                    "s",
                    TestSidecars.Postgres("user.status", SidecarValues.Kind.Enum, "user", "status", labels: ["a"])
                ),
            ]);
        }
        finally
        {
            await using var drop = own.CreateCommand("""DROP SCHEMA IF EXISTS "user" CASCADE""");
            _ = await drop.ExecuteNonQueryAsync(token);
        }
    }

    // The server accepts a pseudo-type, declared or inferred, and an array of one.  A .sql file can cause each, so
    // none throws.
    [Theory]
    [InlineData("SELECT row_to_json(@p)", null, "SQLSRC226")]
    [InlineData("SELECT int4in(@p)", null, "SQLSRC226")]
    [InlineData("SELECT @p", "record", "SQLSRC228")]
    [InlineData("SELECT @p", "anyelement", "SQLSRC228")]
    [InlineData("SELECT @p", "cstring[]", "SQLSRC228")]
    public async Task Describe_ParameterOfAPseudoType_IsAFailureAndNoThrow(string sql, string? declared, string id)
    {
        var request = declared is null ? Requests.Of(sql) : Requests.Of(sql, ("p", declared));

        var described = await DescribeAsync(request);

        var failure = described.Failure.ShouldNotBeNull();
        failure.Descriptor.Id.ShouldBe(id);
        failure.Step.ShouldBe(DescribeStep.ResolveType);
        failure.ServerLines.ShouldBeEmpty();
    }

    // "$2" is no parameter of SqlSource, and generated code binds by name: the tool refuses it, and asks the server
    // nothing.
    [Fact]
    public async Task Describe_SqlWithAPlaceholderOfItsOwn_IsSqlsrc234AndSendsNothing()
    {
        var capture = new DescribeCapture("postgres");

        var described = await DescribeAsync(
            Requests.Of("SELECT 1 FROM users WHERE id = @id AND name = $2"),
            new CapturingExchange(LiveExchange.Instance, capture)
        );

        var failure = described.Failure.ShouldNotBeNull();
        failure.Descriptor.Id.ShouldBe("SQLSRC234");
        failure.Arguments.ShouldBe([Requests.Query, "$2"]);
        MethodsOf(capture).ShouldBe(["open"]);
    }
}
