using System.Globalization;
using System.Linq;
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

    // The answer of "columns" for a statement with no rows, for a test of what is asked before it.
    private static readonly SqlServerColumnsAnswer NoRows = new([], null, null);

    private static readonly SqlServerServerError Lost = new([
        new SqlServerErrorRow(0, 0, 0, ConnectionErrors.LostConnection),
    ]);

    // dbo.users.id as browse mode 2 describes it in the session's database.
    private static readonly SqlServerColumnRow IdColumn = new(
        IsHidden: false,
        ColumnOrdinal: 1,
        Name: "id",
        IsNullable: false,
        SystemTypeName: "int",
        MaxLength: 4,
        Precision: 10,
        Scale: 0,
        UserTypeSchema: null,
        UserTypeName: null,
        AssemblyQualifiedTypeName: null,
        SourceServer: null,
        SourceDatabase: SqlServerScript.Database,
        SourceSchema: "dbo",
        SourceTable: "users",
        SourceColumn: "id",
        IsIdentityColumn: true,
        IsComputedColumn: false,
        SourceType: "U"
    );

    private static readonly SqlServerColumnRow NameColumn = IdColumn with
    {
        ColumnOrdinal = 2,
        Name = "name",
        SystemTypeName = "nvarchar(100)",
        MaxLength = 200,
        Precision = 0,
        SourceColumn = "name",
        IsIdentityColumn = false,
    };

    private static readonly SqlServerColumnRow NicknameColumn = NameColumn with
    {
        ColumnOrdinal = 3,
        Name = "nickname",
        IsNullable = true,
        SystemTypeName = "nvarchar(50)",
        MaxLength = 100,
        SourceColumn = "nickname",
    };

    // The one hidden row that browse mode 2 adds.
    private static readonly SqlServerColumnRow Rowstat = IdColumn with
    {
        IsHidden = true,
        Name = "ROWSTAT",
        SourceDatabase = null,
        SourceSchema = null,
        SourceTable = null,
        SourceColumn = null,
        SourceType = null,
        IsIdentityColumn = false,
    };

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
        var columns = new SqlServerColumnsRequest(Sql, "@id int, @name nvarchar(100)");
        var script = new SqlServerScript()
            .Add(
                "parameters",
                parameters,
                new SqlServerParametersAnswer([IntRow with { Name = "@id" }, Name100Row with { Name = "@name" }], null)
            )
            .Add("columns", columns, NoRows);

        var (described, asked) = await script.DescribeAsync(Of(SqlDialectRules.SqlServer, Sql));

        asked.ShouldBe([Call("parameters", parameters), Call("columns", columns)]);
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
        const string Sql = "SELECT id FROM dbo.users WHERE (@name IS NULL OR name = @name)";
        const string Renamed = "SELECT id FROM dbo.users WHERE (@name_1 IS NULL OR name = @name_2)";
        var parameters = new SqlServerParametersRequest(Renamed, null);
        var xmlFirst = new SqlServerProbeRequest(Renamed, "@name_1 xml, @name_2 nvarchar(100)");
        var intFirst = new SqlServerProbeRequest(Renamed, "@name_1 int, @name_2 nvarchar(100)");
        var xmlSecond = new SqlServerProbeRequest(Renamed, "@name_1 int, @name_2 xml");
        var columns = new SqlServerColumnsRequest(Sql, "@name nvarchar(100)");
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
            .Add("probe", xmlSecond, new SqlServerProbeAnswer(false, null))
            .Add("columns", columns, NoRows);

        var (described, asked) = await script.DescribeAsync(Of(SqlDialectRules.SqlServer, Sql));

        asked.ShouldBe([
            Call("parameters", parameters),
            Call("probe", xmlFirst),
            Call("probe", intFirst),
            Call("probe", xmlSecond),
            Call("columns", columns),
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
        const string Sql = "SELECT id FROM dbo.orders WHERE id = @p OR user_id = @p";
        const string Renamed = "SELECT id FROM dbo.orders WHERE id = @p_1 OR user_id = @p_2";
        var parameters = new SqlServerParametersRequest(Renamed, null);
        var columns = new SqlServerColumnsRequest(Sql, "@p int");
        var script = new SqlServerScript()
            .Add(
                "parameters",
                parameters,
                new SqlServerParametersAnswer([IntRow with { Name = "@p_1" }, IntRow with { Name = "@p_2" }], null)
            )
            .Add("columns", columns, NoRows);

        var (described, asked) = await script.DescribeAsync(Of(SqlDialectRules.SqlServer, Sql));

        asked.ShouldBe([Call("parameters", parameters), Call("columns", columns)]);
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
        var columns = new SqlServerColumnsRequest(Sql, "@tenant int, @id int");
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
            .Add("parameters", parameters, new SqlServerParametersAnswer([IntRow with { Name = "@id" }], null))
            .Add("columns", columns, NoRows);

        var (described, asked) = await script.DescribeAsync(Of(SqlDialectRules.SqlServer, Sql, ("tenant", " int ")));

        asked.ShouldBe([Call("declared", declared), Call("parameters", parameters), Call("columns", columns)]);
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
        var script = new SqlServerScript()
            .Add(
                "parameters",
                new SqlServerParametersRequest(Renamed, null),
                new SqlServerParametersAnswer([IntRow with { Name = "@p_1" }, Name100Row with { Name = "@p_2" }], null)
            )
            .Add(
                "probe",
                new SqlServerProbeRequest(Renamed, "@p_1 xml, @p_2 nvarchar(100)"),
                new SqlServerProbeAnswer(null, Lost)
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

    // Each column as the server gave it, the hidden row dropped; the table asked for the one origin; and the match.
    [Fact]
    public async Task DescribeAsync_Rows_RecordsColumnsAndMatch()
    {
        const string Sql = "SELECT id, name FROM dbo.users WHERE id = @id";
        var columns = new SqlServerColumnsRequest(Sql, "@id int");
        var table = new SqlServerTableRequest("dbo", "users");
        var script = new SqlServerScript()
            .Add(
                "parameters",
                new SqlServerParametersRequest(Sql, null),
                new SqlServerParametersAnswer([IntRow with { Name = "@id" }], null)
            )
            .Add("columns", columns, new SqlServerColumnsAnswer([IdColumn, NameColumn, Rowstat], null, null))
            .Add("table", table, new SqlServerColumnsAnswer([IdColumn, NameColumn, Rowstat], null, null));

        var (described, asked) = await script.DescribeAsync(Of(SqlDialectRules.SqlServer, Sql));

        asked.Select(call => call.Method).ShouldBe(["parameters", "columns", "table"]);
        asked[1].ShouldBe(Call("columns", columns));
        asked[2].ShouldBe(Call("table", table));
        var description = described.Value.ShouldNotBeNull();
        description.ResultKind.ShouldBe(SidecarResultKind.Rows);
        description.Columns.ShouldBe([
            new SidecarColumn(
                0,
                "id",
                Int,
                false,
                SidecarValues.NullableSource.Server,
                new SidecarOrigin("dbo", "users", "id"),
                true,
                false
            ),
            new SidecarColumn(
                1,
                "name",
                Name100,
                false,
                SidecarValues.NullableSource.Server,
                new SidecarOrigin("dbo", "users", "name"),
                false,
                false
            ),
        ]);
        description.MatchesTable.ShouldBe(new SidecarTable("dbo", "users"));
        description.TableMatch.ShouldBe(SidecarValues.TableMatch.Matched);
        description.Plan.ShouldBe(SidecarValues.Plan.Skipped);
    }

    // An UPDATE with no OUTPUT: "columns" answers no row, and nothing of a result is recorded.
    [Fact]
    public async Task DescribeAsync_NoRows_IsNone()
    {
        const string Sql = "UPDATE dbo.users SET nickname = N'x' WHERE id = @id";
        var columns = new SqlServerColumnsRequest(Sql, "@id int");
        var script = new SqlServerScript()
            .Add(
                "parameters",
                new SqlServerParametersRequest(Sql, null),
                new SqlServerParametersAnswer([IntRow with { Name = "@id" }], null)
            )
            .Add("columns", columns, NoRows);

        var (described, asked) = await script.DescribeAsync(Of(SqlDialectRules.SqlServer, Sql));

        asked.Select(call => call.Method).ShouldBe(["parameters", "columns"]);
        var description = described.Value.ShouldNotBeNull();
        description.ResultKind.ShouldBe(SidecarResultKind.None);
        description.Columns.ShouldBeNull();
        description.MatchesTable.ShouldBeNull();
        description.Plan.ShouldBeNull();
        description.TableMatch.ShouldBeNull();
    }

    // The SQL as written, with every parameter of the list declared in its order: a declared one as its declaration
    // reads, a reused one once under its own name, and an inferred one at the type the server gave it, an alias type by
    // its schema and name, each in brackets, and a CLR type of sys by its name.
    [Fact]
    public async Task DescribeAsync_ColumnsParams_DeclareEveryParameter()
    {
        const string Sql =
            "SELECT id FROM dbo.users WHERE tenant = @tenant AND email = @email AND node = @node "
            + "AND (name = @name OR nickname = @name)";
        const string Renamed =
            "SELECT id FROM dbo.users WHERE tenant = @tenant AND email = @email AND node = @node "
            + "AND (name = @name_1 OR nickname = @name_2)";
        var columns = new SqlServerColumnsRequest(
            Sql,
            "@tenant int, @email [dbo].[Email], @node hierarchyid, @name nvarchar(100)"
        );
        var script = new SqlServerScript()
            .Add(
                "declared",
                new SqlServerDeclaredRequest("SELECT @tenant AS [1]", "@tenant int"),
                new SqlServerDeclaredAnswer(
                    [new SqlServerDeclaredRow(1, "int", 4, 10, 0, null, null, null)],
                    null,
                    null
                )
            )
            .Add(
                "parameters",
                new SqlServerParametersRequest(Renamed, "@tenant int"),
                new SqlServerParametersAnswer(
                    [
                        new SqlServerParameterRow("@email", "nvarchar(320)", 640, 0, 0, "dbo", "Email", null),
                        new SqlServerParameterRow(
                            "@node",
                            "hierarchyid",
                            892,
                            0,
                            0,
                            "sys",
                            "hierarchyid",
                            "Microsoft.SqlServer.Types.SqlHierarchyId, Microsoft.SqlServer.Types"
                        ),
                        Name100Row with
                        {
                            Name = "@name_1",
                        },
                        Name100Row with
                        {
                            Name = "@name_2",
                        },
                    ],
                    null
                )
            )
            .Add("columns", columns, NoRows);

        var (described, asked) = await script.DescribeAsync(Of(SqlDialectRules.SqlServer, Sql, ("tenant", "int")));

        described.Failure.ShouldBeNull(described.Failure?.Descriptor.Id);
        asked.Select(call => call.Method).ShouldBe(["declared", "parameters", "columns"]);
        asked[2].ShouldBe(Call("columns", columns));
    }

    // A self-join: two columns of one table, whose list is the table's in part, ask "table" once.
    [Fact]
    public async Task DescribeAsync_OneTable_AsksTableOnce()
    {
        const string Sql = "SELECT a.id, b.name FROM dbo.users AS a JOIN dbo.users AS b ON b.id = a.id";
        var columns = new SqlServerColumnsRequest(Sql, null);
        var table = new SqlServerTableRequest("dbo", "users");
        var script = new SqlServerScript()
            .Add("parameters", new SqlServerParametersRequest(Sql, null), new SqlServerParametersAnswer([], null))
            .Add("columns", columns, new SqlServerColumnsAnswer([IdColumn, NameColumn, Rowstat], null, null))
            .Add(
                "table",
                table,
                new SqlServerColumnsAnswer([IdColumn, NameColumn, NicknameColumn, Rowstat], null, null)
            );

        var (described, asked) = await script.DescribeAsync(Of(SqlDialectRules.SqlServer, Sql));

        asked.ShouldBe([
            Call("parameters", new SqlServerParametersRequest(Sql, null)),
            Call("columns", columns),
            Call("table", table),
        ]);
        var description = described.Value.ShouldNotBeNull();
        description.ResultKind.ShouldBe(SidecarResultKind.Rows);
        description.MatchesTable.ShouldBeNull();
        description.TableMatch.ShouldBe(SidecarValues.TableMatch.ColumnsDiffer);
        description.Plan.ShouldBe(SidecarValues.Plan.Skipped);
    }

    [Fact]
    public async Task DescribeAsync_TwoTables_AsksNoTable()
    {
        const string Sql = "SELECT u.id, o.id AS order_id FROM dbo.users AS u JOIN dbo.orders AS o ON o.user_id = u.id";
        var columns = new SqlServerColumnsRequest(Sql, null);
        var orderId = IdColumn with { ColumnOrdinal = 2, Name = "order_id", SourceTable = "orders" };
        var script = new SqlServerScript()
            .Add("parameters", new SqlServerParametersRequest(Sql, null), new SqlServerParametersAnswer([], null))
            .Add("columns", columns, new SqlServerColumnsAnswer([IdColumn, orderId, Rowstat], null, null));

        var (described, asked) = await script.DescribeAsync(Of(SqlDialectRules.SqlServer, Sql));

        asked.Select(call => call.Method).ShouldBe(["parameters", "columns"]);
        var description = described.Value.ShouldNotBeNull();
        description.Columns.ShouldNotBeNull().Count.ShouldBe(2);
        description.MatchesTable.ShouldBeNull();
        description.TableMatch.ShouldBe(SidecarValues.TableMatch.SeveralTables);
        description.Plan.ShouldBe(SidecarValues.Plan.Skipped);
    }

    // COUNT(*) names no source, so no table is asked.
    [Fact]
    public async Task DescribeAsync_NoOrigin_AsksNoTable()
    {
        const string Sql = "SELECT COUNT(*) AS n FROM dbo.users";
        var columns = new SqlServerColumnsRequest(Sql, null);
        var count = IdColumn with
        {
            Name = "n",
            IsNullable = true,
            SourceDatabase = null,
            SourceSchema = null,
            SourceTable = null,
            SourceColumn = null,
            SourceType = null,
            IsIdentityColumn = null,
            IsComputedColumn = null,
        };
        var script = new SqlServerScript()
            .Add("parameters", new SqlServerParametersRequest(Sql, null), new SqlServerParametersAnswer([], null))
            .Add("columns", columns, new SqlServerColumnsAnswer([count], null, null));

        var (described, asked) = await script.DescribeAsync(Of(SqlDialectRules.SqlServer, Sql));

        asked.Select(call => call.Method).ShouldBe(["parameters", "columns"]);
        var description = described.Value.ShouldNotBeNull();
        description.Columns.ShouldBe([
            new SidecarColumn(0, "n", Int, true, SidecarValues.NullableSource.Server, null, null, null),
        ]);
        description.MatchesTable.ShouldBeNull();
        description.TableMatch.ShouldBe(SidecarValues.TableMatch.NoOrigin);
    }

    // The server names a table-valued function as its columns' source, and SELECT * from it fails without its
    // arguments: a column of a function has no origin, so no table is asked.
    [Theory]
    [InlineData("IF")]
    [InlineData("TF")]
    public async Task DescribeAsync_ColumnsOfATableValuedFunction_AsksNoTable(string type)
    {
        const string Sql = "SELECT id FROM dbo.orders_of(1)";
        var columns = new SqlServerColumnsRequest(Sql, null);
        var id = IdColumn with { SourceTable = "orders_of", SourceType = type, IsIdentityColumn = false };
        var script = new SqlServerScript()
            .Add("parameters", new SqlServerParametersRequest(Sql, null), new SqlServerParametersAnswer([], null))
            .Add("columns", columns, new SqlServerColumnsAnswer([id, Rowstat], null, null));

        var (described, asked) = await script.DescribeAsync(Of(SqlDialectRules.SqlServer, Sql));

        asked.Select(call => call.Method).ShouldBe(["parameters", "columns"]);
        var description = described.Value.ShouldNotBeNull();
        description.Columns.ShouldNotBeNull().Single().Origin.ShouldBeNull();
        description.MatchesTable.ShouldBeNull();
        description.TableMatch.ShouldBe(SidecarValues.TableMatch.NoOrigin);
    }

    // A "table" whose connection was lost, or whose describe answers error rows, fails the query at the table match.
    [Fact]
    public async Task DescribeAsync_TableFails_Is225AtTableMatch()
    {
        const string Sql = "SELECT id FROM dbo.users";
        var lost = await TableAnsweringAsync(Sql, new SqlServerColumnsAnswer(null, null, Lost));
        var refused = await TableAnsweringAsync(
            Sql,
            new SqlServerColumnsAnswer(
                null,
                [new SqlServerDescribeError(208, 7, "Invalid object name 'dbo.users'.")],
                null
            )
        );

        lost.Descriptor.Id.ShouldBe("SQLSRC225");
        lost.Step.ShouldBe(DescribeStep.TableMatch);
        lost.ServerLines.ShouldBe([ConnectionErrors.LostConnection]);
        refused.Descriptor.Id.ShouldBe("SQLSRC225");
        refused.Step.ShouldBe(DescribeStep.TableMatch);
        refused.ServerLines.ShouldBe(["Msg 208: Invalid object name 'dbo.users'."]);
    }

    // "columns" that fails is the query's failure at describe columns: a lost connection, and the error rows by their
    // type.
    [Fact]
    public async Task DescribeAsync_ColumnsFails_IsTheFailureAtDescribeColumns()
    {
        const string Sql = "SELECT id FROM dbo.users";
        var lost = await ColumnsAnsweringAsync(Sql, new SqlServerColumnsAnswer(null, null, Lost));
        var dynamic = await ColumnsAnsweringAsync(
            Sql,
            new SqlServerColumnsAnswer(
                null,
                [
                    new SqlServerDescribeError(
                        11514,
                        4,
                        "The metadata could not be determined because of dynamic SQL."
                    ),
                    new SqlServerDescribeError(11501, 4, "The batch could not be analyzed."),
                ],
                null
            )
        );

        lost.Descriptor.Id.ShouldBe("SQLSRC225");
        lost.Step.ShouldBe(DescribeStep.DescribeColumns);
        lost.ServerLines.ShouldBe([ConnectionErrors.LostConnection]);
        dynamic.Descriptor.Id.ShouldBe("SQLSRC264");
        dynamic.Step.ShouldBe(DescribeStep.DescribeColumns);
    }

    // sp_describe_undeclared_parameters reports a temporary table of a batch of several statements as 208 and 11501;
    // "columns", asked with the declared parameters alone, gives the cause.
    [Fact]
    public async Task DescribeAsync_Refused11501_AsksColumnsWithDeclarationsAlone()
    {
        const string Sql = "SELECT id INTO #t FROM dbo.users WHERE tenant = @tenant AND id = @id; SELECT id FROM #t";
        var parameters = new SqlServerParametersRequest(Sql, "@tenant int");
        var columns = new SqlServerColumnsRequest(Sql, "@tenant int");
        var script = new SqlServerScript()
            .Add(
                "declared",
                new SqlServerDeclaredRequest("SELECT @tenant AS [1]", "@tenant int"),
                new SqlServerDeclaredAnswer(
                    [new SqlServerDeclaredRow(1, "int", 4, 10, 0, null, null, null)],
                    null,
                    null
                )
            )
            .Add(
                "parameters",
                parameters,
                new SqlServerParametersAnswer(
                    null,
                    new SqlServerServerError([
                        new SqlServerErrorRow(208, 16, 1, "Invalid object name '#t'."),
                        new SqlServerErrorRow(11501, 16, 1, "The batch could not be analyzed."),
                    ])
                )
            )
            .Add(
                "columns",
                columns,
                new SqlServerColumnsAnswer(
                    null,
                    [
                        new SqlServerDescribeError(11525, 10, "The metadata could not be determined: '#t'."),
                        new SqlServerDescribeError(11501, 10, "The batch could not be analyzed."),
                    ],
                    null
                )
            );

        var (described, asked) = await script.DescribeAsync(Of(SqlDialectRules.SqlServer, Sql, ("tenant", "int")));

        asked.Select(call => call.Method).ShouldBe(["declared", "parameters", "columns"]);
        asked[2].ShouldBe(Call("columns", columns));
        var failure = described.Failure.ShouldNotBeNull();
        failure.Descriptor.Id.ShouldBe("SQLSRC265");
        failure.Step.ShouldBe(DescribeStep.DescribeColumns);
    }

    // With no declared parameter, the declarations of "columns" are none, as those of "parameters" are.
    [Fact]
    public async Task DescribeAsync_Refused11501WithNoDeclaration_AsksColumnsWithNoParams()
    {
        const string Sql = "SELECT id INTO #t FROM dbo.users WHERE id = @id; SELECT id FROM #t";
        var refused = new SqlServerServerError([
            new SqlServerErrorRow(208, 16, 1, "Invalid object name '#t'."),
            new SqlServerErrorRow(11501, 16, 1, "The batch could not be analyzed."),
        ]);
        var columns = new SqlServerColumnsRequest(Sql, null);
        var script = new SqlServerScript()
            .Add("parameters", new SqlServerParametersRequest(Sql, null), new SqlServerParametersAnswer(null, refused))
            .Add("columns", columns, new SqlServerColumnsAnswer(null, null, Lost));

        var (described, asked) = await script.DescribeAsync(Of(SqlDialectRules.SqlServer, Sql));

        asked.Select(call => call.Method).ShouldBe(["parameters", "columns"]);
        asked[1].ShouldBe(Call("columns", columns));
        var failure = described.Failure.ShouldNotBeNull();
        failure.Descriptor.Id.ShouldBe("SQLSRC225");
        failure.Step.ShouldBe(DescribeStep.DescribeParameters);
        failure.ServerLines.ShouldBe([
            "Msg 208, Level 16, State 1: Invalid object name '#t'.",
            "Msg 11501, Level 16, State 1: The batch could not be analyzed.",
        ]);
    }

    [Fact]
    public async Task DescribeAsync_OtherParametersError_AsksNoColumns()
    {
        const string Sql = "SELECT id FROM dbo.users WHERE id = @p AND name = @p";
        var script = new SqlServerScript().Add(
            "parameters",
            new SqlServerParametersRequest("SELECT id FROM dbo.users WHERE id = @p_1 AND name = @p_2", null),
            new SqlServerParametersAnswer(
                null,
                new SqlServerServerError([new SqlServerErrorRow(102, 15, 1, "Incorrect syntax near 'name'.")])
            )
        );

        var (described, asked) = await script.DescribeAsync(Of(SqlDialectRules.SqlServer, Sql));

        asked.Select(call => call.Method).ShouldBe(["parameters"]);
        var failure = described.Failure.ShouldNotBeNull();
        failure.Descriptor.Id.ShouldBe("SQLSRC225");
        failure.Step.ShouldBe(DescribeStep.DescribeParameters);
    }

    // SELECT 1: the server gives the column no name, and nothing is asked after "columns".
    [Fact]
    public async Task DescribeAsync_NullName_Is266()
    {
        const string Sql = "SELECT id, 1 FROM dbo.users";
        var unnamed = IdColumn with
        {
            ColumnOrdinal = 2,
            Name = null,
            SourceDatabase = null,
            SourceSchema = null,
            SourceTable = null,
            SourceColumn = null,
            SourceType = null,
        };
        var script = new SqlServerScript()
            .Add("parameters", new SqlServerParametersRequest(Sql, null), new SqlServerParametersAnswer([], null))
            .Add(
                "columns",
                new SqlServerColumnsRequest(Sql, null),
                new SqlServerColumnsAnswer([IdColumn, unnamed, Rowstat], null, null)
            );

        var (described, asked) = await script.DescribeAsync(Of(SqlDialectRules.SqlServer, Sql));

        asked.Select(call => call.Method).ShouldBe(["parameters", "columns"]);
        var failure = described.Failure.ShouldNotBeNull();
        failure.Descriptor.Id.ShouldBe("SQLSRC266");
        failure.Arguments.ShouldBe(["2", Query]);
        failure.Step.ShouldBe(DescribeStep.DescribeColumns);
    }

    // The failure of a describe of the SQL, which has no parameter, when "columns" answers as given.
    private static async Task<DescribeFailure> ColumnsAnsweringAsync(string sql, SqlServerColumnsAnswer answer)
    {
        var script = new SqlServerScript()
            .Add("parameters", new SqlServerParametersRequest(sql, null), new SqlServerParametersAnswer([], null))
            .Add("columns", new SqlServerColumnsRequest(sql, null), answer);

        var (described, _) = await script.DescribeAsync(Of(SqlDialectRules.SqlServer, sql));

        return described.Failure.ShouldNotBeNull();
    }

    // The failure of a describe of the SQL, whose one column is dbo.users.id, when "table" answers as given.
    private static async Task<DescribeFailure> TableAnsweringAsync(string sql, SqlServerColumnsAnswer answer)
    {
        var script = new SqlServerScript()
            .Add("parameters", new SqlServerParametersRequest(sql, null), new SqlServerParametersAnswer([], null))
            .Add(
                "columns",
                new SqlServerColumnsRequest(sql, null),
                new SqlServerColumnsAnswer([IdColumn, Rowstat], null, null)
            )
            .Add("table", new SqlServerTableRequest("dbo", "users"), answer);

        var (described, _) = await script.DescribeAsync(Of(SqlDialectRules.SqlServer, sql));

        return described.Failure.ShouldNotBeNull();
    }

    private static (string Method, string Request) Call<TRequest>(string method, TRequest request) =>
        (method, CaptureJson.KeyOf(CaptureJson.ToElement(request)));

    private static Task<Outcome<IDescribeSession, DescribeFailure>> OpenAsync(IDescribeExchange exchange) =>
        new SqlServerDescriber().OpenAsync(SqlServerScript.Database, FixtureReplay.Connection, exchange);
}
