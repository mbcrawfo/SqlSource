using System;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Parsing;
using SqlSource.Snapshot;
using SqlSource.Tool.Describing;
using SqlSource.Tool.Describing.SqlServer;
using SqlSource.Tool.Logging;
using SqlSource.Tool.Testing;
using Xunit;
using static SqlSource.Tool.Testing.DescribeRequests;

namespace SqlSource.Tool.Tests.Describing.SqlServer;

// What the describer says in the run's log of parameters, probe, declared, columns and table, of each column and of the
// table match: each event's members for an answer with rows and for an error, and, through a session, the order the
// events are written in, the query each names, and how long each call took.
public sealed class SqlServerEventsTests
{
    private static readonly TimeSpan CallTime = TimeSpan.FromMilliseconds(7);

    private static readonly SqlServerServerError Lost = new([
        new SqlServerErrorRow(0, 0, 0, ConnectionErrors.LostConnection),
    ]);

    private static readonly SqlServerServerError Refused = new([
        new SqlServerErrorRow(11508, 16, 1, "The parameter '@p' is used twice."),
        new SqlServerErrorRow(8180, 16, 1, "Statement(s) could not be prepared."),
    ]);

    // dbo.users.id as browse mode 2 describes it.
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
        IsComputedColumn: false
    );

    // The one hidden row that browse mode 2 adds.
    private static readonly SqlServerColumnRow Rowstat = IdColumn with
    {
        IsHidden = true,
        ColumnOrdinal = 2,
        Name = "ROWSTAT",
        SourceDatabase = null,
        SourceSchema = null,
        SourceTable = null,
        SourceColumn = null,
        IsIdentityColumn = null,
        IsComputedColumn = null,
    };

    private static readonly SqlServerType Int = new("int", 4, 10, 0, null);

    private static LogValue Member(LogMember[] members, string name) =>
        members.Single(member => member.Name == name).Value;

    private static string[] NamesOf(LogMember[] members) => [.. members.Select(member => member.Name)];

    private static LogValue ErrorsOf(params (long Number, long Class, long State, string Message)[] errors) =>
        LogValue.ListOf(
            errors.Select(error =>
                LogValue.MapOf(
                    new("number", error.Number),
                    new("class", error.Class),
                    new("state", error.State),
                    new("message", error.Message)
                )
            )
        );

    // A row of "parameters" as the event writes it: every member, named as the capture names it.
    private static LogValue ParameterMap(
        string name,
        string? type,
        long? length,
        long? precision,
        long? scale,
        string? userSchema = null,
        string? userName = null,
        string? assembly = null
    ) =>
        LogValue.MapOf(
            new("name", name),
            new("suggestedSystemTypeName", type),
            new("suggestedMaxLength", LogValue.Of(length)),
            new("suggestedPrecision", LogValue.Of(precision)),
            new("suggestedScale", LogValue.Of(scale)),
            new("suggestedUserTypeSchema", userSchema),
            new("suggestedUserTypeName", userName),
            new("suggestedAssemblyQualifiedTypeName", assembly)
        );

    // A row of "columns" or "table" as the event writes it: every member, named as the capture names it.
    private static LogValue ColumnRowMap(SqlServerColumnRow row) =>
        LogValue.MapOf(
            new("isHidden", row.IsHidden),
            new("columnOrdinal", row.ColumnOrdinal),
            new("name", row.Name),
            new("isNullable", LogValue.Of(row.IsNullable)),
            new("systemTypeName", row.SystemTypeName),
            new("maxLength", LogValue.Of(row.MaxLength)),
            new("precision", LogValue.Of(row.Precision)),
            new("scale", LogValue.Of(row.Scale)),
            new("userTypeSchema", row.UserTypeSchema),
            new("userTypeName", row.UserTypeName),
            new("assemblyQualifiedTypeName", row.AssemblyQualifiedTypeName),
            new("sourceServer", row.SourceServer),
            new("sourceDatabase", row.SourceDatabase),
            new("sourceSchema", row.SourceSchema),
            new("sourceTable", row.SourceTable),
            new("sourceColumn", row.SourceColumn),
            new("isIdentityColumn", LogValue.Of(row.IsIdentityColumn)),
            new("isComputedColumn", LogValue.Of(row.IsComputedColumn))
        );

    [Fact]
    public void Parameters_RowsOfTheServer_WritesTheSqlTheDeclarationsAndEveryMemberOfEachRow()
    {
        var log = new RecordingLog();
        var request = new SqlServerParametersRequest("SELECT id FROM dbo.users WHERE id = @id", "@tenant int");
        var answer = new SqlServerParametersAnswer(
            [
                new SqlServerParameterRow("@id", "int", 4, 10, 0, null, null, null),
                new SqlServerParameterRow("@e", "nvarchar(320)", 640, 0, 0, "dbo", "Email", "Types, Version=1.0"),
            ],
            null
        );

        SqlServerEvents.Parameters(log, "GetUser", request, answer, 3);

        var (name, members) = log.Events.ShouldHaveSingleItem();
        name.ShouldBe("parameters");
        NamesOf(members).ShouldBe(["query", "sql", "params", "rows", "durationMs"]);
        Member(members, "query").ShouldBe((LogValue)"GetUser");
        Member(members, "sql").ShouldBe((LogValue)"SELECT id FROM dbo.users WHERE id = @id");
        Member(members, "params").ShouldBe((LogValue)"@tenant int");
        Member(members, "rows")
            .ShouldBe(
                LogValue.ListOf([
                    ParameterMap("@id", "int", 4, 10, 0),
                    ParameterMap("@e", "nvarchar(320)", 640, 0, 0, "dbo", "Email", "Types, Version=1.0"),
                ])
            );
        Member(members, "durationMs").ShouldBe((LogValue)3L);
    }

    // Without declarations the request holds none, and a row the server could not type holds nulls.
    [Fact]
    public void Parameters_NoDeclarationsAndARowWithoutAType_WritesNulls()
    {
        var log = new RecordingLog();

        SqlServerEvents.Parameters(
            log,
            "GetUser",
            new SqlServerParametersRequest("SELECT @x", null),
            new SqlServerParametersAnswer(
                [new SqlServerParameterRow("@x", null, null, null, null, null, null, null)],
                null
            ),
            0
        );

        var members = log.Events.ShouldHaveSingleItem().Members;
        Member(members, "params").ShouldBe(LogValue.Null);
        Member(members, "rows").ShouldBe(LogValue.ListOf([ParameterMap("@x", null, null, null, null)]));
    }

    // Every error of the exception is written, in its order, in the place of the rows.
    [Fact]
    public void Parameters_ErrorOfTheServer_WritesEveryErrorInThePlaceOfTheRows()
    {
        var log = new RecordingLog();

        SqlServerEvents.Parameters(
            log,
            "GetUser",
            new SqlServerParametersRequest("SELECT @p, @p", null),
            new SqlServerParametersAnswer(null, Refused),
            5
        );

        var members = log.Events.ShouldHaveSingleItem().Members;
        NamesOf(members).ShouldBe(["query", "sql", "params", "error", "durationMs"]);
        Member(members, "error")
            .ShouldBe(
                ErrorsOf(
                    (11508, 16, 1, "The parameter '@p' is used twice."),
                    (8180, 16, 1, "Statement(s) could not be prepared.")
                )
            );
    }

    // An answer with neither rows nor an error is a bug of the describer: the event holds no rows, as prepare holds
    // no types.
    [Fact]
    public void Parameters_AnswerWithNothing_WritesNoRows()
    {
        var log = new RecordingLog();

        SqlServerEvents.Parameters(
            log,
            "GetUser",
            new SqlServerParametersRequest("SELECT 1", null),
            new SqlServerParametersAnswer(null, null),
            0
        );

        Member(log.Events.ShouldHaveSingleItem().Members, "rows").ShouldBe(LogValue.ListOf([]));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Probe_AnswerThatBindsOrDoesNot_WritesTheRequestAndBinds(bool binds)
    {
        var log = new RecordingLog();

        SqlServerEvents.Probe(
            log,
            "GetUser",
            new SqlServerProbeRequest("SELECT @p_1, @p_2", "@p_1 xml, @p_2 int"),
            new SqlServerProbeAnswer(binds, null),
            4
        );

        var (name, members) = log.Events.ShouldHaveSingleItem();
        name.ShouldBe("probe");
        NamesOf(members).ShouldBe(["query", "sql", "params", "binds", "durationMs"]);
        Member(members, "query").ShouldBe((LogValue)"GetUser");
        Member(members, "sql").ShouldBe((LogValue)"SELECT @p_1, @p_2");
        Member(members, "params").ShouldBe((LogValue)"@p_1 xml, @p_2 int");
        Member(members, "binds").ShouldBe((LogValue)binds);
        Member(members, "durationMs").ShouldBe((LogValue)4L);
    }

    [Fact]
    public void Probe_ErrorOfTheServer_WritesItInThePlaceOfBinds()
    {
        var log = new RecordingLog();

        SqlServerEvents.Probe(
            log,
            "GetUser",
            new SqlServerProbeRequest("SELECT @p", "@p xml"),
            new SqlServerProbeAnswer(null, Lost),
            0
        );

        var members = log.Events.ShouldHaveSingleItem().Members;
        NamesOf(members).ShouldBe(["query", "sql", "params", "error", "durationMs"]);
        Member(members, "error").ShouldBe(ErrorsOf((0, 0, 0, ConnectionErrors.LostConnection)));
    }

    [Fact]
    public void Probe_AnswerWithNothing_WritesBindsAsNull()
    {
        var log = new RecordingLog();

        SqlServerEvents.Probe(
            log,
            "GetUser",
            new SqlServerProbeRequest("SELECT @p", "@p xml"),
            new SqlServerProbeAnswer(null, null),
            0
        );

        Member(log.Events.ShouldHaveSingleItem().Members, "binds").ShouldBe(LogValue.Null);
    }

    [Fact]
    public void Declared_RowOfTheServer_WritesTheSelectTheDeclarationAndEveryMemberOfTheRow()
    {
        var log = new RecordingLog();

        SqlServerEvents.Declared(
            log,
            "GetUser",
            new SqlServerDeclaredRequest("SELECT @e AS [1]", "@e dbo.Email"),
            new SqlServerDeclaredAnswer(
                [new SqlServerDeclaredRow(1, "nvarchar(320)", 640, 0, 0, "dbo", "Email", null)],
                null,
                null
            ),
            2
        );

        var (name, members) = log.Events.ShouldHaveSingleItem();
        name.ShouldBe("declared");
        NamesOf(members).ShouldBe(["query", "sql", "params", "rows", "durationMs"]);
        Member(members, "sql").ShouldBe((LogValue)"SELECT @e AS [1]");
        Member(members, "params").ShouldBe((LogValue)"@e dbo.Email");
        Member(members, "rows")
            .ShouldBe(
                LogValue.ListOf([
                    LogValue.MapOf(
                        new("columnOrdinal", 1L),
                        new("systemTypeName", "nvarchar(320)"),
                        new("maxLength", 640L),
                        new("precision", 0L),
                        new("scale", 0L),
                        new("userTypeSchema", "dbo"),
                        new("userTypeName", "Email"),
                        new("assemblyQualifiedTypeName", LogValue.Null)
                    ),
                ])
            );
    }

    // The error rows of the describe are in the place of the rows, each as a map of its number, type and message.
    [Fact]
    public void Declared_ErrorRowsOfTheDescribe_WritesThemInThePlaceOfTheRows()
    {
        var log = new RecordingLog();

        SqlServerEvents.Declared(
            log,
            "GetUser",
            new SqlServerDeclaredRequest("SELECT @p AS [1]", "@p dbo.Nope"),
            new SqlServerDeclaredAnswer(
                null,
                [
                    new SqlServerDescribeError(2715, 1, "Cannot find data type dbo.Nope."),
                    new SqlServerDescribeError(11501, 1, "The metadata could not be determined."),
                ],
                null
            ),
            0
        );

        var members = log.Events.ShouldHaveSingleItem().Members;
        NamesOf(members).ShouldBe(["query", "sql", "params", "errors", "durationMs"]);
        Member(members, "errors")
            .ShouldBe(
                LogValue.ListOf([
                    LogValue.MapOf(
                        new("number", 2715L),
                        new("type", 1L),
                        new("message", "Cannot find data type dbo.Nope.")
                    ),
                    LogValue.MapOf(
                        new("number", 11501L),
                        new("type", 1L),
                        new("message", "The metadata could not be determined.")
                    ),
                ])
            );
    }

    [Fact]
    public void Declared_ErrorOfTheServer_WritesItInThePlaceOfTheRows()
    {
        var log = new RecordingLog();

        SqlServerEvents.Declared(
            log,
            "GetUser",
            new SqlServerDeclaredRequest("SELECT @p AS [1]", "@p int"),
            new SqlServerDeclaredAnswer(null, null, Lost),
            0
        );

        var members = log.Events.ShouldHaveSingleItem().Members;
        NamesOf(members).ShouldBe(["query", "sql", "params", "error", "durationMs"]);
        Member(members, "error").ShouldBe(ErrorsOf((0, 0, 0, ConnectionErrors.LostConnection)));
    }

    [Fact]
    public void Declared_AnswerWithNothing_WritesNoRows()
    {
        var log = new RecordingLog();

        SqlServerEvents.Declared(
            log,
            "GetUser",
            new SqlServerDeclaredRequest("SELECT @p AS [1]", "@p int"),
            new SqlServerDeclaredAnswer(null, null, null),
            0
        );

        var members = log.Events.ShouldHaveSingleItem().Members;
        NamesOf(members).ShouldBe(["query", "sql", "params", "rows", "durationMs"]);
        Member(members, "rows").ShouldBe(LogValue.ListOf([]));
    }

    [Fact]
    public void Columns_RowsOfTheServer_WritesTheSqlTheDeclarationsAndEveryMemberOfEachRow()
    {
        var log = new RecordingLog();

        SqlServerEvents.Columns(
            log,
            "GetUser",
            new SqlServerColumnsRequest("SELECT id FROM dbo.users WHERE id = @id", "@id int"),
            new SqlServerColumnsAnswer([IdColumn, Rowstat], null, null),
            6
        );

        var (name, members) = log.Events.ShouldHaveSingleItem();
        name.ShouldBe("columns");
        NamesOf(members).ShouldBe(["query", "sql", "params", "rows", "durationMs"]);
        Member(members, "query").ShouldBe((LogValue)"GetUser");
        Member(members, "sql").ShouldBe((LogValue)"SELECT id FROM dbo.users WHERE id = @id");
        Member(members, "params").ShouldBe((LogValue)"@id int");
        Member(members, "rows")
            .ShouldBe(
                LogValue.ListOf([
                    LogValue.MapOf(
                        new("isHidden", false),
                        new("columnOrdinal", 1L),
                        new("name", "id"),
                        new("isNullable", false),
                        new("systemTypeName", "int"),
                        new("maxLength", 4L),
                        new("precision", 10L),
                        new("scale", 0L),
                        new("userTypeSchema", LogValue.Null),
                        new("userTypeName", LogValue.Null),
                        new("assemblyQualifiedTypeName", LogValue.Null),
                        new("sourceServer", LogValue.Null),
                        new("sourceDatabase", SqlServerScript.Database),
                        new("sourceSchema", "dbo"),
                        new("sourceTable", "users"),
                        new("sourceColumn", "id"),
                        new("isIdentityColumn", true),
                        new("isComputedColumn", false)
                    ),
                    ColumnRowMap(Rowstat),
                ])
            );
        Member(members, "durationMs").ShouldBe((LogValue)6L);
    }

    [Fact]
    public void Columns_ErrorRowsOfTheDescribe_WritesThemInThePlaceOfTheRows()
    {
        var log = new RecordingLog();

        SqlServerEvents.Columns(
            log,
            "GetUser",
            new SqlServerColumnsRequest("EXEC (@s)", "@s nvarchar(100)"),
            new SqlServerColumnsAnswer(
                null,
                [
                    new SqlServerDescribeError(11514, 4, "The metadata could not be determined."),
                    new SqlServerDescribeError(11501, 4, "The batch could not be analyzed."),
                ],
                null
            ),
            0
        );

        var members = log.Events.ShouldHaveSingleItem().Members;
        NamesOf(members).ShouldBe(["query", "sql", "params", "errors", "durationMs"]);
        Member(members, "errors")
            .ShouldBe(
                LogValue.ListOf([
                    LogValue.MapOf(
                        new("number", 11514L),
                        new("type", 4L),
                        new("message", "The metadata could not be determined.")
                    ),
                    LogValue.MapOf(
                        new("number", 11501L),
                        new("type", 4L),
                        new("message", "The batch could not be analyzed.")
                    ),
                ])
            );
    }

    [Fact]
    public void Columns_ErrorOfTheServer_WritesItInThePlaceOfTheRows()
    {
        var log = new RecordingLog();

        SqlServerEvents.Columns(
            log,
            "GetUser",
            new SqlServerColumnsRequest("SELECT 1 AS one", null),
            new SqlServerColumnsAnswer(null, null, Lost),
            0
        );

        var members = log.Events.ShouldHaveSingleItem().Members;
        NamesOf(members).ShouldBe(["query", "sql", "params", "error", "durationMs"]);
        Member(members, "params").ShouldBe(LogValue.Null);
        Member(members, "error").ShouldBe(ErrorsOf((0, 0, 0, ConnectionErrors.LostConnection)));
    }

    [Fact]
    public void Table_RowsOfTheServer_WritesTheSchemaTheTableAndEveryMemberOfEachRow()
    {
        var log = new RecordingLog();

        SqlServerEvents.Table(
            log,
            "GetUser",
            new SqlServerTableRequest("dbo", "users"),
            new SqlServerColumnsAnswer([IdColumn, Rowstat], null, null),
            3
        );

        var (name, members) = log.Events.ShouldHaveSingleItem();
        name.ShouldBe("table");
        NamesOf(members).ShouldBe(["query", "schema", "table", "rows", "durationMs"]);
        Member(members, "query").ShouldBe((LogValue)"GetUser");
        Member(members, "schema").ShouldBe((LogValue)"dbo");
        Member(members, "table").ShouldBe((LogValue)"users");
        Member(members, "rows").ShouldBe(LogValue.ListOf([ColumnRowMap(IdColumn), ColumnRowMap(Rowstat)]));
        Member(members, "durationMs").ShouldBe((LogValue)3L);
    }

    [Fact]
    public void Table_ErrorOfTheServer_WritesItInThePlaceOfTheRows()
    {
        var log = new RecordingLog();

        SqlServerEvents.Table(
            log,
            "GetUser",
            new SqlServerTableRequest("dbo", "users"),
            new SqlServerColumnsAnswer(null, null, Lost),
            0
        );

        var members = log.Events.ShouldHaveSingleItem().Members;
        NamesOf(members).ShouldBe(["query", "schema", "table", "error", "durationMs"]);
        Member(members, "error").ShouldBe(ErrorsOf((0, 0, 0, ConnectionErrors.LostConnection)));
    }

    [Fact]
    public void Column_ColumnWithAnOrigin_WritesEveryMemberAndNoReason()
    {
        var log = new RecordingLog();

        SqlServerEvents.Column(
            log,
            "GetUser",
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
            null
        );

        var (name, members) = log.Events.ShouldHaveSingleItem();
        name.ShouldBe("column");
        NamesOf(members)
            .ShouldBe([
                "query",
                "ordinal",
                "name",
                "type",
                "origin",
                "noOrigin",
                "nullable",
                "nullableSource",
                "identity",
                "computed",
            ]);
        Member(members, "query").ShouldBe((LogValue)"GetUser");
        Member(members, "ordinal").ShouldBe((LogValue)0L);
        Member(members, "name").ShouldBe((LogValue)"id");
        Member(members, "type").ShouldBe((LogValue)"int");
        Member(members, "origin")
            .ShouldBe(LogValue.MapOf(new("schema", "dbo"), new("table", "users"), new("column", "id")));
        Member(members, "noOrigin").ShouldBe(LogValue.Null);
        Member(members, "nullable").ShouldBe((LogValue)false);
        Member(members, "nullableSource").ShouldBe((LogValue)"server");
        Member(members, "identity").ShouldBe((LogValue)true);
        Member(members, "computed").ShouldBe((LogValue)false);
    }

    // An expression has no origin, and the server says nothing of identity or computed for it.
    [Fact]
    public void Column_ColumnWithNoOrigin_WritesTheReasonAndNulls()
    {
        var log = new RecordingLog();

        SqlServerEvents.Column(
            log,
            "GetUser",
            new SidecarColumn(1, "n", Int, true, SidecarValues.NullableSource.Server, null, null, null),
            SqlServerColumns.Expression
        );

        var members = log.Events.ShouldHaveSingleItem().Members;
        Member(members, "origin").ShouldBe(LogValue.Null);
        Member(members, "noOrigin").ShouldBe((LogValue)SqlServerColumns.Expression);
        Member(members, "nullable").ShouldBe((LogValue)true);
        Member(members, "identity").ShouldBe(LogValue.Null);
        Member(members, "computed").ShouldBe(LogValue.Null);
    }

    [Fact]
    public void TableMatch_Matched_WritesTheResultAndTheTable()
    {
        var log = new RecordingLog();

        SqlServerEvents.TableMatch(log, "GetUser", new SidecarTable("dbo", "users"), SidecarValues.TableMatch.Matched);

        var (name, members) = log.Events.ShouldHaveSingleItem();
        name.ShouldBe("table-match");
        NamesOf(members).ShouldBe(["query", "tableMatch", "table"]);
        Member(members, "query").ShouldBe((LogValue)"GetUser");
        Member(members, "tableMatch").ShouldBe((LogValue)"matched");
        Member(members, "table").ShouldBe(LogValue.MapOf(new("schema", "dbo"), new("table", "users")));
    }

    [Fact]
    public void TableMatch_CheckThatFailed_WritesNoTable()
    {
        var log = new RecordingLog();

        SqlServerEvents.TableMatch(log, "GetUser", null, SidecarValues.TableMatch.SeveralTables);

        var members = log.Events.ShouldHaveSingleItem().Members;
        Member(members, "tableMatch").ShouldBe((LogValue)"several-tables");
        Member(members, "table").ShouldBe(LogValue.Null);
    }

    // A describe of a statement with rows, one of whose columns has no origin: parameters, columns, a column for each
    // and the match, with no table, each naming its query.
    [Fact]
    public async Task DescribeAsync_Rows_WritesColumnsEachColumnAndTheMatchInOrder()
    {
        const string Sql = "SELECT id, 1 + id AS next FROM dbo.users";
        var next = IdColumn with
        {
            ColumnOrdinal = 2,
            Name = "next",
            IsNullable = true,
            SourceDatabase = null,
            SourceSchema = null,
            SourceTable = null,
            SourceColumn = null,
            IsIdentityColumn = null,
            IsComputedColumn = null,
        };
        var script = new SqlServerScript()
            .Add("parameters", new SqlServerParametersRequest(Sql, null), new SqlServerParametersAnswer([], null))
            .Add(
                "columns",
                new SqlServerColumnsRequest(Sql, null),
                new SqlServerColumnsAnswer([IdColumn, next, Rowstat with { ColumnOrdinal = 3 }], null, null)
            );
        var log = new RecordingLog();

        _ = await script.DescribeAsync(Of(SqlDialectRules.SqlServer, Sql), log, new TestClock(), CallTime);

        log.Events.Select(written => written.Name)
            .ShouldBe(["parameters", "columns", "column", "column", "table-match"]);
        foreach (var (_, members) in log.Events)
        {
            Member(members, "query").ShouldBe((LogValue)Query);
        }

        Member(log.Events[1].Members, "durationMs").ShouldBe((LogValue)(long)CallTime.TotalMilliseconds);
        Member(log.Events[2].Members, "name").ShouldBe((LogValue)"id");
        Member(log.Events[3].Members, "name").ShouldBe((LogValue)"next");
        Member(log.Events[3].Members, "noOrigin").ShouldBe((LogValue)SqlServerColumns.Expression);
        Member(log.Events[4].Members, "tableMatch").ShouldBe((LogValue)"no-origin");
    }

    // When every column comes from one table, "table" is written between the columns and the match.
    [Fact]
    public async Task DescribeAsync_OneTable_WritesTableBeforeTheMatch()
    {
        const string Sql = "SELECT id FROM dbo.users";
        var script = new SqlServerScript()
            .Add("parameters", new SqlServerParametersRequest(Sql, null), new SqlServerParametersAnswer([], null))
            .Add(
                "columns",
                new SqlServerColumnsRequest(Sql, null),
                new SqlServerColumnsAnswer([IdColumn, Rowstat], null, null)
            )
            .Add(
                "table",
                new SqlServerTableRequest("dbo", "users"),
                new SqlServerColumnsAnswer([IdColumn, Rowstat], null, null)
            );
        var log = new RecordingLog();

        _ = await script.DescribeAsync(Of(SqlDialectRules.SqlServer, Sql), log, new TestClock(), CallTime);

        log.Events.Select(written => written.Name)
            .ShouldBe(["parameters", "columns", "column", "table", "table-match"]);
        Member(log.Events[3].Members, "durationMs").ShouldBe((LogValue)(long)CallTime.TotalMilliseconds);
        Member(log.Events[4].Members, "tableMatch").ShouldBe((LogValue)"matched");
    }

    // A describe of a declared parameter and a reused one: declared, then parameters, then the probes of each copy,
    // then columns, each naming its query and taking the time of its call, and no event for open.
    [Fact]
    public async Task DescribeAsync_DeclaredAndReusedParameters_WritesDeclaredParametersProbesAndColumnsInOrder()
    {
        const string Sql = "SELECT id FROM dbo.users WHERE tenant = @tenant AND (@name IS NULL OR name = @name)";
        const string Renamed =
            "SELECT id FROM dbo.users WHERE tenant = @tenant AND (@name_1 IS NULL OR name = @name_2)";
        var parameters = new SqlServerParametersRequest(Renamed, "@tenant int");
        var intRow = new SqlServerParameterRow("@name_1", "int", 4, 10, 0, null, null, null);
        var textRow = new SqlServerParameterRow("@name_2", "nvarchar(100)", 200, 0, 0, null, null, null);
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
            .Add("parameters", parameters, new SqlServerParametersAnswer([intRow, textRow], null))
            .Add(
                "probe",
                new SqlServerProbeRequest(Renamed, "@tenant int, @name_1 xml, @name_2 nvarchar(100)"),
                new SqlServerProbeAnswer(true, null)
            )
            .Add(
                "probe",
                new SqlServerProbeRequest(Renamed, "@tenant int, @name_1 int, @name_2 nvarchar(100)"),
                new SqlServerProbeAnswer(true, null)
            )
            .Add(
                "probe",
                new SqlServerProbeRequest(Renamed, "@tenant int, @name_1 int, @name_2 xml"),
                new SqlServerProbeAnswer(false, null)
            )
            .Add(
                "columns",
                new SqlServerColumnsRequest(Sql, "@tenant int, @name nvarchar(100)"),
                new SqlServerColumnsAnswer([], null, null)
            );
        var log = new RecordingLog();

        _ = await script.DescribeAsync(
            Of(SqlDialectRules.SqlServer, Sql, ("tenant", "int")),
            log,
            new TestClock(),
            CallTime
        );

        log.Events.Select(written => written.Name)
            .ShouldBe(["declared", "parameters", "probe", "probe", "probe", "columns"]);
        foreach (var (_, members) in log.Events)
        {
            Member(members, "query").ShouldBe((LogValue)Query);
            Member(members, "durationMs").ShouldBe((LogValue)(long)CallTime.TotalMilliseconds);
        }

        Member(log.Events[0].Members, "params").ShouldBe((LogValue)"@tenant int");
        Member(log.Events[1].Members, "sql").ShouldBe((LogValue)Renamed);
        Member(log.Events[2].Members, "params").ShouldBe((LogValue)"@tenant int, @name_1 xml, @name_2 nvarchar(100)");
        Member(log.Events[2].Members, "binds").ShouldBe((LogValue)true);
        Member(log.Events[4].Members, "binds").ShouldBe((LogValue)false);
        Member(log.Events[5].Members, "sql").ShouldBe((LogValue)Sql);
        Member(log.Events[5].Members, "params").ShouldBe((LogValue)"@tenant int, @name nvarchar(100)");
    }

    // A call that fails is written, with its error, and nothing is asked after it.
    [Fact]
    public async Task DescribeAsync_ParametersThatFail_WritesTheErrorAndNoProbe()
    {
        const string Sql = "SELECT id FROM dbo.users WHERE id = @id";
        var script = new SqlServerScript().Add(
            "parameters",
            new SqlServerParametersRequest(Sql, null),
            new SqlServerParametersAnswer(null, Refused)
        );
        var log = new RecordingLog();

        _ = await script.DescribeAsync(Of(SqlDialectRules.SqlServer, Sql), log, new TestClock(), CallTime);

        var (name, members) = log.Events.ShouldHaveSingleItem();
        name.ShouldBe("parameters");
        NamesOf(members).ShouldBe(["query", "sql", "params", "error", "durationMs"]);
    }

    // As a run gives the describer its log: the engine before each name, and the connection's value written as ***
    // wherever it stands: in the SQL, in the declarations and in a string of a row.
    [Fact]
    public async Task DescribeAsync_ThroughTheLogOfASession_HasTheEngineBeforeEachNameAndNothingOfTheSecret()
    {
        const string Sql = "SELECT id FROM dbo.users WHERE email = @p AND other = @e";
        var script = new SqlServerScript()
            .Add(
                "declared",
                new SqlServerDeclaredRequest("SELECT @p AS [1]", "@p dbo.Email"),
                new SqlServerDeclaredAnswer(
                    [new SqlServerDeclaredRow(1, "nvarchar(320)", 640, 0, 0, "dbo", "Email", null)],
                    null,
                    null
                )
            )
            .Add(
                "parameters",
                new SqlServerParametersRequest(Sql, "@p dbo.Email"),
                new SqlServerParametersAnswer(
                    [new SqlServerParameterRow("@e", "nvarchar(320)", 640, 0, 0, "dbo", "Email", null)],
                    null
                )
            )
            .Add(
                "columns",
                new SqlServerColumnsRequest(Sql, "@p dbo.Email, @e dbo.Email"),
                new SqlServerColumnsAnswer([], null, null)
            );
        var inner = new RecordingLog();

        var (described, _) = await script.DescribeAsync(
            Of(SqlDialectRules.SqlServer, Sql, ("p", "dbo.Email")),
            new SessionRunLog(inner, new ConnectionSecret("Email"), "mssql.")
        );

        _ = described.Value.ShouldNotBeNull();
        inner.Events.Select(written => written.Name).ShouldBe(["mssql.declared", "mssql.parameters", "mssql.columns"]);
        var declared = inner.Events[0].Members;
        Member(declared, "params").ShouldBe((LogValue)"@p dbo.***");
        var parameters = inner.Events[1].Members;
        Member(parameters, "params").ShouldBe((LogValue)"@p dbo.***");
        Member(parameters, "rows")
            .ShouldBe(LogValue.ListOf([ParameterMap("@e", "nvarchar(320)", 640, 0, 0, "dbo", "***")]));
        Member(inner.Events[2].Members, "params").ShouldBe((LogValue)"@p dbo.***, @e dbo.***");
    }

    // The same inside a list of maps: the message of an error is a string two levels down, and the SQL holds it too.
    [Fact]
    public async Task DescribeAsync_ThroughTheLogOfASession_RemovesTheSecretFromTheSqlAndTheMessageOfAnError()
    {
        const string Sql = "SELECT id FROM dbo.secret WHERE id = @id";
        var script = new SqlServerScript().Add(
            "parameters",
            new SqlServerParametersRequest(Sql, null),
            new SqlServerParametersAnswer(
                null,
                new SqlServerServerError([new SqlServerErrorRow(208, 16, 1, "Invalid object name 'dbo.secret'.")])
            )
        );
        var inner = new RecordingLog();

        _ = await script.DescribeAsync(
            Of(SqlDialectRules.SqlServer, Sql),
            new SessionRunLog(inner, new ConnectionSecret("secret"), "mssql.")
        );

        var (name, members) = inner.Events.ShouldHaveSingleItem();
        name.ShouldBe("mssql.parameters");
        Member(members, "sql").ShouldBe((LogValue)"SELECT id FROM dbo.*** WHERE id = @id");
        Member(members, "error").ShouldBe(ErrorsOf((208, 16, 1, "Invalid object name 'dbo.***'.")));
    }
}
