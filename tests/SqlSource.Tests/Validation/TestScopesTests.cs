using System;
using Shouldly;
using SqlSource.Testing;
using Xunit;

namespace SqlSource.Tests.Validation;

// The reader of tools/test-scopes.txt, tested on small texts: the pattern language, which line wins, and the lines that
// name a test project.  The file itself is checked by TestScopesFileTests.
public sealed class TestScopesTests
{
    private const string Layered = "postgres a/b/*\nnone a/*\n";

    [Fact]
    public void Of_FirstMatchingLine_Decides()
    {
        var scopes = TestScopes.Parse(Layered);

        scopes.Of("a/b/c").ShouldBe(TestScope.Postgres);
        scopes.Of("a/c").ShouldBe(TestScope.None);
    }

    [Fact]
    public void Of_NoMatchingLine_IsBoth() => TestScopes.Parse(Layered).Of("z/c").ShouldBe(TestScope.Both);

    [Fact]
    public void Of_BackslashSeparators_MatchAsSlashes() =>
        TestScopes.Parse(Layered).Of(@"a\b\c").ShouldBe(TestScope.Postgres);

    [Fact]
    public void Matches_Star_CrossesSlashes() => TestScopes.Matches("*.md", "docs/manual/x.md").ShouldBeTrue();

    [Theory]
    [InlineData("a/*", "xa/b", false)]
    [InlineData("a", "ab", false)]
    [InlineData("a*c", "abbc", true)]
    [InlineData("a*c", "abcd", false)]
    public void Matches_Pattern_IsAnchoredAtBothEnds(string pattern, string path, bool matches) =>
        TestScopes.Matches(pattern, path).ShouldBe(matches);

    [Fact]
    public void Matches_OtherCharacters_AreLiteral()
    {
        TestScopes.Matches("a.b", "aXb").ShouldBeFalse();
        TestScopes.Matches("a?b", "aXb").ShouldBeFalse();
    }

    [Fact]
    public void Parse_CommentsAndBlankLines_AreIgnored()
    {
        var scopes = TestScopes.Parse("# a comment\n\n   \npostgres a/*\r\n  # indented\n");

        scopes.Lines.ShouldBe([(TestScope.Postgres, "a/*")]);
    }

    [Fact]
    public void Parse_UnknownScope_ThrowsNamingTheLine() =>
        Should
            .Throw<FormatException>(() => TestScopes.Parse("none a/*\nsometimes b/*\n"))
            .Message.ShouldContain("line 2");

    [Fact]
    public void Parse_MissingPattern_ThrowsNamingTheLine() =>
        Should.Throw<FormatException>(() => TestScopes.Parse("\npostgres\n")).Message.ShouldContain("line 2");

    [Theory]
    [InlineData("?")]
    [InlineData("[")]
    [InlineData("]")]
    public void Parse_PatternWithAGlobCharacter_ThrowsNamingTheLine(string character) =>
        Should
            .Throw<FormatException>(() => TestScopes.Parse($"none a/*\nnone a{character}b\n"))
            .Message.ShouldContain("line 2");

    [Theory]
    [InlineData("project sqlserver")]
    [InlineData("project oracle tests/X/X.csproj")]
    [InlineData("project sqlserver tests/X/X.csproj extra")]
    public void Parse_MalformedProjectLine_ThrowsNamingTheLine(string line) =>
        Should.Throw<FormatException>(() => TestScopes.Parse($"none a/*\n{line}\n")).Message.ShouldContain("line 2");

    [Fact]
    public void Parse_ProjectLine_MapsTheProjectToItsEngine()
    {
        var scopes = TestScopes.Parse("project sqlserver tests/X/X.csproj\nproject postgres tests/Y/Y.csproj\n");

        scopes.Projects["X"].ShouldBe(TestScope.SqlServer);
        scopes.Projects["Y"].ShouldBe(TestScope.Postgres);
        scopes.ProjectFiles.ShouldBe(["tests/X/X.csproj", "tests/Y/Y.csproj"]);
        scopes.Lines.ShouldBeEmpty();
    }

    [Fact]
    public void Runs_ProjectOfTheOtherEngine_IsFalse()
    {
        var scopes = TestScopes.Parse("project sqlserver tests/X/X.csproj");

        scopes.Runs("X", TestScope.Postgres).ShouldBeFalse();
        scopes.Runs("X", TestScope.None).ShouldBeFalse();
        scopes.Runs("X", TestScope.SqlServer).ShouldBeTrue();
    }

    [Fact]
    public void Runs_UnlistedProject_IsTrue()
    {
        var scopes = TestScopes.Parse("project sqlserver tests/X/X.csproj");

        scopes.Runs("Other", TestScope.None).ShouldBeTrue();
        scopes.Runs("Other", TestScope.Postgres).ShouldBeTrue();
    }

    [Fact]
    public void Runs_Both_IsTrueForEveryProject()
    {
        var scopes = TestScopes.Parse("project sqlserver tests/X/X.csproj");

        scopes.Runs("X", TestScope.Both).ShouldBeTrue();
        scopes.Runs("Other", TestScope.Both).ShouldBeTrue();
    }
}
