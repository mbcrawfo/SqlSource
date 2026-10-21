using System.Globalization;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Tool.Describing.Capture;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Describing.SqlServer;
using SqlSource.Tool.Testing;
using SqlSource.Tool.Testing.Fixtures;
using Xunit;

namespace SqlSource.Tool.Tests.Describing.SqlServer;

// The session over a capture the test writes, which holds the one answer of "open": what the session makes of the
// server's version, its edition and its settings, and of an error.  No driver runs: the connection is a word that
// SqlClient could not open.
public sealed class SqlServerSessionTests
{
    private const string Database = "app";

    // What "open" answers for SQL Server 2017 with the settings a database of its defaults has.
    private static readonly SqlServerOpenAnswer Opened2017 = new(
        "14.0.3560.2",
        3,
        "7.1.1",
        [
            new SqlServerSetting("compatibility_level", "140"),
            new SqlServerSetting("ANSI_NULLS", "ON"),
            new SqlServerSetting("QUOTED_IDENTIFIER", "ON"),
            new SqlServerSetting("collation", "SQL_Latin1_General_CP1_CI_AS"),
        ],
        null
    );

    [Fact]
    public async Task OpenAsync_Server2017_OpensWithTheVersionTheDriverAndTheFourSettings()
    {
        var exchange = Answering(Opened2017);

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
        var opened = await OpenAsync(Answering(Opened2017 with { ProductVersion = "13.0.6300.2" }));

        opened.Value.ShouldBeNull();
        var failure = opened.Failure.ShouldNotBeNull();
        failure.Descriptor.Id.ShouldBe("SQLSRC235");
        failure.Arguments.ShouldBe([Database, "SQL Server", "13.0.6300.2", "14 (SQL Server 2017)"]);
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
        var opened = await OpenAsync(Answering(Opened2017 with { ProductVersion = "12.0.2000.8", EngineEdition = 5 }));

        await using var session = opened.Value.ShouldNotBeNull();
        session.Server.Version.ShouldBe("12.0.2000.8");
    }

    [Fact]
    public async Task OpenAsync_AzureManagedInstance_Opens()
    {
        var opened = await OpenAsync(Answering(Opened2017 with { ProductVersion = "12.0.2000.8", EngineEdition = 8 }));

        await using var session = opened.Value.ShouldNotBeNull();
        session.Server.Version.ShouldBe("12.0.2000.8");
    }

    [Fact]
    public async Task OpenAsync_ErrorAnswered_IsSqlsrc224WithTheFixedLine()
    {
        var error = new SqlServerServerError([new SqlServerErrorRow(18456, 14, 8, "the server refused the login")]);

        var opened = await OpenAsync(Answering(new SqlServerOpenAnswer(null, null, null, null, error)));

        opened.Value.ShouldBeNull();
        var failure = opened.Failure.ShouldNotBeNull();
        failure.Descriptor.Id.ShouldBe("SQLSRC224");
        failure.Arguments.ShouldBe([Database]);
        failure.Step.ShouldBeNull();
        failure.ServerLines.ShouldBe(["Msg 18456, Level 14, State 8: the server refused the login"]);
        failure.Help.ShouldBe(
            "check that the server is running, and that the connection string names it, a login it accepts and the "
                + "database"
        );
    }

    // A capture of the one call "open", answered as the test gives.
    private static ReplayingExchange Answering(SqlServerOpenAnswer answer)
    {
        var capture = new DescribeCapture(SidecarValues.Engine.SqlServer);
        capture.Add(
            new CaptureCall("open", CaptureJson.ToElement(SqlServerOpenRequest.Instance), CaptureJson.ToElement(answer))
        );
        return new ReplayingExchange(capture);
    }

    private static Task<Outcome<IDescribeSession, DescribeFailure>> OpenAsync(IDescribeExchange exchange) =>
        new SqlServerDescriber().OpenAsync(Database, FixtureReplay.Connection, exchange);
}
