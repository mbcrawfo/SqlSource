using Shouldly;
using SqlSource.Tool.Describing.SqlServer;
using SqlSource.Tool.Logging;
using SqlSource.Tool.Testing;
using Xunit;
using static SqlSource.Tool.Testing.RecordingLog;
using static SqlSource.Tool.Tests.SqlServerEventCases;
using static SqlSource.Tool.Tests.SqlServerScript;

namespace SqlSource.Tool.Tests.Describing.SqlServer;

// What the describer says in the run's log of parameters, probe and declared: each event's members for an answer with
// rows and for an error.
public sealed class SqlServerEventsTests
{
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

    // An answer with neither rows nor an error is a bug of the describer: the event holds no rows.
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
}
