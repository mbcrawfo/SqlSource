using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Parsing;
using SqlSource.Snapshot;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Describing.SqlServer;
using Xunit;
using static SqlSource.Tool.Testing.DescribeRequests;

namespace SqlSource.Tests.SqlServer.Describer;

// A query's parameters against a real server, each described in a session of its own.  The canary of the schema,
// dbo.victim, must be there after every text that would drop it.
[Collection(UsesSqlServer.Name)]
public sealed class ParameterTests(SqlServerContainer sqlServer)
{
    private const string DropVictim = "DROP TABLE dbo.victim";

    private static readonly SqlServerType Int = new("int", 4, 10, 0, null);
    private static readonly SqlServerType BigInt = new("bigint", 8, 19, 0, null);
    private static readonly SqlServerType Name100 = new("nvarchar(100)", 200, 0, 0, null);

    private static readonly SqlServerType Email = new(
        "nvarchar(320)",
        640,
        0,
        0,
        new SqlServerUserType("dbo", "Email", null)
    );

    // Every row of the spike's table of parameters that the server types, one parameter a row: the SQL, the parameter,
    // and its type's name, length, precision and scale, and whether it is the alias type dbo.Email.
    public static TheoryData<string, string, string, int, int, int, bool> SpikeRows =>
        new()
        {
            { "SELECT TOP (@n) id FROM dbo.users", "n", "bigint", 8, 19, 0, false },
            {
                "SELECT id FROM dbo.users ORDER BY id OFFSET @skip ROWS FETCH NEXT @take ROWS ONLY",
                "skip",
                "bigint",
                8,
                19,
                0,
                false
            },
            {
                "SELECT id FROM dbo.users ORDER BY id OFFSET @skip ROWS FETCH NEXT @take ROWS ONLY",
                "take",
                "bigint",
                8,
                19,
                0,
                false
            },
            { "SELECT id FROM dbo.users WHERE name LIKE @p", "p", "nvarchar(4000)", 8000, 0, 0, false },
            { "SELECT id FROM dbo.users WHERE name = @p + N'x'", "p", "nvarchar(4000)", 8000, 0, 0, false },
            { "SELECT id FROM dbo.users WHERE id IN (@a, @b)", "a", "int", 4, 10, 0, false },
            { "SELECT id FROM dbo.users WHERE id IN (@a, @b)", "b", "int", 4, 10, 0, false },
            { "SELECT id FROM dbo.users WHERE email = @email", "email", "nvarchar(320)", 640, 0, 0, true },
            { "SELECT id FROM dbo.orders WHERE total > @t", "t", "decimal(38,19)", 17, 38, 19, false },
            { "SELECT id FROM dbo.users WHERE rv = @rv", "rv", "timestamp", 8, 0, 0, false },
            { "SELECT id FROM dbo.users WHERE deleted_at < @since", "since", "datetime2(7)", 8, 27, 7, false },
            {
                "SELECT id FROM dbo.users WHERE name = @p COLLATE Latin1_General_BIN",
                "p",
                "nvarchar(100)",
                200,
                0,
                0,
                false
            },
            {
                "INSERT INTO dbo.users (name, nickname) VALUES (@name, @nick)",
                "name",
                "nvarchar(100)",
                200,
                0,
                0,
                false
            },
            {
                "INSERT INTO dbo.users (name, nickname) VALUES (@name, @nick)",
                "nick",
                "nvarchar(50)",
                100,
                0,
                0,
                false
            },
            { "UPDATE dbo.users SET nickname = @nick WHERE id = @id", "nick", "nvarchar(50)", 100, 0, 0, false },
            { "UPDATE dbo.users SET nickname = @nick WHERE id = @id", "id", "int", 4, 10, 0, false },
            { "SELECT id FROM dbo.users WHERE CAST(@p AS int) = id", "p", "int", 4, 10, 0, false },
            { "SELECT ISNULL(@p, 0) AS x", "p", "int", 4, 10, 0, false },
            {
                "SELECT id FROM dbo.users AS u WHERE EXISTS "
                    + "(SELECT 1 FROM dbo.orders AS o WHERE o.user_id = u.id AND o.note = @note)",
                "note",
                "varchar(200)",
                200,
                0,
                0,
                false
            },
            {
                "WITH named AS (SELECT id, name FROM dbo.users WHERE name = @p) SELECT id FROM named",
                "p",
                "nvarchar(100)",
                200,
                0,
                0,
                false
            },
            { "SELECT id FROM dbo.users WHERE @p IS NULL", "p", "int", 4, 10, 0, false },
        };

