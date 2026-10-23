using System.Threading.Tasks;
using Shouldly;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Describing.SqlServer;
using SqlSource.Tool.Testing;
using SqlSource.Tool.Testing.Fixtures;
using Xunit;

namespace SqlSource.Tool.Tests.Describing.SqlServer;

// The session over a capture the test writes: what the session makes of the server's version, its edition and its
// settings, and of an error, at "open".  No driver runs: the connection is a word that SqlClient could not open.
public sealed class SqlServerSessionTests
{
    private static Task<Outcome<IDescribeSession, DescribeFailure>> OpenAsync(IDescribeExchange exchange) =>
        new SqlServerDescriber().OpenAsync(SqlServerScript.Database, FixtureReplay.Connection, exchange);

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
        Failures
            .MessageOf(failure)
            .ShouldBe(
                "The database 'app' runs SQL Server 13.0.6300.2, and the tool supports 14 (SQL Server 2017) and later"
            );
    }

    // The leading digits of a version decide, as for PostgreSQL: a suffix after them does not hide the major.
    [Fact]
    public async Task OpenAsync_ServerWithASuffixBelowTheFloor_IsSqlsrc235()
    {
        var opened = await OpenAsync(
            SqlServerScript.Answering(SqlServerScript.Opened with { ProductVersion = "13beta1" })
        );

        opened.Value.ShouldBeNull();
        var failure = opened.Failure.ShouldNotBeNull();
        failure.Descriptor.Id.ShouldBe("SQLSRC235");
        failure.Arguments.ShouldBe([SqlServerScript.Database, "SQL Server", "13beta1", "14 (SQL Server 2017)"]);
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
}
