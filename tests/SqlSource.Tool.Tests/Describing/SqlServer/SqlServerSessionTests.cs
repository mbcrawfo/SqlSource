using System.Globalization;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Parsing;
using SqlSource.Snapshot;
using SqlSource.Tool.Describing.Capture;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Describing.SqlServer;
using SqlSource.Tool.Testing;
using SqlSource.Tool.Testing.Fixtures;
using Xunit;
using static SqlSource.Tool.Testing.DescribeRequests;

namespace SqlSource.Tool.Tests.Describing.SqlServer;

// The session over a capture the test writes: what the session makes of the server's version, its edition and its
// settings, and of an error, at "open"; and for a describe, the calls it asks, in order, and what it makes of their
// answers.  No driver runs: the connection is a word that SqlClient could not open.
public sealed class SqlServerSessionTests
{
    private static readonly SqlServerParameterRow IntRow = new("@x", "int", 4, 10, 0, null, null, null);
    private static readonly SqlServerParameterRow Name100Row = new("@x", "nvarchar(100)", 200, 0, 0, null, null, null);
    private static readonly SqlServerType Int = new("int", 4, 10, 0, null);
    private static readonly SqlServerType Name100 = new("nvarchar(100)", 200, 0, 0, null);

    [Fact]
    public async Task OpenAsync_Server2017_OpensWithTheVersionTheDriverAndTheFourSettings()
    {
        var exchange = SqlServerScript.Answering(SqlServerScript.Opened);

        var opened = await OpenAsync(exchange);

        await using var session = opened.Value.ShouldNotBeNull();
        session.Server.Version.ShouldBe("14.0.3560.2");
        session.Server.Driver.ShouldBe("Microsoft.Data.SqlClient");
        session.Server.DriverVersion.ShouldBe("7.1.1");
        session.Server.Settings.ShouldBe([
            new ServerSetting("compatibility_level", "140"),
            new ServerSetting("ANSI_NULLS", "ON"),
            new ServerSetting("QUOTED_IDENTIFIER", "ON"),
            new ServerSetting("collation", "SQL_Latin1_General_CP1_CI_AS"),
        ]);
        exchange.Unused.ShouldBeEmpty();
    }

    // SQL Server 2016 is 13: the server is refused before any query is described, and it said nothing.
    [Fact]
    public async Task OpenAsync_Server2016_IsSqlsrc235()
    {
        var opened = await OpenAsync(
            SqlServerScript.Answering(SqlServerScript.Opened with { ProductVersion = "13.0.6300.2" })
        );

        opened.Value.ShouldBeNull();
        var failure = opened.Failure.ShouldNotBeNull();
        failure.Descriptor.Id.ShouldBe("SQLSRC235");
        failure.Arguments.ShouldBe([SqlServerScript.Database, "SQL Server", "13.0.6300.2", "14 (SQL Server 2017)"]);
        failure.Step.ShouldBeNull();
        failure.ServerLines.ShouldBeEmpty();
        string.Format(
                CultureInfo.InvariantCulture,
                failure.Descriptor.MessageFormat.ToString(CultureInfo.InvariantCulture),
                [.. failure.Arguments]
            )
            .ShouldBe(
                "The database 'app' runs SQL Server 13.0.6300.2, and the tool supports 14 (SQL Server 2017) and later"
            );
    }

    // An Azure SQL Database reports a version of its own, which is no release of SQL Server.
    [Fact]
    public async Task OpenAsync_AzureSqlDatabaseOfAnOlderVersion_Opens()
    {
        var opened = await OpenAsync(
            SqlServerScript.Answering(SqlServerScript.Opened with { ProductVersion = "12.0.2000.8", EngineEdition = 5 })
        );

        await using var session = opened.Value.ShouldNotBeNull();
        session.Server.Version.ShouldBe("12.0.2000.8");
    }

    [Fact]
    public async Task OpenAsync_AzureManagedInstance_Opens()
    {
        var opened = await OpenAsync(
            SqlServerScript.Answering(SqlServerScript.Opened with { ProductVersion = "12.0.2000.8", EngineEdition = 8 })
        );

        await using var session = opened.Value.ShouldNotBeNull();
        session.Server.Version.ShouldBe("12.0.2000.8");
    }

