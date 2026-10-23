using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Npgsql;
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
            .SecretsOf("Host=h;Password=pw;SSL Password=kp", static _ => null)
            .ShouldBe(["pw", "kp"], ignoreOrder: true);

    // A string the driver cannot read has no parts it knows, and opening it fails in its own place.
    [Fact]
    public void SecretsOf_ConnectionTheDriverCannotRead_IsNone() =>
        new PostgresDescriber().SecretsOf("Nonsense=1", static _ => null).ShouldBeEmpty();

    // A keyword the builder knows with a value it cannot read: the builder throws, whatever else the string holds.
    [Fact]
    public void SecretsOf_ConnectionWithAValueTheDriverCannotRead_IsNone() =>
        new PostgresDescriber().SecretsOf("Password=pw;Port=abc", static _ => null).ShouldBeEmpty();

    // Npgsql takes the password of the environment when the string has none.
    [Fact]
    public void SecretsOf_NoPasswordAndPgPasswordSet_IsPgPassword() =>
        new PostgresDescriber().SecretsOf("Host=h", Environment(("PGPASSWORD", "envpass1"))).ShouldBe(["envpass1"]);

    [Fact]
    public void SecretsOf_PasswordInTheStringAndPgPasswordSet_IsTheStringsAlone() =>
        new PostgresDescriber()
            .SecretsOf("Host=h;Password=pw", Environment(("PGPASSWORD", "envpass1")))
            .ShouldBe(["pw"]);

    [Fact]
    public void SecretsOf_NoPasswordAndAPasswordFile_IsItsPasswords()
    {
        using var folder = new TempFolder();
        var file = folder.Write("pgpass", "# c\nh:5432:db:u:filepass1\n*:*:*:*:filepass2\n");

        new PostgresDescriber()
            .SecretsOf("Host=h", Environment(("PGPASSFILE", file)))
            .ShouldBe(["filepass1", "filepass2"]);
    }

    // PGPASSWORD is before the file in Npgsql, so a file is not read when it is set.
    [Fact]
    public void SecretsOf_PgPasswordAndAPasswordFile_IsPgPassword()
    {
        using var folder = new TempFolder();
        var file = folder.Write("pgpass", "h:5432:db:u:filepass1\n");

        new PostgresDescriber()
            .SecretsOf("Host=h", Environment(("PGPASSWORD", "envpass1"), ("PGPASSFILE", file)))
            .ShouldBe(["envpass1"]);
    }

    [Fact]
    public void SecretsOf_PasswordInTheStringAndAPasswordFile_IsTheStringsAlone()
    {
        using var folder = new TempFolder();
        var file = folder.Write("pgpass", "h:5432:db:u:filepass1\n");

        new PostgresDescriber().SecretsOf("Host=h;Password=pw", Environment(("PGPASSFILE", file))).ShouldBe(["pw"]);
    }

    [Fact]
    public void SecretsOf_PassfileInTheString_WinsOverPgPassFile()
    {
        using var folder = new TempFolder();
        var own = folder.Write("own", "h:5432:db:u:ownpass1\n");
        var other = folder.Write("other", "h:5432:db:u:otherpass1\n");

        new PostgresDescriber()
            .SecretsOf($"Host=h;Passfile={own}", Environment(("PGPASSFILE", other)))
            .ShouldBe(["ownpass1"]);
    }

    [Fact]
    public void SecretsOf_DefaultPasswordFileOfHome_IsItsPasswords()
    {
        using var folder = new TempFolder();
        _ = folder.Write(".pgpass", "h:5432:db:u:homepass1\n");

        new PostgresDescriber().SecretsOf("Host=h", Environment(("HOME", folder.Path))).ShouldBe(["homepass1"]);
    }

    [Fact]
    public void SecretsOf_PasswordFileMissingOrAFolder_IsNoneAndDoesNotThrow()
    {
        using var folder = new TempFolder();
        var describer = new PostgresDescriber();

        describer.SecretsOf("Host=h", Environment(("PGPASSFILE", Path.Combine(folder.Path, "none")))).ShouldBeEmpty();
        describer.SecretsOf("Host=h", Environment(("PGPASSFILE", folder.Path))).ShouldBeEmpty();
        describer.SecretsOf("Host=h", Environment(("PGPASSFILE", "bad\0name"))).ShouldBeEmpty();
    }

    // Npgsql reads a password file only for a string that has no Password, as it reads the environment's.
    [Fact]
    public void SecretsOf_SslPasswordOnlyAndPgPasswordSet_IsBoth() =>
        new PostgresDescriber()
            .SecretsOf("Host=h;SSL Password=kp", Environment(("PGPASSWORD", "envpass1")))
            .ShouldBe(["kp", "envpass1"], ignoreOrder: true);

    [Fact]
    public void RewriteOf_ConnectionString_IsTheBuildersText() =>
        new PostgresDescriber()
            .RewriteOf("host=h;password=pw")
            .ShouldBe(new NpgsqlConnectionStringBuilder("host=h;password=pw").ConnectionString);

    [Theory]
    [InlineData("Nonsense=1")]
    [InlineData("Password=pw;Port=abc")]
    public void RewriteOf_ConnectionTheDriverCannotRead_IsNull(string connection) =>
        new PostgresDescriber().RewriteOf(connection).ShouldBeNull();

    private static Func<string, string?> Environment(params (string Name, string Value)[] variables)
    {
        var map = new Dictionary<string, string>();
        foreach (var (name, value) in variables)
        {
            map[name] = value;
        }

        return name => map.GetValueOrDefault(name);
    }

    private sealed class TempFolder : IDisposable
    {
        public string Path { get; } = Directory.CreateTempSubdirectory("sqlsource-pgpass").FullName;

        public string Write(string name, string text)
        {
            var file = System.IO.Path.Combine(Path, name);
            File.WriteAllText(file, text);
            return file;
        }

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }

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

    // The describer refuses Multiplexing=true before it connects, so the port that nothing listens on is never tried.
    [Fact]
    public async Task OpenAsync_Multiplexing_IsSqlsrc236WithTheRefusalAsItsLine()
    {
        var opened = await PostgresScript.OpenAsync(
            LiveExchange.Instance,
            connection: "Host=127.0.0.1;Port=1;Multiplexing=true"
        );

        opened.Value.ShouldBeNull();
        var failure = opened.Failure.ShouldNotBeNull();
        failure.Descriptor.Id.ShouldBe("SQLSRC236");
        failure.ServerLines.ShouldBe(["Multiplexing=true does not keep one session"]);
    }
}