    [Theory]
    [MemberData(nameof(SpikeRows))]
    public async Task DescribeAsync_RowOfTheSpike_IsTypedAsTheServerInfersIt(
        string sql,
        string parameter,
        string name,
        int maxLength,
        int precision,
        int scale,
        bool isEmail
    )
    {
        var description = await ShouldDescribeAsync(Of(SqlDialectRules.SqlServer, sql));

        description.ResultKind.ShouldBe(SidecarResultKind.None);
        description
            .Parameters.Single(described => described.Name == parameter)
            .ShouldBe(
                new DescribedParameter(
                    parameter,
                    new SqlServerType(name, maxLength, precision, scale, isEmail ? Email.UserType : null),
                    SidecarValues.TypeSource.Inferred
                )
            );
    }

    // The one row of the spike that differs between the two servers.
    [Fact]
    public async Task DescribeAsync_DateAdd_IsIntOn2017AndBigintOn2025()
    {
        var description = await ShouldDescribeAsync(
            Of(SqlDialectRules.SqlServer, "SELECT DATEADD(day, @days, SYSUTCDATETIME()) AS d")
        );

        description.Parameters.ShouldHaveSingleItem().Type.ShouldBe(sqlServer.Major >= 17 ? BigInt : Int);
    }

    [Theory]
    [InlineData("SELECT id FROM dbo.users WHERE id = @p", "int", "int", 4, 10, false)]
    [InlineData("SELECT id FROM dbo.users WHERE email = @p", "dbo.Email", "nvarchar(320)", 640, 0, true)]
    [InlineData(
        "SELECT id FROM dbo.all_types WHERE varchar_max_value = @p",
        "varchar(max)",
        "varchar(max)",
        -1,
        0,
        false
    )]
    public async Task DescribeAsync_DeclaredType_IsTheServersReadingOfTheDeclaration(
        string sql,
        string declared,
        string name,
        int maxLength,
        int precision,
        bool isEmail
    )
    {
        var description = await ShouldDescribeAsync(Of(SqlDialectRules.SqlServer, sql, ("p", declared)));

        description.Parameters.ShouldBe([
            new DescribedParameter(
                "p",
                new SqlServerType(name, maxLength, precision, 0, isEmail ? Email.UserType : null),
                SidecarValues.TypeSource.Declared
            ),
        ]);
    }

    // The server checks a declared type against every use of its parameter.
    [Fact]
    public async Task DescribeAsync_DeclaredTypeThatAUseRejects_IsSqlsrc225()
    {
        var failure = await ShouldFailAsync(
            Of(SqlDialectRules.SqlServer, "SELECT id FROM dbo.users WHERE id = @p", ("p", "xml")),
            "SQLSRC225"
        );

        failure.Step.ShouldBe(DescribeStep.DescribeParameters);
        failure.ServerLines[0].ShouldStartWith("Msg 206, ");
        failure.ServerLines[^1].ShouldStartWith("Msg 11501, ");
    }

    [Theory]
    [InlineData("SELECT @p AS x", "Msg 11506, ")]
    [InlineData("SELECT COALESCE(@p, name) FROM dbo.users", "Msg 11502, ")]
    public async Task DescribeAsync_NoContextTypesTheParameter_IsSqlsrc226NamingIt(string sql, string line)
    {
        var failure = await ShouldFailAsync(Of(SqlDialectRules.SqlServer, sql), "SQLSRC226");

        failure.Arguments.ShouldBe(["@p", Query]);
        failure.ServerLines.ShouldHaveSingleItem().ShouldStartWith(line);
    }

