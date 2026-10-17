using System.Threading.Tasks;
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

    // The name the server gives its array: "_" and the type's name, cut to 63 bytes.
    private const string LongArrayName = "_check_enum_with_a_name_of_exactly_sixty_three_characters_long0";

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

    // Both readers skip a @name in a string, '@id'.  A @name in a comment has no row: a run describes the query's SQL
    // without its comments (SampleSql.Build), so check never reads one.
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
    [InlineData("SELECT '$1' AS a, id FROM users WHERE id = @id")]
    [InlineData("SELECT '@id' AS a, id FROM users WHERE id = @id")]
    public async Task Describe_QueryNpgsqlReadsAsTheLexerDoes_PassesTheCheck(string sql) => await ShouldPassAsync(sql);

    // Npgsql ignores a parameter that the SQL does not reference, and still resolves its type.
    [Theory]
    [InlineData("DELETE FROM samples WHERE flag")]
    [InlineData("SELECT id FROM users WHERE id = @id")]
    public async Task Describe_DeclaredOnlyParameter_PassesTheCheck(string sql) =>
        await ShouldPassAsync(sql, ("tenant", "int"));

    // The name the describer gives each kind of type to bind by is one Npgsql binds: an alias, a modifier that is
    // dropped, a domain, an enum, arrays, a range and a multirange; a schema and a type whose names need quotes, and
    // an array of it; plain bit, which the server spells "bit"; a domain over a domain, bound by its innermost base;
    // "char"; and an array of a type whose name is as long as a name can be.  The parameter is returned as a column,
    // so the check's comparison of the columns' types proves that the name binds the type the tool read, and not
    // only one Npgsql accepts.  An array of a domain, email_t[], is bound by public.email_t[] but sent as an array of
    // its base, so returned as a column it differs: the two tests of an array of a domain below assert its name and
    // that difference.
    //
    // The last two, and numeric, were tried against Npgsql 10.0.3 on PostgreSQL 14 and 18, with SELECT @p AS p, to see
    // the type the server was sent, with the default search_path and with public."char", other.<long name> and
    // public."numeric" standing before pg_catalog on it.  "char" and pg_catalog."char" are refused: the data type name
    // "could not be found in the types that were loaded by Npgsql", and so is internalchar.  char, which is Npgsql's
    // own DataTypeName for NpgsqlDbType.InternalChar, and pg_catalog.char bind "char" in every session.  The array's
    // public.<long name>[] and the unqualified <long name>[] are refused: "Name is too long and would be truncated".
    // public.<array's name> binds the array in every session; the unqualified <array's name> binds it too, but is
    // refused as ambiguous once another schema holds a type of the same name.  numeric binds pg_catalog's numeric in
    // every session.
    [Theory]
    [InlineData("int", "integer")]
    [InlineData("bigint", "bigint")]
    [InlineData("smallint", "smallint")]
    [InlineData("boolean", "boolean")]
    [InlineData("real", "real")]
    [InlineData("double precision", "double precision")]
    [InlineData("numeric(18,2)", "numeric")]
    [InlineData("varchar(50)", "character varying")]
    [InlineData("char(2)", "bpchar")]
    [InlineData("text", "text")]
    [InlineData("bytea", "bytea")]
    [InlineData("uuid", "uuid")]
    [InlineData("jsonb", "jsonb")]
    [InlineData("date", "date")]
    [InlineData("time", "time without time zone")]
    [InlineData("timestamp(3)", "timestamp without time zone")]
    [InlineData("timestamptz", "timestamp with time zone")]
    [InlineData("interval", "interval")]
    [InlineData("email_t", "public.email_t")]
    [InlineData("user_status", "public.user_status")]
    [InlineData("text[]", "text[]")]
    [InlineData("user_status[]", "public.user_status[]")]
    [InlineData("tstzrange", "tstzrange")]
    [InlineData("int4multirange", "int4multirange")]
    [InlineData("varbit(5)", "bit varying")]
    [InlineData("\"My Schema\".\"Status\"", "My Schema.Status")]
    [InlineData("\"My Schema\".\"Status\"[]", "My Schema.Status[]")]
    [InlineData("bit(3)", "bit")]
    [InlineData("check_d2", "character varying")]
    [InlineData("\"char\"", "char")]
    [InlineData(LongTypeName + "[]", "public." + LongArrayName)]
    public async Task Describe_ParameterOfEachKindOfType_IsBoundByItsBindName(string type, string bindName)
    {
        await postgres.ExecuteAsync(OwnObjects);

        var described = await DescribeAsync("SELECT @p AS p", ("p", type));

        described.Failure.ShouldBeNull(
            described.Failure is { } failure ? $"{failure.Descriptor.Id}: {string.Join("; ", failure.Arguments)}" : null
        );
        described
            .Value.ShouldNotBeNull()
            .Parameters.ShouldHaveSingleItem()
            .Type.ShouldNotBeNull()
            .BindName.ShouldBe(bindName);
    }

    // Types Npgsql cannot send a value of: a composite type, a table's row type and a type of the system catalogs
    // that Npgsql does not load.
    [Theory]
    [InlineData("SELECT 1 AS one FROM users WHERE @p IS NOT NULL", "check_pair", "public.check_pair", "@p")]
    [InlineData("SELECT 1 AS one FROM users WHERE @p IS NOT NULL", "users", "public.users", "@p")]
    [InlineData("SELECT c.relname FROM pg_catalog.pg_class AS c WHERE c.oid = @t::regclass", null, "regclass", "@t")]
    public async Task Describe_ParameterNpgsqlCannotBind_IsSqlsrc232NamingIt(
        string sql,
        string? declared,
        string name,
        string written
    )
    {
        await postgres.ExecuteAsync(OwnObjects);

        var described = declared is null ? await DescribeAsync(sql) : await DescribeAsync(sql, ("p", declared));

        described.Value.ShouldBeNull();
        var failure = described.Failure.ShouldNotBeNull();
        failure.Descriptor.Id.ShouldBe("SQLSRC232");
        failure.Arguments.ShouldBe([name, written, Requests.Query]);
        failure.Step.ShouldBe(DescribeStep.CheckParameters);
        failure.ServerLines.ShouldHaveSingleItem().ShouldNotBeNullOrWhiteSpace();
        failure.Help.ShouldBe(PostgresErrors.BindHelp);
    }

    // Npgsql resolves the type of a parameter it will not send.
    [Fact]
    public async Task Describe_DeclaredOnlyParameterNpgsqlCannotBind_IsSqlsrc232NamingIt()
    {
        await postgres.ExecuteAsync(OwnObjects);

        var described = await DescribeAsync("SELECT id FROM users WHERE id = @id", ("unused", "check_pair"));

        var failure = described.Failure.ShouldNotBeNull();
        failure.Descriptor.Id.ShouldBe("SQLSRC232");
        failure.Arguments.ShouldBe(["public.check_pair", "@unused", Requests.Query]);
    }

    // Where it is not returned, an array of a domain is bound by its element's name and [], which Npgsql takes.
    [Fact]
    public async Task Describe_ArrayOfADomainNotReturned_IsBoundByItsElementsNameAndBrackets()
    {
        var described = await DescribeAsync("SELECT 1 AS one FROM users WHERE @p IS NOT NULL", ("p", "email_t[]"));

        described
            .Value.ShouldNotBeNull()
            .Parameters.ShouldHaveSingleItem()
            .Type.ShouldNotBeNull()
            .BindName.ShouldBe("public.email_t[]");
    }

    // Npgsql sends an array of a domain as an array of its base, and the server keeps the domain: the column the
    // parameter is returned as has two types.  The help is the cast of the column, not the help of a @.
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
        failure.Help.ShouldBe(PostgresErrors.ArrayOfDomainHelp);
    }

    // A parameter written twice, as @id and as :id in a slice that reads the column id.  Npgsql places it in both
    // and drops the ":".  In "tags[1:id]" that leaves "tags[1$1]", which the server refuses.  In a slice without a
    // lower bound, "tags[:id]", it leaves a subscript, "tags[$1]", which the server accepts, and in a predicate the
    // columns are the same: only the texts differ, and the help says the message says where.
    [Theory]
    [InlineData(
        "SELECT tags[1:id] AS t FROM users WHERE id = @id",
        "syntax error at or near \"$1\"",
        PostgresErrors.CheckHelp
    )]
    [InlineData(
        "SELECT id FROM users WHERE id = @id AND tags[:id] IS NOT NULL",
        "the SQL with $n parts at character 45: '$1] IS NOT' for Npgsql and ':id] IS NO' for SqlSource",
        PostgresErrors.PlacementHelp
    )]
    public async Task Describe_ColonParameterNpgsqlReadsInASlice_IsSqlsrc231(string sql, string differs, string help)
    {
        var described = await DescribeAsync(sql);

        var failure = described.Failure.ShouldNotBeNull();
        failure.Descriptor.Id.ShouldBe("SQLSRC231");
        failure.Arguments.ShouldBe([Requests.Query, differs]);
        failure.Step.ShouldBe(DescribeStep.CheckParameters);
        failure.Help.ShouldBe(help);
    }

    // What Npgsql sent: its rewrite of the SQL, and the name of the parameter each $n stands for.
    [Fact]
    public async Task CheckAsync_QueryWithParameters_AnswersNpgsqlsText()
    {
        await using var session = await Sessions.OpenSessionAsync(postgres.ConnectionString);
        var live = new PostgresLive(session.Connection);

        var answer = await live.CheckAsync(
            new PostgresCheckRequest(
                "SELECT id FROM users WHERE id = @id",
                [new PostgresCheckParameter("id", "integer")]
            ),
            TestContext.Current.CancellationToken
        );

        _ = answer.Columns.ShouldNotBeNull();
        var sent = answer.Sent.ShouldNotBeNull();
        sent.Text.ShouldBe("SELECT id FROM users WHERE id = $1");
        sent.Parameters.ShouldBe(["id"]);
    }

    // The live function itself, with a text no describe can bring it: "prepare" refuses such a text first.  Npgsql
    // and the server read two statements.  With a named parameter Npgsql refuses and sends nothing; without one the
    // server refuses the whole text.  Either way the answer is an error and the table of the second statement is
    // still there.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CheckAsync_TextNpgsqlWouldSplit_AnswersAnErrorAndRunsNothing(bool withAParameter)
    {
        await postgres.ExecuteAsync(OwnObjects);
        await using var session = await Sessions.OpenSessionAsync(postgres.ConnectionString);
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
        answer.TextRefused.ShouldBe(withAParameter);
        (await postgres.ScalarAsync("SELECT to_regclass('check_canary') IS NOT NULL")).ShouldBe(true);
    }

    // A type that cannot be bound is found before the text is read, and is named with its parameter whichever
    // place of the list it has.
    [Fact]
    public async Task CheckAsync_SecondParameterCannotBeBound_AnswersItAsTheDriverError()
    {
        await using var session = await Sessions.OpenSessionAsync(postgres.ConnectionString);
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
        await using var session = await Sessions.OpenSessionAsync(postgres.ConnectionString);
        var live = new PostgresLive(session.Connection);

        var answer = await live.CheckAsync(
            new PostgresCheckRequest(
                "SELECT nope FROM users WHERE id = @id",
                [new PostgresCheckParameter("id", "integer")]
            ),
            TestContext.Current.CancellationToken
        );

        answer.Error.ShouldNotBeNull().SqlState.ShouldBe("42703");
        answer.TextRefused.ShouldBe(false);
    }
}
