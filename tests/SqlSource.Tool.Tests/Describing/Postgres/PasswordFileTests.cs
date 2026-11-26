using System.IO;
using System.Linq;
using Shouldly;
using SqlSource.Tool.Describing.Postgres;
using SqlSource.Tool.Testing;
using Xunit;

namespace SqlSource.Tool.Tests.Describing.Postgres;

// The passwords of a password file that Npgsql 10.0.3 may use (PgPassFile), and the file it would read.
public sealed class PasswordFileTests
{
    private static readonly PasswordFile.Target Any = PasswordFile.Target.Unknown;

    [Fact]
    public void Passwords_EntriesWithCommentsAndBlankLines_AreEachPassword() =>
        PasswordFile.Passwords("# c\n\nh:5432:db:u:pw1\n*:*:*:*:pw2\n", Any).ShouldBe(["pw1", "pw2"]);

    // Npgsql's reader ends a line at a carriage return alone too, as a file of the old Mac's line ends has them.
    [Fact]
    public void Passwords_LinesEndedByCarriageReturnsAlone_AreEachPassword() =>
        PasswordFile.Passwords("h:*:*:u:pw1\rh:*:*:u:pw2\r", Any).ShouldBe(["pw1", "pw2"]);

    // Npgsql trims each line, and a CRLF file is read the same.
    [Fact]
    public void Passwords_LinesWithWhiteSpaceAndCarriageReturns_AreTrimmed() =>
        PasswordFile.Passwords("  h:*:*:u:pw1  \r\n\t# c\r\nh:*:*:u:pw2\r\n", Any).ShouldBe(["pw1", "pw2"]);

    [Fact]
    public void Passwords_EscapedColonAndBackslash_AreUnescaped() =>
        PasswordFile.Passwords(@"h:*:*:u:a\:b\\c\d", Any).ShouldBe([@"a:b\c\d"]);

