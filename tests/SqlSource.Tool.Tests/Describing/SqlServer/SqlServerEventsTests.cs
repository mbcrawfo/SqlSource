using System;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Parsing;
using SqlSource.Snapshot;
using SqlSource.Tool.Describing;
using SqlSource.Tool.Describing.Capture;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Describing.SqlServer;
using SqlSource.Tool.Logging;
using SqlSource.Tool.Testing;
using SqlSource.Tool.Testing.Fixtures;
using Xunit;
using static SqlSource.Tool.Testing.DescribeRequests;

namespace SqlSource.Tool.Tests.Describing.SqlServer;

// What the describer says in the run's log of parameters, probe and declared: each event's members for an answer with
// rows and for an error, and, through a session, the order the events are written in, the query each names, and how
// long each call took.
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
                    LogValue.MapOf(
                        new("name", "@id"),
                        new("suggestedSystemTypeName", "int"),
                        new("suggestedMaxLength", 4L),
                        new("suggestedPrecision", 10L),
                        new("suggestedScale", 0L),
                        new("suggestedUserTypeSchema", LogValue.Null),
                        new("suggestedUserTypeName", LogValue.Null),
                        new("suggestedAssemblyQualifiedTypeName", LogValue.Null)
                    ),
                    LogValue.MapOf(
                        new("name", "@e"),
                        new("suggestedSystemTypeName", "nvarchar(320)"),
                        new("suggestedMaxLength", 640L),
                        new("suggestedPrecision", 0L),
                        new("suggestedScale", 0L),
                        new("suggestedUserTypeSchema", "dbo"),
                        new("suggestedUserTypeName", "Email"),
                        new("suggestedAssemblyQualifiedTypeName", "Types, Version=1.0")
                    ),
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
        Member(members, "rows")
            .ShouldBe(
                LogValue.ListOf([
                    LogValue.MapOf(
                        new("name", "@x"),
                        new("suggestedSystemTypeName", LogValue.Null),
                        new("suggestedMaxLength", LogValue.Null),
                        new("suggestedPrecision", LogValue.Null),
                        new("suggestedScale", LogValue.Null),
                        new("suggestedUserTypeSchema", LogValue.Null),
                        new("suggestedUserTypeName", LogValue.Null),
                        new("suggestedAssemblyQualifiedTypeName", LogValue.Null)
                    ),
                ])
            );
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

    // A describe of a declared parameter and a reused one: declared, then parameters, then the probes of each copy,
    // each naming its query and taking the time of its call, and no event for open.
    [Fact]
    public async Task DescribeAsync_DeclaredAndReusedParameters_WritesDeclaredParametersAndProbesInOrder()
    {
        const string Renamed =
            "SELECT id FROM dbo.users WHERE tenant = @tenant AND (@name_1 IS NULL OR name = @name_2)";
        var parameters = new SqlServerParametersRequest(Renamed, "@tenant int");
        var intRow = new SqlServerParameterRow("@name_1", "int", 4, 10, 0, null, null, null);
        var textRow = new SqlServerParameterRow("@name_2", "nvarchar(100)", 200, 0, 0, null, null, null);
        var script = new EventScript()
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
            );
        var log = new RecordingLog();

        await script.DescribeAsync(
            Of(
                SqlDialectRules.SqlServer,
                "SELECT id FROM dbo.users WHERE tenant = @tenant AND (@name IS NULL OR name = @name)",
                ("tenant", "int")
            ),
            log
        );

        log.Events.Select(written => written.Name).ShouldBe(["declared", "parameters", "probe", "probe", "probe"]);
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
    }

    // A call that fails is written, with its error, and nothing is asked after it.
    [Fact]
    public async Task DescribeAsync_ParametersThatFail_WritesTheErrorAndNoProbe()
    {
        const string Sql = "SELECT id FROM dbo.users WHERE id = @id";
        var script = new EventScript().Add(
            "parameters",
            new SqlServerParametersRequest(Sql, null),
            new SqlServerParametersAnswer(null, Refused)
        );
        var log = new RecordingLog();

        await script.DescribeAsync(Of(SqlDialectRules.SqlServer, Sql), log);

        var (name, members) = log.Events.ShouldHaveSingleItem();
        name.ShouldBe("parameters");
        NamesOf(members).ShouldBe(["query", "sql", "params", "error", "durationMs"]);
    }

    // As a run gives the describer its log: the engine before each name, and the connection's value written as ***
    // wherever it stands, in the SQL, the declarations and the message of an error one level down.
    [Fact]
    public async Task DescribeAsync_ThroughTheLogOfASession_HasTheEngineBeforeEachNameAndNothingOfTheSecret()
    {
        const string Sql = "SELECT id FROM dbo.secret WHERE id = @id";
        var script = new EventScript().Add(
            "parameters",
            new SqlServerParametersRequest(Sql, null),
            new SqlServerParametersAnswer(
                null,
                new SqlServerServerError([new SqlServerErrorRow(208, 16, 1, "Invalid object name 'dbo.secret'.")])
            )
        );
        var inner = new RecordingLog();

        await script.DescribeAsync(
            Of(SqlDialectRules.SqlServer, Sql),
            new SessionRunLog(inner, new ConnectionSecret("secret"), "mssql.")
        );

        var (name, members) = inner.Events.ShouldHaveSingleItem();
        name.ShouldBe("mssql.parameters");
        Member(members, "sql").ShouldBe((LogValue)"SELECT id FROM dbo.*** WHERE id = @id");
        Member(members, "error").ShouldBe(ErrorsOf((208, 16, 1, "Invalid object name 'dbo.***'.")));
    }

    // A capture of "open" and of the calls a test adds, replayed with a clock that every call moves by CallTime.
    private sealed class EventScript
    {
        private readonly DescribeCapture _capture = new(SidecarValues.Engine.SqlServer);

        public EventScript()
        {
            _ = Add(
                "open",
                SqlServerOpenRequest.Instance,
                new SqlServerOpenAnswer(
                    "14.0.3560.2",
                    3,
                    "7.1.1",
                    [new SqlServerSetting("compatibility_level", "140")],
                    null
                )
            );
        }

        public EventScript Add<TRequest, TAnswer>(string method, TRequest request, TAnswer answer)
        {
            _capture.Add(new CaptureCall(method, CaptureJson.ToElement(request), CaptureJson.ToElement(answer)));
            return this;
        }

        public async Task DescribeAsync(DescribeRequest request, IRunLog log)
        {
            var clock = new TestClock();
            var replay = new ReplayingExchange(_capture);
            var opened = await new SqlServerDescriber().OpenAsync(
                "app",
                FixtureReplay.Connection,
                new ClockedExchange(replay, clock, CallTime),
                log,
                clock
            );
            await using var session = opened.Value.ShouldNotBeNull();
            _ = await session.DescribeAsync(request, TestContext.Current.CancellationToken);
            replay.Unused.ShouldBeEmpty();
        }
    }
}
