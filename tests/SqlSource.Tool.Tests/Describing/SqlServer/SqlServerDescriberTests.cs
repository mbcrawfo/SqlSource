using System;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Parsing;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Describing.SqlServer;
using SqlSource.Tool.Testing;
using Xunit;

namespace SqlSource.Tool.Tests.Describing.SqlServer;

// The describer of SQL Server where no server is needed: its dialect, and the parts of a connection's value that it
// gives the run to remove.
public sealed class SqlServerDescriberTests
{
    [Fact]
    public void Dialect_Is_SqlServer() => new SqlServerDescriber().Dialect.ShouldBe(SqlDialect.SqlServer);

    [Fact]
    public void SecretsOf_ValueWithAPassword_GivesThePassword() =>
        new SqlServerDescriber().SecretsOf("Data Source=x;Password=Sesame123").ShouldBe(["Sesame123"]);

    [Fact]
    public void SecretsOf_ValueWithoutAPassword_GivesNone() =>
        new SqlServerDescriber().SecretsOf("Data Source=x;Integrated Security=true").ShouldBeEmpty();

    // A value SqlClient cannot read has no parts it knows, and opening it fails in its own place.  The builder throws
    // an ArgumentException for the first two, an OverflowException, an InvalidOperationException and a
    // NotSupportedException for the last three.
    [Theory]
    [InlineData("Data Source=x;Nonsense=1")]
    [InlineData("=;")]
    [InlineData("Data Source=x;Password=Sesame123;Connect Timeout=99999999999")]
    [InlineData("Data Source=x;Password=Sesame123;Context Connection=true")]
    [InlineData("Data Source=x;Password=Sesame123;Network Library=dbmssocn")]
    public void SecretsOf_ValueSqlClientCannotRead_GivesNoneAndDoesNotThrow(string connection) =>
        new SqlServerDescriber().SecretsOf(connection).ShouldBeEmpty();

    // Each is refused before anything is opened, with the describer's own line and nothing of SqlClient's text.
    [Theory]
    [InlineData(";", "the connection string is empty")]
    [InlineData("  ", "the connection string is empty")]
    [InlineData("Data Source=127.0.0.1,1;Nonsense=" + Secrets.Marker, "SqlClient cannot read the connection string")]
    [InlineData(
        "Data Source=127.0.0.1,1;Connect Timeout=" + Secrets.Marker,
        "SqlClient cannot read the connection string"
    )]
    [InlineData(
        "Data Source=127.0.0.1,1;Password=" + Secrets.Marker + ";Connect Timeout=99999999999",
        "SqlClient cannot read the connection string"
    )]
    [InlineData(
        "Data Source=127.0.0.1,1;Password=" + Secrets.Marker + ";Context Connection=true",
        "SqlClient cannot read the connection string"
    )]
    [InlineData(
        "Data Source=127.0.0.1,1;Password=" + Secrets.Marker + ";Network Library=dbmssocn",
        "SqlClient cannot read the connection string"
    )]
    [InlineData(
        "Data Source=127.0.0.1,1;Authentication=Sql Password;Password=" + Secrets.Marker,
        "the Authentication of the connection string needs both a User ID and a Password"
    )]
    [InlineData(
        "Data Source=127.0.0.1,1;Authentication=Sql Password;User ID=" + Secrets.Marker,
        "the Authentication of the connection string needs both a User ID and a Password"
    )]
    [InlineData(
        "Data Source=127.0.0.1,1;Authentication=Sql Password;User ID=" + Secrets.Marker + ";Password=",
        "the Authentication of the connection string needs both a User ID and a Password"
    )]
    [InlineData(
        "Data Source=127.0.0.1,1;Authentication=Active Directory Password;User ID=" + Secrets.Marker,
        "the Authentication of the connection string needs both a User ID and a Password"
    )]
    [InlineData(
        "Data Source=127.0.0.1,1;Authentication=Active Directory Service Principal;Password=" + Secrets.Marker,
        "the Authentication of the connection string needs both a User ID and a Password"
    )]
    [InlineData(
        "Data Source=127.0.0.1,1;Authentication=Active Directory Integrated;Password=" + Secrets.Marker,
        "SqlClient does not accept these settings of the connection string together"
    )]
    [InlineData(
        "Data Source=127.0.0.1,1;Integrated Security=true;Authentication=Sql Password;Password=" + Secrets.Marker,
        "SqlClient does not accept these settings of the connection string together"
    )]
    public async Task OpenAsync_ConnectionStringRefusedBeforeOpening_IsSqlsrc224WithTheFixedLine(
        string connection,
        string line
    )
    {
        var opened = await new SqlServerDescriber().OpenAsync("app", connection, LiveExchange.Instance);

        opened.Value.ShouldBeNull();
        var failure = opened.Failure.ShouldNotBeNull();
        failure.Descriptor.Id.ShouldBe("SQLSRC224");
        failure.ServerLines.ShouldBe([line]);
        failure
            .Arguments.Concat(failure.ServerLines)
            .ShouldAllBe(text => !text.Contains(Secrets.Marker, StringComparison.Ordinal));
    }

    // SqlClient supports LocalDB on Windows alone, and elsewhere throws a PlatformNotSupportedException as it opens.
    [Fact]
    public async Task OpenAsync_LocalDbOffWindows_IsSqlsrc224WithTheFixedLine()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "SqlClient supports LocalDB on Windows.");

        var opened = await new SqlServerDescriber().OpenAsync(
            "app",
            @"Data Source=(localdb)\MSSQLLocalDB;Password=" + Secrets.Marker,
            LiveExchange.Instance
        );

        opened.Value.ShouldBeNull();
        var failure = opened.Failure.ShouldNotBeNull();
        failure.Descriptor.Id.ShouldBe("SQLSRC224");
        failure.ServerLines.ShouldBe(["SqlClient does not support this server on this platform"]);
        failure
            .Arguments.Concat(failure.ServerLines)
            .ShouldAllBe(text => !text.Contains(Secrets.Marker, StringComparison.Ordinal));
    }
}
