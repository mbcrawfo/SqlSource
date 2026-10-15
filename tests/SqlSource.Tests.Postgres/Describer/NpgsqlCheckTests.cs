using System.Threading.Tasks;
using Npgsql;
using Shouldly;
using SqlSource.Tool;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Describing.Postgres;
using Xunit;

namespace SqlSource.Tests.Postgres.Describer;

// The check of the tool's lexer against Npgsql, with a real driver and a real server.  Every describe of
// ParameterTests and of ColumnTests runs the check too, so each of their cases is one that passes it.
[Collection(UsesPostgres.Name)]
public sealed class NpgsqlCheckTests(PostgresContainer postgres)
{
    // The longest name a type can have: PostgreSQL keeps 63 bytes of a name.
    private const string LongTypeName = "check_enum_with_a_name_of_exactly_sixty_three_characters_long00";

    // What these tests need and the fixture schema lacks: a type in a schema whose name needs quotes, a domain
    // over a domain, a composite type, an enum whose name is as long as a name can be, and a table that a
    // describe must leave as it is.  Each statement can be run again.
    private const string OwnObjects = $$"""
        CREATE SCHEMA IF NOT EXISTS "My Schema";
        DO $do$
        BEGIN
            CREATE TYPE "My Schema"."Status" AS ENUM ('a', 'b');
        EXCEPTION WHEN duplicate_object THEN NULL;
        END
        $do$;
        DO $do$
        BEGIN
            CREATE DOMAIN check_d2 AS email_t;
        EXCEPTION WHEN duplicate_object THEN NULL;
        END
        $do$;
        DO $do$
        BEGIN
            CREATE TYPE check_pair AS (a int, b int);
        EXCEPTION WHEN duplicate_object THEN NULL;
        END
        $do$;
        DO $do$
        BEGIN
            CREATE TYPE {{LongTypeName}} AS ENUM ('a', 'b');
        EXCEPTION WHEN duplicate_object THEN NULL;
        END
        $do$;
        CREATE TABLE IF NOT EXISTS check_canary (id int);
        """;

