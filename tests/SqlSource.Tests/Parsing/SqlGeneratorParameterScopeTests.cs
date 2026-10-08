using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis.Text;
using Shouldly;
using SqlSource.Parsing;
using Xunit;

namespace SqlSource.Tests.Parsing;

public class SqlGeneratorParameterScopeTests
{
    [Fact]
    public void NewScope_HasNoGeneratorParameters()
    {
        var scope = new SqlGeneratorParameterScope();

        scope.KeepComments.ShouldBeFalse();
        scope.TokenValidation.ShouldBeNull();
        scope.IgnoredTokens.ShouldBeEmpty();
    }

    [Fact]
    public void Read_KeepComments_SetsTheFlag()
    {
        var (scope, errors) = Read("-- generator: keep-comments");

        errors.ShouldBeEmpty();
        scope.KeepComments.ShouldBeTrue();
        scope.TokenValidation.ShouldBeNull();
    }

    [Theory]
    [InlineData("-- generator: token-validation", true)]
    [InlineData("-- generator: no-token-validation", false)]
    public void Read_ValidationGeneratorParameter_SetsTokenValidation(string line, bool expected)
    {
        var (scope, errors) = Read(line);

        errors.ShouldBeEmpty();
        scope.TokenValidation.ShouldBe(expected);
    }

    [Fact]
    public void Read_GeneratorParameterNames_AreCaseInsensitive()
    {
        var (scope, errors) = Read("-- generator: KEEP-COMMENTS No-Token-Validation Token-Ignore=a");

        errors.ShouldBeEmpty();
        scope.KeepComments.ShouldBeTrue();
        scope.TokenValidation.ShouldBe(false);
        scope.IgnoredTokens.ShouldBe(["a"]);
    }

    [Fact]
    public void Read_SeveralGeneratorParametersOnOneLine_AppliesEach()
    {
        var (scope, errors) = Read("-- generator: keep-comments   token-ignore=a\ttoken-ignore=b");

        errors.ShouldBeEmpty();
        scope.KeepComments.ShouldBeTrue();
        scope.IgnoredTokens.ShouldBe(["a", "b"], ignoreOrder: true);
    }

    [Fact]
    public void Read_SeveralMarkers_Accumulate()
    {
        var (scope, errors) = Read(
            "-- generator: keep-comments",
            "-- generator: token-validation",
            "-- generator: token-ignore=a"
        );

        errors.ShouldBeEmpty();
        scope.KeepComments.ShouldBeTrue();
        scope.TokenValidation.ShouldBe(true);
        scope.IgnoredTokens.ShouldBe(["a"]);
    }

    [Fact]
    public void Read_RepeatedGeneratorParameter_IsAllowed()
    {
        var (scope, errors) = Read(
            "-- generator: keep-comments keep-comments token-validation token-ignore=a",
            "-- generator: token-validation token-ignore=a"
        );

        errors.ShouldBeEmpty();
        scope.TokenValidation.ShouldBe(true);
        scope.IgnoredTokens.ShouldBe(["a"]);
    }

    [Fact]
    public void Read_TokenIgnore_KeepsTheNameAsWrittenAndAcceptsAKeyword()
    {
        var (scope, errors) = Read("-- generator: token-ignore=Table token-ignore=class");

        errors.ShouldBeEmpty();
        scope.IgnoredTokens.ShouldBe(["Table", "class"], ignoreOrder: true);
    }

    [Theory]
    [InlineData("keep-comment")]
    [InlineData("preserve-comments")]
    [InlineData("strip-comments")]
    [InlineData("foo=bar")]
    [InlineData("=x")]
    // The dialect is a marker of its own.  As a generator parameter the word means nothing.
    [InlineData("dialect=mysql")]
    [InlineData("dialect")]
    [InlineData("DIALECT=postgres")]
    public void Read_UnknownGeneratorParameter_IsAnError(string parameter)
    {
        var line = "-- generator: " + parameter;

        var (_, errors) = Read(line);

        errors.ShouldBe([
            SqlParseError.Create(SqlParseErrorKind.UnknownGeneratorParameter, SpanOf(line, parameter), parameter),
        ]);
    }

    [Theory]
    [InlineData("-- generator:")]
    [InlineData("-- generator:   ")]
    public void Read_MarkerWithoutGeneratorParameters_IsAnError(string line)
    {
        var (_, errors) = Read(line);

        errors.ShouldBe([SqlParseError.Create(SqlParseErrorKind.EmptyGeneratorLine, new TextSpan(0, line.Length))]);
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
    public void Read_MissingOrUnexpectedValue_IsAnError(string parameter)
    {
        var line = "-- generator: " + parameter;

        var (scope, errors) = Read(line);

        errors.ShouldBe([
            SqlParseError.Create(SqlParseErrorKind.InvalidMarkerValue, SpanOf(line, parameter), parameter),
        ]);
        scope.KeepComments.ShouldBeFalse();
        scope.TokenValidation.ShouldBeNull();
        scope.IgnoredTokens.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("token-validation", "no-token-validation", true)]
    [InlineData("no-token-validation", "token-validation", false)]
    public void Read_BothValidationGeneratorParametersOnOneLine_ReportsTheSecond(string first, string second, bool kept)
    {
        var line = $"-- generator: {first} {second}";

        var (scope, errors) = Read(line);

        errors.ShouldBe([
            SqlParseError.Create(
                SqlParseErrorKind.ConflictingSettings,
                new TextSpan(line.Length - second.Length, second.Length),
                second
            ),
        ]);
        scope.TokenValidation.ShouldBe(kept);
    }

    [Fact]
    public void Read_BothValidationGeneratorParametersOnSeparateLines_IsAnError()
    {
        var (_, errors) = Read("-- generator: token-validation", "-- generator: no-token-validation");

        errors.Count.ShouldBe(1);
        errors[0].Kind.ShouldBe(SqlParseErrorKind.ConflictingSettings);
    }

    [Fact]
    public void Read_ErrorInOneGeneratorParameter_StillAppliesTheOthers()
    {
        var (scope, errors) = Read("-- generator: bogus keep-comments");

        errors.Count.ShouldBe(1);
        scope.KeepComments.ShouldBeTrue();
    }

    // Each line is lexed alone, so every generator parameter is at the offset it has in its own line.
    private static (SqlGeneratorParameterScope Scope, List<SqlParseError> Errors) Read(params string[] lines)
    {
        var scope = new SqlGeneratorParameterScope();
        var errors = new List<SqlParseError>();
        foreach (var line in lines)
        {
            scope.Read(line, Marker(line), errors);
        }

        return (scope, errors);
    }

    private static SqlMarker Marker(string line)
    {
        var marker = SqlMarkerReader.Read(line, SqlLexer.Lex(line, SqlDialectRules.Ansi).Lexemes[0]);
        return marker.ShouldNotBeNull();
    }

    private static TextSpan SpanOf(string text, string value) =>
        new(text.LastIndexOf(value, StringComparison.Ordinal), value.Length);
}
