using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Describing.SqlServer;
using SqlSource.Tool.Describing.SqlServer.Columns;
using SqlSource.Tool.Logging;
using SqlSource.Tool.Testing;
using Xunit;
using static SqlSource.Tool.Testing.RecordingLog;
using static SqlSource.Tool.Tests.SqlServerEventCases;
using static SqlSource.Tool.Tests.SqlServerScript;

namespace SqlSource.Tool.Tests.Describing.SqlServer;

// What the describer says in the run's log of columns and of each column: each event's members for an answer with
// rows and for an error, and for a column with and without an origin.
public sealed class SqlServerEventsColumnTests
{
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
                        new("sourceDatabase", Database),
                        new("sourceSchema", "dbo"),
                        new("sourceTable", "users"),
                        new("sourceColumn", "id"),
                        new("isIdentityColumn", true),
                        new("isComputedColumn", false),
                        new("sourceType", "U"),
                        new("persistedNotNull", false)
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
        Member(members, "error").ShouldBe(ErrorsOf((0, 0, 0, ConnectionTexts.LostConnection)));
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
}
