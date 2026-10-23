using Shouldly;
using SqlSource.Tool.Describing.Postgres;
using SqlSource.Tool.Testing;
using Xunit;

namespace SqlSource.Tool.Tests.Describing.Postgres;

// The passwords of a password file as Npgsql 10.0.3 reads it (PgPassFile), and the files it would read.
public sealed class PasswordFileTests
{
    [Fact]
    public void Passwords_EntriesWithCommentsAndBlankLines_AreEachPassword() =>
        PasswordFile.Passwords("# c\n\nh:5432:db:u:pw1\n*:*:*:*:pw2\n").ShouldBe(["pw1", "pw2"]);

    // Npgsql's reader ends a line at a carriage return alone too, as a file of the old Mac's line ends has them.
    [Fact]
    public void Passwords_LinesEndedByCarriageReturnsAlone_AreEachPassword() =>
        PasswordFile.Passwords("h:*:*:u:pw1\rh:*:*:u:pw2\r").ShouldBe(["pw1", "pw2"]);

    // Npgsql trims each line, and a CRLF file is read the same.
    [Fact]
    public void Passwords_LinesWithWhiteSpaceAndCarriageReturns_AreTrimmed() =>
        PasswordFile.Passwords("  h:*:*:u:pw1  \r\n\t# c\r\nh:*:*:u:pw2\r\n").ShouldBe(["pw1", "pw2"]);

    [Fact]
    public void Passwords_EscapedColonAndBackslash_AreUnescaped() =>
        PasswordFile.Passwords(@"h:*:*:u:a\:b\\c\d").ShouldBe([@"a:b\c\d"]);

    [Fact]
    public void Passwords_TrailingBackslash_IsKept() => PasswordFile.Passwords(@"h:*:*:u:abc\").ShouldBe([@"abc\"]);

    [Theory]
    [InlineData("h:5432:db:u")]
    [InlineData("h:5432:db:u:pw:extra")]
    [InlineData("pw")]
    public void Passwords_LineWithoutFiveFields_IsSkipped(string line) =>
        PasswordFile.Passwords(line + "\nh:*:*:u:good").ShouldBe(["good"]);

    // An unescaped colon in the password makes six fields.
    [Fact]
    public void Passwords_PasswordWithAnUnescapedColon_IsSkipped() =>
        PasswordFile.Passwords("h:*:*:u:a:b").ShouldBeEmpty();

    [Fact]
    public void Passwords_PortThatIsNoNumber_IsSkipped() =>
        PasswordFile.Passwords("h:abc:db:u:bad\nh:5432:db:u:good").ShouldBe(["good"]);

    [Fact]
    public void Passwords_WildcardPassword_IsNone() => PasswordFile.Passwords("h:*:*:u:*").ShouldBeEmpty();

    [Fact]
    public void Passwords_EmptyPassword_IsNone() => PasswordFile.Passwords("h:*:*:u:").ShouldBeEmpty();

    [Fact]
    public void Passwords_EmptyText_IsNone() => PasswordFile.Passwords("").ShouldBeEmpty();

    [Fact]
    public void PathsOf_PassfileGiven_IsItAlone() =>
        PasswordFile
            .PathsOf("/p/own", Environments.Of(("PGPASSFILE", "/e/file"), ("HOME", "/home/u")))
            .ShouldBe(["/p/own"]);

    [Fact]
    public void PathsOf_PgPassFileSet_IsIt() =>
        PasswordFile
            .PathsOf(null, Environments.Of(("PGPASSFILE", "/e/file"), ("HOME", "/home/u")))
            .ShouldBe(["/e/file"]);

    [Fact]
    public void PathsOf_NeitherSet_IsTheDefaultsOfHomeAndAppData() =>
        PasswordFile
            .PathsOf(null, Environments.Of(("HOME", "/home/u"), ("APPDATA", "/app")))
            .ShouldBe([
                System.IO.Path.Combine("/home/u", ".pgpass"),
                System.IO.Path.Combine("/app", "postgresql", "pgpass.conf"),
            ]);

    [Fact]
    public void PathsOf_OnlyHomeSet_IsTheHomeDefault() =>
        PasswordFile
            .PathsOf(null, Environments.Of(("HOME", "/home/u")))
            .ShouldBe([System.IO.Path.Combine("/home/u", ".pgpass")]);

    [Fact]
    public void PathsOf_NothingSet_IsNone() => PasswordFile.PathsOf(null, Environments.Of()).ShouldBeEmpty();

    [Fact]
    public void PathsOf_EmptyValues_AreNotSet() =>
        PasswordFile.PathsOf("", Environments.Of(("PGPASSFILE", ""), ("HOME", ""))).ShouldBeEmpty();
}