    [Fact]
    public async Task DescribeAsync_DeclaredTypeTheDatabaseLacks_IsSqlsrc228()
    {
        var failure = await ShouldFailAsync(
            Of(SqlDialectRules.SqlServer, "SELECT id FROM dbo.users WHERE email = @p", ("p", "dbo.Nope")),
            "SQLSRC228"
        );

        failure.Arguments.ShouldBe(["dbo.Nope", "@p", Query, SqlServerErrors.NoSuchType]);
        failure.Step.ShouldBe(DescribeStep.ResolveType);
        failure.ServerLines[0].ShouldStartWith("Msg 2715: ");
    }

    // The optional filter: the use in "IS NULL" types nothing, and the parameter takes the type of the column, in a
    // SELECT, in an UPDATE with no rows, and in an IF whose two paths return two shapes.
    [Theory]
    [InlineData("SELECT id FROM dbo.users WHERE (@name IS NULL OR name = @name)")]
    [InlineData("UPDATE dbo.users SET nickname = N'x' WHERE (@name IS NULL OR name = @name)")]
    [InlineData(
        "IF @a = 1 SELECT id FROM dbo.users WHERE (@name IS NULL OR name = @name) ELSE SELECT name FROM dbo.users"
    )]
    public async Task DescribeAsync_OptionalFilter_IsTypedFromTheCopyThatConstrains(string sql)
    {
        var description = await ShouldDescribeAsync(Of(SqlDialectRules.SqlServer, sql));

        description
            .Parameters.Single(described => described.Name == "name")
            .ShouldBe(new DescribedParameter("name", Name100, SidecarValues.TypeSource.InferredFromCopies));
    }

    // A column of xml: the copy in "IS NULL" binds as xml and as int, and the one assigned to the column fails as int.
    [Fact]
    public async Task DescribeAsync_ReusedParameterOfAnXmlColumn_IsXml()
    {
        var description = await ShouldDescribeAsync(
            Of(SqlDialectRules.SqlServer, "UPDATE dbo.all_types SET xml_value = @d WHERE (@d IS NULL OR id = 1)")
        );

        description.Parameters.ShouldBe([
            new DescribedParameter(
                "d",
                new SqlServerType("xml", -1, 0, 0, null),
                SidecarValues.TypeSource.InferredFromCopies
            ),
        ]);
    }

    // Both copies are typed int, as a parameter used once in "IS NULL" is: they agree, and nothing is probed.
    [Fact]
    public async Task DescribeAsync_EveryUseTypesNothing_IsWhatTheServerGives()
    {
        var description = await ShouldDescribeAsync(
            Of(SqlDialectRules.SqlServer, "SELECT id FROM dbo.users WHERE @p IS NULL OR @p IS NOT NULL")
        );

        description.Parameters.ShouldBe([
            new DescribedParameter("p", Int, SidecarValues.TypeSource.InferredFromCopies),
        ]);
    }

    // A name too long for an identifier is refused by the server with error 103, once, reused and declared alike, and
    // the message, which quotes the start of the name, is the same.
    [Fact]
    public async Task DescribeAsync_NameTooLong_IsSqlsrc225WhetherUsedOnceReusedOrDeclared()
    {
        var name = new string('p', 127) + "q";
        var sql = $"SELECT id FROM dbo.users WHERE id = @{name}";

        var once = await ShouldFailAsync(Of(SqlDialectRules.SqlServer, sql), "SQLSRC225");
        var reused = await ShouldFailAsync(Of(SqlDialectRules.SqlServer, $"{sql} OR @{name} IS NULL"), "SQLSRC225");
        var declared = await ShouldFailAsync(Of(SqlDialectRules.SqlServer, sql, (name, "int")), "SQLSRC225");

        once.ServerLines[0].ShouldStartWith("Msg 103, ");
        reused.Arguments.ShouldBe(once.Arguments);
        declared.Arguments.ShouldBe(once.Arguments);
        declared.Step.ShouldBe(DescribeStep.ResolveType);
        declared.ServerLines[0].ShouldStartWith("Msg 103: ");
    }

