using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis.Text;
using Shouldly;
using SqlSource.Parsing;
using Xunit;

namespace SqlSource.Tests.Parsing;

public class SqlDirectiveScopeTests
{
    [Fact]
    public void NewScope_HasNoDirectives()
    {
        var scope = new SqlDirectiveScope();

        scope.KeepComments.ShouldBeFalse();
        scope.TokenValidation.ShouldBeNull();
        scope.IgnoredTokens.ShouldBeEmpty();
    }

    [Fact]
    public void Read_KeepComments_SetsTheFlag()
    {
        var (scope, errors) = Read("-- SqlSource: keep-comments");

        errors.ShouldBeEmpty();
        scope.KeepComments.ShouldBeTrue();
        scope.TokenValidation.ShouldBeNull();
    }

    [Theory]
    [InlineData("-- SqlSource: token-validation", true)]
    [InlineData("-- SqlSource: no-token-validation", false)]
    public void Read_ValidationDirective_SetsTokenValidation(string line, bool expected)
    {
        var (scope, errors) = Read(line);

        errors.ShouldBeEmpty();
        scope.TokenValidation.ShouldBe(expected);
    }

    [Fact]
    public void Read_DirectiveNames_AreCaseInsensitive()
    {
        var (scope, errors) = Read("-- SqlSource: KEEP-COMMENTS No-Token-Validation Token-Ignore=a");

        errors.ShouldBeEmpty();
        scope.KeepComments.ShouldBeTrue();
        scope.TokenValidation.ShouldBe(false);
        scope.IgnoredTokens.ShouldBe(["a"]);
    }

    [Fact]
    public void Read_SeveralDirectivesOnOneLine_AppliesEach()
    {
        var (scope, errors) = Read("-- SqlSource: keep-comments   token-ignore=a\ttoken-ignore=b");

        errors.ShouldBeEmpty();
        scope.KeepComments.ShouldBeTrue();
        scope.IgnoredTokens.ShouldBe(["a", "b"], ignoreOrder: true);
    }

    [Fact]
    public void Read_SeveralMarkers_Accumulate()
    {
        var (scope, errors) = Read(
            "-- SqlSource: keep-comments",
            "-- SqlSource: token-validation",
            "-- SqlSource: token-ignore=a"
        );

        errors.ShouldBeEmpty();
        scope.KeepComments.ShouldBeTrue();
        scope.TokenValidation.ShouldBe(true);
        scope.IgnoredTokens.ShouldBe(["a"]);
    }

    [Fact]
    public void Read_RepeatedDirective_IsAllowed()
    {
        var (scope, errors) = Read(
            "-- SqlSource: keep-comments keep-comments token-validation token-ignore=a",
            "-- SqlSource: token-validation token-ignore=a"
        );

        errors.ShouldBeEmpty();
        scope.TokenValidation.ShouldBe(true);
        scope.IgnoredTokens.ShouldBe(["a"]);
    }

    [Fact]
    public void Read_TokenIgnore_KeepsTheNameAsWrittenAndAcceptsAKeyword()
    {
        var (scope, errors) = Read("-- SqlSource: token-ignore=Table token-ignore=class");

        errors.ShouldBeEmpty();
        scope.IgnoredTokens.ShouldBe(["Table", "class"], ignoreOrder: true);
    }

    [Theory]
    [InlineData("keep-comment")]
    [InlineData("preserve-comments")]
    [InlineData("strip-comments")]
    [InlineData("foo=bar")]
    [InlineData("=x")]
    public void Read_UnknownDirective_IsAnError(string directive)
    {
        var line = "-- SqlSource: " + directive;

        var (_, errors) = Read(line);

        errors.ShouldBe([SqlParseError.Create(SqlParseErrorKind.UnknownDirective, SpanOf(line, directive), directive)]);
    }

    [Theory]
    [InlineData("-- SqlSource:")]
    [InlineData("-- SqlSource:   ")]
    public void Read_MarkerWithoutDirectives_IsAnError(string line)
    {
        var (_, errors) = Read(line);

        errors.ShouldBe([SqlParseError.Create(SqlParseErrorKind.EmptyDirectiveLine, new TextSpan(0, line.Length))]);
    }

    [Theory]
    [InlineData("token-ignore")]
    [InlineData("token-ignore=")]
    [InlineData("token-ignore=1x")]
    [InlineData("token-ignore=a=b")]
    [InlineData("token-ignore=a,b")]
    [InlineData("keep-comments=x")]
    [InlineData("token-validation=true")]
    [InlineData("no-token-validation=")]
    public void Read_MissingOrUnexpectedValue_IsAnError(string directive)
    {
        var line = "-- SqlSource: " + directive;

        var (scope, errors) = Read(line);

        errors.ShouldBe([
            SqlParseError.Create(SqlParseErrorKind.InvalidDirectiveValue, SpanOf(line, directive), directive),
        ]);
        scope.KeepComments.ShouldBeFalse();
        scope.TokenValidation.ShouldBeNull();
        scope.IgnoredTokens.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("token-validation", "no-token-validation", true)]
    [InlineData("no-token-validation", "token-validation", false)]
    public void Read_BothValidationDirectivesOnOneLine_ReportsTheSecond(string first, string second, bool kept)
    {
        var line = $"-- SqlSource: {first} {second}";

        var (scope, errors) = Read(line);

        errors.ShouldBe([
            SqlParseError.Create(
                SqlParseErrorKind.ConflictingDirectives,
                new TextSpan(line.Length - second.Length, second.Length),
                second
            ),
        ]);
        scope.TokenValidation.ShouldBe(kept);
    }

    [Fact]
    public void Read_BothValidationDirectivesOnSeparateLines_IsAnError()
    {
        var (_, errors) = Read("-- SqlSource: token-validation", "-- SqlSource: no-token-validation");

        errors.Count.ShouldBe(1);
        errors[0].Kind.ShouldBe(SqlParseErrorKind.ConflictingDirectives);
    }

    [Fact]
    public void Read_ErrorInOneDirective_StillAppliesTheOthers()
    {
        var (scope, errors) = Read("-- SqlSource: bogus keep-comments");

        errors.Count.ShouldBe(1);
        scope.KeepComments.ShouldBeTrue();
    }

    private static (SqlDirectiveScope Scope, List<SqlParseError> Errors) Read(params string[] lines)
    {
        var scope = new SqlDirectiveScope();
        var errors = new List<SqlParseError>();
        foreach (var line in lines)
        {
            var marker = SqlMarkerReader.Read(line, SqlLexer.Lex(line).Lexemes[0]);
            _ = marker.ShouldNotBeNull();
            scope.Read(line, marker.Value, errors);
        }

        return (scope, errors);
    }

    private static TextSpan SpanOf(string text, string value) =>
        new(text.LastIndexOf(value, StringComparison.Ordinal), value.Length);
}