    [Fact]
    public async Task OpenAsync_ErrorAnswered_IsSqlsrc224WithTheFixedLine()
    {
        var error = new SqlServerServerError([new SqlServerErrorRow(18456, 14, 8, "the server refused the login")]);

        var opened = await OpenAsync(
            SqlServerScript.Answering(new SqlServerOpenAnswer(null, null, null, null, error, null))
        );

        opened.Value.ShouldBeNull();
        var failure = opened.Failure.ShouldNotBeNull();
        failure.Descriptor.Id.ShouldBe("SQLSRC224");
        failure.Arguments.ShouldBe([SqlServerScript.Database]);
        failure.Step.ShouldBeNull();
        failure.ServerLines.ShouldBe(["Msg 18456, Level 14, State 8: the server refused the login"]);
        failure.Help.ShouldBe(
            "check that the server is running, and that the connection string names it, a login it accepts and the "
                + "database"
        );
    }

    [Fact]
    public async Task DescribeAsync_TwoInferredParameters_AsksParametersOnceWithNoDeclarations()
    {
        const string Sql = "SELECT id FROM dbo.users WHERE id = @id AND name = @name";
        var parameters = new SqlServerParametersRequest(Sql, null);
        var script = new SqlServerScript().Add(
            "parameters",
            parameters,
            new SqlServerParametersAnswer([IntRow with { Name = "@id" }, Name100Row with { Name = "@name" }], null)
        );

        var (described, asked) = await script.DescribeAsync(Of(SqlDialectRules.SqlServer, Sql));

        asked.ShouldBe([Call("parameters", parameters)]);
        var description = described.Value.ShouldNotBeNull();
        description.ResultKind.ShouldBe(SidecarResultKind.None);
        description.Columns.ShouldBeNull();
        description.Parameters.ShouldBe([
            new DescribedParameter("id", Int, SidecarValues.TypeSource.Inferred),
            new DescribedParameter("name", Name100, SidecarValues.TypeSource.Inferred),
        ]);
    }

    // The optional filter: the copy in "IS NULL" binds as xml and as int, so it types nothing and is ignored; the copy
    // compared with the column fails as xml, and is not probed as int.
    [Fact]
    public async Task DescribeAsync_OptionalFilter_ProbesEachCopyAndTakesTheTypeOfTheOneThatConstrains()
    {
        const string Renamed = "SELECT id FROM dbo.users WHERE (@name_1 IS NULL OR name = @name_2)";
        var parameters = new SqlServerParametersRequest(Renamed, null);
        var xmlFirst = new SqlServerProbeRequest(Renamed, "@name_1 xml, @name_2 nvarchar(100)");
        var intFirst = new SqlServerProbeRequest(Renamed, "@name_1 int, @name_2 nvarchar(100)");
        var xmlSecond = new SqlServerProbeRequest(Renamed, "@name_1 int, @name_2 xml");
        var script = new SqlServerScript()
            .Add(
                "parameters",
                parameters,
                new SqlServerParametersAnswer(
                    [IntRow with { Name = "@name_1" }, Name100Row with { Name = "@name_2" }],
                    null
                )
            )
            .Add("probe", xmlFirst, new SqlServerProbeAnswer(true, null))
            .Add("probe", intFirst, new SqlServerProbeAnswer(true, null))
            .Add("probe", xmlSecond, new SqlServerProbeAnswer(false, null));

        var (described, asked) = await script.DescribeAsync(
            Of(SqlDialectRules.SqlServer, "SELECT id FROM dbo.users WHERE (@name IS NULL OR name = @name)")
        );

        asked.ShouldBe([
            Call("parameters", parameters),
            Call("probe", xmlFirst),
            Call("probe", intFirst),
            Call("probe", xmlSecond),
        ]);
        described
            .Value.ShouldNotBeNull()
            .Parameters.ShouldBe([
                new DescribedParameter("name", Name100, SidecarValues.TypeSource.InferredFromCopies),
            ]);
    }

    [Fact]
    public async Task DescribeAsync_CopiesAgree_AsksNoProbe()
    {
        const string Renamed = "SELECT id FROM dbo.orders WHERE id = @p_1 OR user_id = @p_2";
        var parameters = new SqlServerParametersRequest(Renamed, null);
        var script = new SqlServerScript().Add(
            "parameters",
            parameters,
            new SqlServerParametersAnswer([IntRow with { Name = "@p_1" }, IntRow with { Name = "@p_2" }], null)
        );

        var (described, asked) = await script.DescribeAsync(
            Of(SqlDialectRules.SqlServer, "SELECT id FROM dbo.orders WHERE id = @p OR user_id = @p")
        );

        asked.ShouldBe([Call("parameters", parameters)]);
        described
            .Value.ShouldNotBeNull()
            .Parameters.ShouldBe([new DescribedParameter("p", Int, SidecarValues.TypeSource.InferredFromCopies)]);
    }