    // SQL Server describes no statement that creates a procedure, so the server's error comes before the lists are
    // compared: 11527, the query is SQLSRC225.
    [Fact]
    public async Task DescribeAsync_CreateProcedure_IsSqlsrc225ThatTheServerCannotDescribeIt()
    {
        var failure = await ShouldFailAsync(
            Of(SqlDialectRules.SqlServer, "CREATE PROCEDURE p @id int AS SELECT 1"),
            "SQLSRC225"
        );

        failure.ServerLines.ShouldHaveSingleItem().ShouldStartWith("Msg 11527, ");
    }

    // A real misreading: SqlSource ends a list of DECLAREd variables at a ";" or at a word that starts a statement, and
    // neither the label nor RECEIVE is one, so it reads "@p" after the comma as a variable, where SQL Server reads a
    // parameter.  The list is the one the generator's parser makes of the file; the queue is the test's own.
    [Fact]
    public async Task DescribeAsync_ParameterSqlSourceReadsAsADeclaredVariable_IsSqlsrc231()
    {
        const string Sql =
            "DECLARE @x int = @n next_message: RECEIVE TOP (1) message_body, @p + 1 AS q FROM dbo.sqlsource_messages";
        var parsed = SqlFileParser.Parse("-- dialect: mssql\n-- name: Q\n" + Sql, "Q.sql");
        parsed.Errors.ShouldBeEmpty();
        var parameters = parsed.Blocks.ShouldHaveSingleItem().Parameters;
        parameters.Select(parameter => parameter.Name).ShouldBe(["n"]);
        await sqlServer.ExecuteAsync("CREATE QUEUE dbo.sqlsource_messages");
        try
        {
            var failure = await ShouldFailAsync(Listed(Sql, [.. parameters]), "SQLSRC231");

            failure.Arguments.ShouldBe([
                "SQL Server",
                Query,
                "SQL Server reads '@p' as a parameter and SqlSource does not",
            ]);
        }
        finally
        {
            await sqlServer.ExecuteAsync("DROP QUEUE dbo.sqlsource_messages");
        }
    }

    // The lists are the test's, as a reading of SqlSource that differs from the server's makes them.
    [Fact]
    public async Task DescribeAsync_ListLacksAParameterTheServerReports_IsSqlsrc231()
    {
        var failure = await ShouldFailAsync(
            Listed("SELECT id FROM dbo.users WHERE id = @a AND name = @b", Inferred("a")),
            "SQLSRC231"
        );

        failure.Arguments.ShouldBe([
            "SQL Server",
            Query,
            "SQL Server reads '@b' as a parameter and SqlSource does not",
        ]);
        failure.Step.ShouldBe(DescribeStep.CheckParameters);
    }

    [Fact]
    public async Task DescribeAsync_ListHoldsAParameterTheServerDoesNotReport_IsSqlsrc231()
    {
        var failure = await ShouldFailAsync(
            Listed("SELECT id FROM dbo.users WHERE id = @a", Inferred("a"), Inferred("b")),
            "SQLSRC231"
        );

        failure.Arguments[2].ShouldBe("SqlSource reads '@b' as a parameter and SQL Server does not");
    }

    [Fact]
    public async Task DescribeAsync_CopiesThatConstrainDisagree_IsSqlsrc262()
    {
        var failure = await ShouldFailAsync(
            Of(SqlDialectRules.SqlServer, "SELECT id FROM dbo.users WHERE id = @p AND name = @p"),
            "SQLSRC262"
        );

        failure.Arguments.ShouldBe(["@p", Query, "int", "nvarchar(100)"]);
    }

    // The plan leaves a DECLAREd variable out of the list, and the server does not report it.
    [Fact]
    public async Task DescribeAsync_DeclaredVariable_IsNoParameter()
    {
        var description = await ShouldDescribeAsync(
            Listed("DECLARE @x int = 1; SELECT id FROM dbo.users WHERE id = @x")
        );

        description.Parameters.ShouldBeEmpty();
    }

