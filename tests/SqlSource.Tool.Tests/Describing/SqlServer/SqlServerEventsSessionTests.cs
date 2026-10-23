using System;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Parsing;
using SqlSource.Tool.Describing;
using SqlSource.Tool.Describing.SqlServer;
using SqlSource.Tool.Logging;
using SqlSource.Tool.Testing;
using Xunit;
using static SqlSource.Tool.Testing.DescribeRequests;
using static SqlSource.Tool.Testing.RecordingLog;
using static SqlSource.Tool.Tests.SqlServerEventCases;
using static SqlSource.Tool.Tests.SqlServerScript;

namespace SqlSource.Tool.Tests.Describing.SqlServer;

// What the describer says in the run's log through a whole session: the order the events are written in, the query
// each names, how long each call took, and that nothing of the connection's secret is written.
public sealed class SqlServerEventsSessionTests
{
    private static readonly TimeSpan CallTime = TimeSpan.FromMilliseconds(7);

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
            SourceType = null,
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
    // wherever it stands: in the declarations and in a string of a row.
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
                new SqlServerColumnsRequest(Sql, "@p dbo.Email, @e [dbo].[Email]"),
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
        Member(inner.Events[2].Members, "params").ShouldBe((LogValue)"@p dbo.***, @e [dbo].[***]");
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