    // The declared type is the server's reading of the declaration, asked before "parameters", which then leaves the
    // declared parameter out of its answer.
    [Fact]
    public async Task DescribeAsync_DeclaredParameter_AsksDeclaredFirstThenParametersUnderItsDeclaration()
    {
        const string Sql = "SELECT id FROM dbo.orders WHERE user_id = @tenant AND id = @id";
        var declared = new SqlServerDeclaredRequest("SELECT @tenant AS [1]", "@tenant int");
        var parameters = new SqlServerParametersRequest(Sql, "@tenant int");
        var script = new SqlServerScript()
            .Add(
                "declared",
                declared,
                new SqlServerDeclaredAnswer(
                    [new SqlServerDeclaredRow(1, "int", 4, 10, 0, null, null, null)],
                    null,
                    null
                )
            )
            .Add("parameters", parameters, new SqlServerParametersAnswer([IntRow with { Name = "@id" }], null));

        var (described, asked) = await script.DescribeAsync(Of(SqlDialectRules.SqlServer, Sql, ("tenant", " int ")));

        asked.ShouldBe([Call("declared", declared), Call("parameters", parameters)]);
        described
            .Value.ShouldNotBeNull()
            .Parameters.ShouldBe([
                new DescribedParameter("tenant", Int, SidecarValues.TypeSource.Declared),
                new DescribedParameter("id", Int, SidecarValues.TypeSource.Inferred),
            ]);
    }

    [Fact]
    public async Task DescribeAsync_DeclaredTypeTheGrammarRefuses_IsSqlsrc228AndAsksNothing()
    {
        var (described, asked) = await new SqlServerScript().DescribeAsync(
            Of(SqlDialectRules.SqlServer, "SELECT id FROM dbo.users WHERE id = @p", ("p", "int; DROP TABLE dbo.victim"))
        );

        var failure = described.Failure.ShouldNotBeNull();
        failure.Descriptor.Id.ShouldBe("SQLSRC228");
        failure.Arguments[0].ShouldBe("int; DROP TABLE dbo.victim");
        failure.Arguments[3].ShouldBe(DeclaredTypeText.NotATypeName + ": " + DeclaredTypeText.Semicolon);
        asked.ShouldBeEmpty();
    }

    // A list that lacks a parameter the server reports, as SqlSource's reading of a DECLARE can make one: the list is
    // the test's.
    [Fact]
    public async Task DescribeAsync_ServerReportsAParameterTheListLacks_IsSqlsrc231()
    {
        const string Sql = "SELECT TOP (@n) id, @p + 1 AS q FROM dbo.users";
        var parameters = new SqlServerParametersRequest(Sql, null);
        var script = new SqlServerScript().Add(
            "parameters",
            parameters,
            new SqlServerParametersAnswer(
                [new SqlServerParameterRow("@n", "bigint", 8, 19, 0, null, null, null), IntRow with { Name = "@p" }],
                null
            )
        );

        var (described, asked) = await script.DescribeAsync(Listed(Sql, Inferred("n")));

        asked.ShouldBe([Call("parameters", parameters)]);
        var failure = described.Failure.ShouldNotBeNull();
        failure.Descriptor.Id.ShouldBe("SQLSRC231");
        failure.Arguments.ShouldBe([
            "SQL Server",
            Query,
            "SQL Server reads '@p' as a parameter and SqlSource does not",
        ]);
    }

    // The lists are compared before any probe: a text the two read differently is not probed.
    [Fact]
    public async Task DescribeAsync_ListsDifferAndCopiesDisagree_IsSqlsrc231AndAsksNoProbe()
    {
        const string Renamed = "SELECT id FROM dbo.users WHERE id = @p_1 AND name = @p_2 AND id = @q";
        var parameters = new SqlServerParametersRequest(Renamed, null);
        var script = new SqlServerScript().Add(
            "parameters",
            parameters,
            new SqlServerParametersAnswer(
                [IntRow with { Name = "@p_1" }, Name100Row with { Name = "@p_2" }, IntRow with { Name = "@q" }],
                null
            )
        );

        var (described, asked) = await script.DescribeAsync(
            Listed("SELECT id FROM dbo.users WHERE id = @p AND name = @p AND id = @q", Inferred("p"))
        );

        asked.ShouldBe([Call("parameters", parameters)]);
        described.Failure.ShouldNotBeNull().Descriptor.Id.ShouldBe("SQLSRC231");
    }