    [Fact]
    public void Passwords_TrailingBackslash_IsKept() =>
        PasswordFile.Passwords(@"h:*:*:u:abc\", Any).ShouldBe([@"abc\"]);

    // Npgsql throws at a line it cannot read, so it never uses an entry after one.
    [Theory]
    [InlineData("h:5432:db:u")]
    [InlineData("h:5432:db:u:pw:extra")]
    [InlineData("pw")]
    public void Passwords_LineWithoutFiveFields_EndsTheSearch(string line) =>
        PasswordFile.Passwords("h:*:*:u:before\n" + line + "\nh:*:*:u:after", Any).ShouldBe(["before"]);

    // An unescaped colon in the password makes six fields.
    [Fact]
    public void Passwords_PasswordWithAnUnescapedColon_IsNone() =>
        PasswordFile.Passwords("h:*:*:u:a:b", Any).ShouldBeEmpty();

    [Fact]
    public void Passwords_PortThatIsNoNumber_EndsTheSearch() =>
        PasswordFile.Passwords("h:abc:db:u:bad\nh:5432:db:u:after", Any).ShouldBeEmpty();

    [Fact]
    public void Passwords_WildcardPassword_IsNone() => PasswordFile.Passwords("h:*:*:u:*", Any).ShouldBeEmpty();

    [Fact]
    public void Passwords_EmptyPassword_IsNone() => PasswordFile.Passwords("h:*:*:u:", Any).ShouldBeEmpty();

    [Fact]
    public void Passwords_EmptyText_IsNone() => PasswordFile.Passwords("", Any).ShouldBeEmpty();

    // Npgsql uses the first entry that matches, so the search ends at the first that surely does.
    [Fact]
    public void Passwords_EntriesUpToTheFirstSureMatch_AreGivenAndNoneAfter() =>
        PasswordFile
            .Passwords("h:5432:db:u:first\nh:5432:*:*:second\n*:*:*:*:third\n", Of("h", 5432, "db", "u"))
            .ShouldBe(["first"]);

    // The fields are compared ordinally, as Npgsql compares them.
    [Theory]
    [InlineData("other:5432:db:u:skipped")]
    [InlineData("h:5433:db:u:skipped")]
    [InlineData("h:5432:other:u:skipped")]
    [InlineData("h:5432:db:other:skipped")]
    [InlineData("H:5432:db:u:skipped")]
    public void Passwords_EntryWithAFieldThatDiffers_IsSkipped(string entry) =>
        PasswordFile.Passwords(entry + "\n*:*:*:*:used", Of("h", 5432, "db", "u")).ShouldBe(["used"]);

    // A string without a database gives Npgsql none to compare, and every entry's database matches.
    [Fact]
    public void Passwords_NoDatabase_MatchesEveryDatabase() =>
        PasswordFile.Passwords("h:5432:billing:u:used\n*:*:*:*:after", Of("h", 5432, null, "u")).ShouldBe(["used"]);

    // Npgsql takes a user from Kerberos or the system that the tool does not ask, so any user may match, and only a
    // wildcard surely does.
    [Fact]
    public void Passwords_UnknownUser_MayMatchEveryUserUpToAWildcard() =>
        PasswordFile
            .Passwords(
                "h:5432:db:alice:a\nh:5432:db:bob:b\nother:5432:db:alice:c\nh:5432:db:*:d\n*:*:*:*:e",
                Of("h", 5432, "db", null)
            )
            .ShouldBe(["a", "b", "d"]);

    [Fact]
    public void Passwords_SamePasswordTwice_IsGivenOnce() =>
        PasswordFile.Passwords("h:*:*:alice:same\nh:*:*:bob:same", Of("h", 5432, "db", null)).ShouldBe(["same"]);

    // Each server of a string with several hosts may be the one Npgsql connects to.
    [Fact]
    public void Passwords_SeveralServers_GiveEachOnesMatch() =>
        PasswordFile
            .Passwords(
                "a:5432:*:*:pa\nb:6000:*:*:pb\nc:5432:*:*:pc",
                new PasswordFile.Target(PasswordFile.ServersOf("a, b:6000", 5432), null, "u")
            )
            .ShouldBe(["pa", "pb"]);

    // A host is split from its port as Npgsql's TrySplitHostPort splits it, but for a single one that names a socket.
    // A host of one letter and a colon is a drive on Windows, so the hosts here are longer.
    [Theory]
    [InlineData("pg1", "pg1:5432")]
    [InlineData("pg1:6000", "pg1:6000")]
    [InlineData(" pg1 , pg2:6000 ,pg1", "pg1:5432|pg2:6000")]
    [InlineData("[::1]:6000", "[::1]:6000")]
    [InlineData("::1", "::1:5432")]
    [InlineData("pg1:abc", "pg1:abc:5432")]
    [InlineData("/tmp:6000", "/tmp:6000:5432")]
    [InlineData("@pg:6000", "@pg:6000:5432")]
    [InlineData("/tmp:6000,pg1", "/tmp:6000|pg1:5432")]
    public void ServersOf_Host_IsEachServerAsNpgsqlSplitsIt(string host, string expected) =>
        PasswordFile
            .ServersOf(host, 5432)
            .Select(static server => server.Host + ":" + server.Port)
            .ShouldBe(expected.Split('|'));

    [Theory]
    [InlineData(null)]
    [InlineData(" ")]
    public void ServersOf_NoHost_IsAnUnknownHost(string? host) =>
        PasswordFile.ServersOf(host, 5432).ShouldBe([new PasswordFile.Server(null, 5432)]);

    [Fact]
    public void PathsOf_PassfileGiven_IsItAlone() =>
        PasswordFile
            .PathsOf("/p/own", Environments.Of(("PGPASSFILE", "/e/file"), ("HOME", "/home/u")), windows: false)
            .ShouldBe(["/p/own"]);

    [Fact]
    public void PathsOf_PgPassFileSet_IsIt() =>
        PasswordFile
            .PathsOf(null, Environments.Of(("PGPASSFILE", "/e/file"), ("HOME", "/home/u")), windows: false)
            .ShouldBe(["/e/file"]);

    // Npgsql reads the one default file of the system it runs on.
    [Theory]
    [InlineData(false, ".pgpass")]
    [InlineData(true, "postgresql|pgpass.conf")]
    public void PathsOf_NeitherSet_IsTheDefaultOfTheSystem(bool windows, string under) =>
        PasswordFile
            .PathsOf(null, Environments.Of(("HOME", "/home/u"), ("APPDATA", "/app")), windows)
            .ShouldBe([Path.Combine([windows ? "/app" : "/home/u", .. under.Split('|')])]);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PathsOf_NothingSet_IsNone(bool windows) =>
        PasswordFile.PathsOf(null, Environments.Of(), windows).ShouldBeEmpty();

    [Fact]
    public void PathsOf_EmptyValues_AreNotSet() =>
        PasswordFile
            .PathsOf("", Environments.Of(("PGPASSFILE", ""), ("HOME", ""), ("APPDATA", "")), windows: false)
            .ShouldBeEmpty();

    private static PasswordFile.Target Of(string host, int port, string? database, string? user) =>
        new([new PasswordFile.Server(host, port)], database, user);
}