    private async Task<object?> ScalarAsync(string sql)
    {
        await using var source = NpgsqlDataSource.Create(postgres.ConnectionString);
        await using var command = source.CreateCommand(sql);
        return await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);
    }

    private Task<Outcome<QueryDescription, DescribeFailure>> DescribeAsync(
        string sql,
        params (string Name, string Type)[] declared
    ) => Sessions.DescribeAsync(postgres.ConnectionString, Requests.Of(sql, declared));

    private async Task ShouldPassAsync(string sql, params (string Name, string Type)[] declared)
    {
        var described = await DescribeAsync(sql, declared);

        described.Failure.ShouldBeNull(
            described.Failure is { } failure ? $"{failure.Descriptor.Id}: {string.Join("; ", failure.Arguments)}" : null
        );
    }

    [Theory]
    [InlineData("SELECT id, name FROM users WHERE id = @id")]
    [InlineData("SELECT id FROM users WHERE id = @ID OR id = @id")]
    [InlineData("SELECT $$a@b @c$$ AS x, id FROM users WHERE id = @id")]
    [InlineData("SELECT id FROM users WHERE tags @> @tags")]
    [InlineData("SELECT @id::int AS x")]
    [InlineData("SELECT tags[1:2] FROM users WHERE id = @id")]
    [InlineData("SELECT id, name FROM users")]
    [InlineData("UPDATE users SET name = @name WHERE id = @id")]
    [InlineData("INSERT INTO orders (user_id, total) VALUES (@user_id, @total) RETURNING id")]
    [InlineData("SELECT id FROM users WHERE id = @id; -- the end")]
    public async Task Describe_QueryNpgsqlReadsAsTheLexerDoes_PassesTheCheck(string sql) => await ShouldPassAsync(sql);

    // Npgsql ignores a parameter that the SQL does not reference, and still resolves its type.
    [Theory]
    [InlineData("DELETE FROM samples WHERE flag")]
    [InlineData("SELECT id FROM users WHERE id = @id")]
    public async Task Describe_DeclaredOnlyParameter_PassesTheCheck(string sql) =>
        await ShouldPassAsync(sql, ("tenant", "int"));

    // The name the describer spells for each kind of type is one Npgsql binds: an alias, a modifier that is
    // dropped, a domain, an enum, arrays, a range and a multirange.
    [Theory]
    [InlineData("int")]
    [InlineData("bigint")]
    [InlineData("smallint")]
    [InlineData("boolean")]
    [InlineData("real")]
    [InlineData("double precision")]
    [InlineData("numeric(18,2)")]
    [InlineData("varchar(50)")]
    [InlineData("char(2)")]
    [InlineData("text")]
    [InlineData("bytea")]
    [InlineData("uuid")]
    [InlineData("jsonb")]
    [InlineData("date")]
    [InlineData("time")]
    [InlineData("timestamp(3)")]
    [InlineData("timestamptz")]
    [InlineData("interval")]
    [InlineData("email_t")]
    [InlineData("user_status")]
    [InlineData("text[]")]
    [InlineData("user_status[]")]
    [InlineData("email_t[]")]
    [InlineData("tstzrange")]
    [InlineData("int4multirange")]
    [InlineData("varbit(5)")]
    public async Task Describe_ParameterOfEachKindOfType_IsBoundByItsSpelledName(string type) =>
        await ShouldPassAsync("SELECT 1 AS one FROM users WHERE @p IS NOT NULL", ("p", type));

    // Types whose spelled names Npgsql refuses: one in a schema whose name needs quotes, the "char" of the system
    // catalogs, a domain over a domain, a bit string type, which the describer spells "bit" for a parameter, a
    // composite type, and an array of a type whose name is so long that the spelling with its schema is too long
    // for Npgsql, which refuses it with an ArgumentException.
    [Theory]
    [InlineData(
        "SELECT 1 AS one FROM users WHERE @p IS NOT NULL",
        "\"My Schema\".\"Status\"",
        "\"My Schema\".\"Status\"",
        true
    )]
    [InlineData("SELECT relname FROM pg_catalog.pg_class WHERE relkind = @p", null, "\"char\"", true)]
    [InlineData("SELECT 1 AS one FROM users WHERE @p IS NOT NULL", "check_d2", "public.check_d2", false)]
    [InlineData("SELECT 1 AS one FROM users WHERE @p IS NOT NULL", "bit(3)", "\"bit\"", true)]
    [InlineData("SELECT 1 AS one FROM users WHERE @p IS NOT NULL", "check_pair", "public.check_pair", false)]
    [InlineData(
        "SELECT 1 AS one FROM users WHERE @p IS NOT NULL",
        LongTypeName + "[]",
        "public." + LongTypeName + "[]",
        false
    )]
    public async Task Describe_ParameterNpgsqlCannotBind_IsSqlsrc232NamingIt(
        string sql,
        string? declared,
        string spelled,
        bool quoted
    )
    {
        _ = await ScalarAsync(OwnObjects);

        var described = declared is null ? await DescribeAsync(sql) : await DescribeAsync(sql, ("p", declared));

        described.Value.ShouldBeNull();
        var failure = described.Failure.ShouldNotBeNull();
        failure.Descriptor.Id.ShouldBe("SQLSRC232");
        failure.Arguments.ShouldBe([spelled, "@p", Requests.Query]);
        failure.Step.ShouldBe(DescribeStep.CheckParameters);
        failure.ServerLines.ShouldHaveSingleItem().ShouldNotBeNullOrWhiteSpace();
        failure.Help.ShouldBe(quoted ? PostgresErrors.BindQuotedHelp : PostgresErrors.BindHelp);
    }

    // Npgsql resolves the type of a parameter it will not send.
    [Fact]
    public async Task Describe_DeclaredOnlyParameterNpgsqlCannotBind_IsSqlsrc232NamingIt()
    {
        _ = await ScalarAsync(OwnObjects);

        var described = await DescribeAsync("SELECT id FROM users WHERE id = @id", ("unused", "check_d2"));

        var failure = described.Failure.ShouldNotBeNull();
        failure.Descriptor.Id.ShouldBe("SQLSRC232");
        failure.Arguments.ShouldBe(["public.check_d2", "@unused", Requests.Query]);
    }

    // Npgsql sends an array of a domain as an array of its base, and the server keeps the domain: the column the
    // parameter is returned as has two types.  docs/tech-debt records that the help speaks of a @.
    [Fact]
    public async Task Describe_ArrayOfADomainReturnedAsAColumn_IsSqlsrc231NamingBothTypes()
    {
        var described = await DescribeAsync("SELECT @p AS p", ("p", "email_t[]"));

        var failure = described.Failure.ShouldNotBeNull();
        failure.Descriptor.Id.ShouldBe("SQLSRC231");
        failure.Arguments.ShouldBe([
            Requests.Query,
            "column 1 is character varying[] for Npgsql and email_t[] for SqlSource",
        ]);
        failure.ServerLines.ShouldBeEmpty();
    }

    // The live function itself, with a text no describe can bring it: "prepare" refuses such a text first.  The
    // tool's lexer reads "$x$" after a "§" as the start of a dollar-quoted string; Npgsql and the server read two
    // statements.  With a named parameter Npgsql refuses and sends nothing; without one the server refuses the
    // whole text.  Either way the answer is an error and the table of the second statement is still there.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CheckAsync_TextNpgsqlWouldSplit_AnswersAnErrorAndRunsNothing(bool withAParameter)
    {
        _ = await ScalarAsync(OwnObjects);
        var opened = await Sessions.OpenAsync(postgres.ConnectionString);
        await using var session = opened.Value.ShouldBeOfType<PostgresSession>();
        var live = new PostgresLive(session.Connection);

        var answer = await live.CheckAsync(
            withAParameter
                ? new PostgresCheckRequest(
                    "SELECT @id AS §$x$; DROP TABLE check_canary; --$x$",
                    [new PostgresCheckParameter("id", "integer")]
                )
                : new PostgresCheckRequest("SELECT 1 AS §$x$; DROP TABLE check_canary; --$x$", []),
            TestContext.Current.CancellationToken
        );

        answer.Columns.ShouldBeNull();
        answer.DriverError.ShouldBeNull();
        var error = answer.Error.ShouldNotBeNull();
        error.SqlState.ShouldBe(withAParameter ? null : "42601");
        (await ScalarAsync("SELECT to_regclass('check_canary') IS NOT NULL")).ShouldBe(true);
    }

    // A type that cannot be bound is found before the text is read, and is named with its parameter whichever
    // place of the list it has.
    [Fact]
    public async Task CheckAsync_SecondParameterCannotBeBound_AnswersItAsTheDriverError()
    {
        var opened = await Sessions.OpenAsync(postgres.ConnectionString);
        await using var session = opened.Value.ShouldBeOfType<PostgresSession>();
        var live = new PostgresLive(session.Connection);

        var answer = await live.CheckAsync(
            new PostgresCheckRequest(
                "SELECT @a, @b; SELECT 2",
                [new PostgresCheckParameter("a", "integer"), new PostgresCheckParameter("b", "no_such_type")]
            ),
            TestContext.Current.CancellationToken
        );

        answer.Error.ShouldBeNull();
        var unbound = answer.DriverError.ShouldNotBeNull();
        unbound.Parameter.ShouldBe("b");
        unbound.Message.ShouldContain("no_such_type");
    }

    [Fact]
    public async Task CheckAsync_SqlTheServerRejects_AnswersItsError()
    {
        var opened = await Sessions.OpenAsync(postgres.ConnectionString);
        await using var session = opened.Value.ShouldBeOfType<PostgresSession>();
        var live = new PostgresLive(session.Connection);

        var answer = await live.CheckAsync(
            new PostgresCheckRequest(
                "SELECT nope FROM users WHERE id = @id",
                [new PostgresCheckParameter("id", "integer")]
            ),
            TestContext.Current.CancellationToken
        );

        answer.Error.ShouldNotBeNull().SqlState.ShouldBe("42703");
    }
}