    [Fact]
    public async Task DescribeAsync_CopiesThatConstrainDisagree_IsSqlsrc262()
    {
        const string Renamed = "SELECT id FROM dbo.users WHERE id = @p_1 AND name = @p_2";
        var parameters = new SqlServerParametersRequest(Renamed, null);
        var script = new SqlServerScript()
            .Add(
                "parameters",
                parameters,
                new SqlServerParametersAnswer([IntRow with { Name = "@p_1" }, Name100Row with { Name = "@p_2" }], null)
            )
            .Add(
                "probe",
                new SqlServerProbeRequest(Renamed, "@p_1 xml, @p_2 nvarchar(100)"),
                new SqlServerProbeAnswer(false, null)
            )
            .Add(
                "probe",
                new SqlServerProbeRequest(Renamed, "@p_1 int, @p_2 xml"),
                new SqlServerProbeAnswer(false, null)
            );

        var (described, _) = await script.DescribeAsync(
            Of(SqlDialectRules.SqlServer, "SELECT id FROM dbo.users WHERE id = @p AND name = @p")
        );

        var failure = described.Failure.ShouldNotBeNull();
        failure.Descriptor.Id.ShouldBe("SQLSRC262");
        failure.Arguments.ShouldBe(["@p", Query, "int", "nvarchar(100)"]);
    }

    // A probe whose connection was lost says nothing of the copy: the describe ends with the error.
    [Fact]
    public async Task DescribeAsync_ProbeAnswersAnError_IsSqlsrc225AtDescribeParameters()
    {
        const string Renamed = "SELECT id FROM dbo.users WHERE id = @p_1 AND name = @p_2";
        var lost = new SqlServerServerError([new SqlServerErrorRow(0, 0, 0, ConnectionErrors.LostConnection)]);
        var script = new SqlServerScript()
            .Add(
                "parameters",
                new SqlServerParametersRequest(Renamed, null),
                new SqlServerParametersAnswer([IntRow with { Name = "@p_1" }, Name100Row with { Name = "@p_2" }], null)
            )
            .Add(
                "probe",
                new SqlServerProbeRequest(Renamed, "@p_1 xml, @p_2 nvarchar(100)"),
                new SqlServerProbeAnswer(null, lost)
            );

        var (described, _) = await script.DescribeAsync(
            Of(SqlDialectRules.SqlServer, "SELECT id FROM dbo.users WHERE id = @p AND name = @p")
        );

        var failure = described.Failure.ShouldNotBeNull();
        failure.Descriptor.Id.ShouldBe("SQLSRC225");
        failure.Step.ShouldBe(DescribeStep.DescribeParameters);
        failure.ServerLines.ShouldBe(["the connection to the server was lost"]);
    }

    [Fact]
    public async Task DescribeAsync_DeclaredAnswersErrorRows_IsSqlsrc228AndAsksNoParameters()
    {
        var declared = new SqlServerDeclaredRequest("SELECT @p AS [1]", "@p dbo.Nope");
        var script = new SqlServerScript().Add(
            "declared",
            declared,
            new SqlServerDeclaredAnswer(
                null,
                [
                    new SqlServerDescribeError(
                        2715,
                        1,
                        "Column, parameter, or variable #1: Cannot find data type dbo.Nope."
                    ),
                ],
                null
            )
        );

        var (described, asked) = await script.DescribeAsync(
            Of(SqlDialectRules.SqlServer, "SELECT id FROM dbo.users WHERE email = @p", ("p", "dbo.Nope"))
        );

        asked.ShouldBe([Call("declared", declared)]);
        var failure = described.Failure.ShouldNotBeNull();
        failure.Descriptor.Id.ShouldBe("SQLSRC228");
        failure.Arguments[3].ShouldBe(SqlServerErrors.NoSuchType);
    }

    private static (string Method, string Request) Call<TRequest>(string method, TRequest request) =>
        (method, CaptureJson.KeyOf(CaptureJson.ToElement(request)));

    private static Task<Outcome<IDescribeSession, DescribeFailure>> OpenAsync(IDescribeExchange exchange) =>
        new SqlServerDescriber().OpenAsync(SqlServerScript.Database, FixtureReplay.Connection, exchange);
}