    // The user's text reaches the server as the value of a parameter of a procedure that analyses it and runs nothing.
    [Fact]
    public async Task DescribeAsync_TextOfTwoStatements_DescribesTheFirstAndRunsNothing()
    {
        var description = await ShouldDescribeAsync(
            Of(SqlDialectRules.SqlServer, $"SELECT id FROM dbo.users WHERE id = @p; {DropVictim}")
        );

        description.Parameters.ShouldBe([new DescribedParameter("p", Int, SidecarValues.TypeSource.Inferred)]);
        await ShouldStandAsync();
    }

    [Fact]
    public async Task ProbeAsync_TextOfTwoStatements_BindsAndRunsNothing()
    {
        var token = TestContext.Current.CancellationToken;
        await using var session = await Sessions.OpenSessionAsync(sqlServer.ConnectionString);
        var live = new SqlServerLive(session.Connection);

        var answer = await live.ProbeAsync(
            new SqlServerProbeRequest($"SELECT id FROM dbo.users WHERE id = @p; {DropVictim}", "@p int"),
            token
        );

        answer.ShouldBe(new SqlServerProbeAnswer(true, null));
        await ShouldStandAsync();
    }

    // The grammar refuses the declaration, and nothing is sent.
    [Fact]
    public async Task DescribeAsync_DeclarationThatIsAStatement_IsSqlsrc228AndSendsNothing()
    {
        var (described, capture) = await Sessions.DescribeCapturedAsync(
            sqlServer.ConnectionString,
            Of(SqlDialectRules.SqlServer, "SELECT id FROM dbo.users WHERE id = @p", ("p", $"int; {DropVictim}"))
        );

        described.Failure.ShouldNotBeNull().Descriptor.Id.ShouldBe("SQLSRC228");
        capture.Calls.Select(call => call.Method).ShouldBe(["open"]);
        await ShouldStandAsync();
    }

    // The live calls themselves, with a declaration no describe can bring them: the server reads it as declarations
    // and refuses it, and runs nothing.
    [Fact]
    public async Task DeclaredAsync_DeclarationThatIsAStatement_AnswersErrorRowsAndRunsNothing()
    {
        var token = TestContext.Current.CancellationToken;
        await using var session = await Sessions.OpenSessionAsync(sqlServer.ConnectionString);
        var live = new SqlServerLive(session.Connection);

        var answer = await live.DeclaredAsync(
            new SqlServerDeclaredRequest("SELECT @p AS [1]", $"@p int; {DropVictim}"),
            token
        );

        answer.Rows.ShouldBeNull();
        answer.Error.ShouldBeNull();
        answer.Errors.ShouldNotBeNull().Select(error => error.Number).ShouldContain(102);
        await ShouldStandAsync();
    }

    [Fact]
    public async Task ParametersAsync_DeclarationThatIsAStatement_AnswersAnErrorAndRunsNothing()
    {
        var token = TestContext.Current.CancellationToken;
        await using var session = await Sessions.OpenSessionAsync(sqlServer.ConnectionString);
        var live = new SqlServerLive(session.Connection);

        var answer = await live.ParametersAsync(
            new SqlServerParametersRequest("SELECT id FROM dbo.users WHERE id = @p", $"@p int; {DropVictim}"),
            token
        );

        answer.Rows.ShouldBeNull();
        answer.Error.ShouldNotBeNull().Errors[0].Number.ShouldBe(102);
        await ShouldStandAsync();
    }

    private Task<QueryDescription> ShouldDescribeAsync(DescribeRequest request) =>
        Sessions.ShouldDescribeAsync(sqlServer.ConnectionString, request);

    private async Task<DescribeFailure> ShouldFailAsync(DescribeRequest request, string id)
    {
        var described = await Sessions.DescribeAsync(sqlServer.ConnectionString, request);

        var failure = described.Failure.ShouldNotBeNull();
        failure.Descriptor.Id.ShouldBe(id);
        return failure;
    }

    // The canary is still there.
    private async Task ShouldStandAsync() =>
        (await sqlServer.ScalarAsync("SELECT OBJECT_ID('dbo.victim')")).ShouldBeOfType<int>(
            "the call dropped the table"
        );
}
