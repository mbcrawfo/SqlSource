using System;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Parsing;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Describing.Postgres;
using SqlSource.Tool.Testing;
using Xunit;

namespace SqlSource.Tool.Tests.Describing.Postgres;

// The describer of PostgreSQL where no server is needed: what "open" answered becomes the session's server or the
// failure, and a connection string the driver cannot read is a failure too.
public sealed class PostgresDescriberTests
{
    [Fact]
    public void Dialect_OfTheDescriber_IsPostgreSql() =>
        new PostgresDescriber().Dialect.ShouldBe(SqlDialect.PostgreSql);

    [Fact]
    public void SecretsOf_ConnectionWithBothPasswords_IsBoth() =>
        new PostgresDescriber()
            .SecretsOf("Host=h;Password=pw;SSL Password=kp")
            .ShouldBe(["pw", "kp"], ignoreOrder: true);

    // A string the driver cannot read has no parts it knows, and opening it fails in its own place.
    [Fact]
    public void SecretsOf_ConnectionTheDriverCannotRead_IsNone() =>
        new PostgresDescriber().SecretsOf("Nonsense=1").ShouldBeEmpty();

    // A keyword the builder knows with a value it cannot read: the builder throws, whatever else the string holds.
    [Fact]
    public void SecretsOf_ConnectionWithAValueTheDriverCannotRead_IsNone() =>
        new PostgresDescriber().SecretsOf("Password=pw;Port=abc").ShouldBeEmpty();

    // The list is closed: nothing else reaches a SET LOCAL, and a describer that would send it is never made.
    [Theory]
    [InlineData("work_mem=1")]
    [InlineData("enable_hashjoin=off; DROP TABLE users")]
    public void Constructor_SettingOutsideTheList_Throws(string setting) =>
        Should.Throw<ArgumentException>(() => new PostgresDescriber(["enable_nestloop=off", setting]));

    // The connection of a script is the word "replay", which no driver could open: nothing of Npgsql runs.
    [Fact]
    public async Task OpenAsync_ReplayedOpen_GivesTheServerOfTheAnswerAndTouchesNoConnection()
    {
        var exchange = new PostgresScript().Open().Play();

        await using var session = await PostgresScript.SessionAsync(exchange);

        session.Server.ShouldBe(
            new ServerInfo(
                "18.4",
                "Npgsql",
                "10.0.3",
                new EquatableArray<ServerSetting>([
                    new ServerSetting("search_path", "\"$user\", public"),
                    new ServerSetting("plan_cache_mode", "auto"),
                    new ServerSetting("standard_conforming_strings", "on"),
                ])
            )
        );
        exchange.Unused.ShouldBeEmpty();
    }

    [Fact]
    public async Task OpenAsync_ReplayedError_IsSqlsrc224ForTheDatabase()
    {
        var error = new PostgresServerError("3D000", ConnectionErrors.NoSuchDatabase, null, null, null);
        var exchange = new PostgresScript()
            .Answer("open", PostgresOpenRequest.Instance, new PostgresOpenAnswer(null, null, null, error))
            .Play();

        var opened = await PostgresScript.OpenAsync(exchange);

        opened.Value.ShouldBeNull();
        var failure = opened.Failure.ShouldNotBeNull();
        failure.Descriptor.Id.ShouldBe("SQLSRC224");
        failure.Arguments.ShouldBe([PostgresScript.Database]);
        failure.ServerLines.ShouldBe(["3D000: the server has no such database"]);
    }

    // A user causes each of these, so none is an exception, and the driver's own text, whose inner exception
    // quotes the value, is not passed on.
    [Theory]
    [InlineData("Host=127.0.0.1;Port=" + Secrets.Marker)]
    [InlineData("Host=127.0.0.1;Bogus=" + Secrets.Marker)]
    [InlineData(Secrets.Marker)]
    [InlineData("Password=" + Secrets.Marker)]
    public async Task OpenAsync_ConnectionStringNpgsqlCannotRead_IsSqlsrc224WithoutTheDriversText(string connection)
    {
        var opened = await PostgresScript.OpenAsync(LiveExchange.Instance, connection: connection);

        var failure = opened.Failure.ShouldNotBeNull();
        failure.Descriptor.Id.ShouldBe("SQLSRC224");
        failure.ServerLines.ShouldBe([PostgresConnection.UnreadableConnectionString]);
        failure
            .Arguments.Concat(failure.ServerLines)
            .ShouldAllBe(text => !text.Contains(Secrets.Marker, StringComparison.Ordinal));
    }
}
