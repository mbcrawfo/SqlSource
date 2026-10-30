using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Describing.SqlServer;
using SqlSource.Tool.Logging;
using SqlSource.Tool.Testing;
using Xunit;
using static SqlSource.Tool.Testing.RecordingLog;
using static SqlSource.Tool.Tests.SqlServerEventCases;
using static SqlSource.Tool.Tests.SqlServerScript;

namespace SqlSource.Tool.Tests.Describing.SqlServer;

// What the describer says in the run's log of table and of the table match: each event's members for an answer with
// rows and for an error, and the result of the match.
public sealed class SqlServerEventsTableTests
{
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
        Member(members, "error").ShouldBe(ErrorsOf((0, 0, 0, ConnectionTexts.LostConnection)));
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
}
