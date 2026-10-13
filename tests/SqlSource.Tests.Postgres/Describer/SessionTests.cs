using System;
using System.Threading.Tasks;
using Npgsql;
using Shouldly;
using SqlSource.Tool.Describing.Capture;
using SqlSource.Tool.Describing.Describers;
using Xunit;

namespace SqlSource.Tests.Postgres.Describer;

// "open" against a real server: what a session knows of it, and each way a session is not opened.
[Collection(UsesPostgres.Name)]
public sealed class SessionTests(PostgresContainer postgres)
{
    private const string Marker = "s3cret-marker";

    [Fact]
    public async Task OpenAsync_Container_GivesTheVersionTheDriverAndTheThreeSettings()
    {
        var opened = await Sessions.OpenAsync(postgres.ConnectionString);

        await using var session = opened.Value.ShouldNotBeNull();
        var server = session.Server;
        server.Version.ShouldNotContain(" ");
        char.IsAsciiDigit(server.Version[0]).ShouldBeTrue(server.Version);
        server.Driver.ShouldBe("Npgsql");
        server.DriverVersion.ShouldBe(typeof(NpgsqlConnection).Assembly.GetName().Version!.ToString(3));
        server.Settings.ShouldBe([
            new ServerSetting("search_path", "\"$user\", public"),
            new ServerSetting("plan_cache_mode", "auto"),
            new ServerSetting("standard_conforming_strings", "on"),
        ]);
    }

    [Fact]
    public async Task OpenAsync_ThroughACapturingExchange_KeepsAnEmptyRequestAndNothingOfTheConnection()
    {
        var capture = new DescribeCapture("postgres");

        var opened = await Sessions.OpenAsync(
            postgres.ConnectionString,
            new CapturingExchange(LiveExchange.Instance, capture)
        );

        await using var session = opened.Value.ShouldNotBeNull();
        var call = capture.Calls.ShouldHaveSingleItem();
        call.Method.ShouldBe("open");
        CaptureJson.TextOf(call.Request).ShouldBe("{}");
        CaptureJson.Write(capture).ShouldNotContain(postgres.ConnectionString);
    }

    [Fact]
    public async Task OpenAsync_WrongPassword_IsSqlsrc224WithTheServersMessage()
    {
        var connection = new NpgsqlConnectionStringBuilder(postgres.ConnectionString) { Password = Marker };

        var failure = await ShouldNotOpenAsync(connection.ConnectionString);

        failure.ServerLines[0].ShouldStartWith("28P01: password authentication failed");
    }

    [Fact]
    public async Task OpenAsync_MissingDatabase_IsSqlsrc224WithTheServersMessage()
    {
        var connection = new NpgsqlConnectionStringBuilder(postgres.ConnectionString) { Database = "nope" };

        var failure = await ShouldNotOpenAsync(connection.ConnectionString);

        failure.ServerLines.ShouldBe(["3D000: database \"nope\" does not exist"]);
    }

    // The port 1 of this machine refuses at once.
    [Fact]
    public async Task OpenAsync_UnreachableHost_IsSqlsrc224WithTheDriversMessage()
    {
        var failure = await ShouldNotOpenAsync("Host=127.0.0.1;Port=1;Timeout=5;Username=app;Password=" + Marker);

        failure.ServerLines.ShouldBe(["Failed to connect to 127.0.0.1:1"]);
    }

    // No session, SQLSRC224 at no step for the database, and the connection's whole value in no text of it.
    private static async Task<DescribeFailure> ShouldNotOpenAsync(string connection)
    {
        var opened = await Sessions.OpenAsync(connection);

        opened.Value.ShouldBeNull();
        var failure = opened.Failure.ShouldNotBeNull();
        failure.Descriptor.Id.ShouldBe("SQLSRC224");
        failure.Arguments.ShouldBe([PostgresContainer.Database]);
        failure.Step.ShouldBeNull();
        Sessions
            .TextsOf(failure)
            .ShouldAllBe(text =>
                !text.Contains(connection, StringComparison.Ordinal) && !text.Contains(Marker, StringComparison.Ordinal)
            );
        return failure;
    }
}
