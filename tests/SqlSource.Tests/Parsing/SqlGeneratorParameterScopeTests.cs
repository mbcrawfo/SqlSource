using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis.Text;
using Shouldly;
using SqlSource.Parsing;
using SqlSource.Settings;
using Xunit;

namespace SqlSource.Tests.Parsing;

public class SqlGeneratorParameterScopeTests
{
    [Fact]
    public void NewScope_HasNoList() => new SqlGeneratorParameterScope().Parameters.ShouldBeNull();

    [Theory]
    // An internal enum cannot be the parameter of a public test method, so a row gives the flags as a number.
    [InlineData("-- generator: keep-comments", 1)]
    [InlineData("-- generator: KEEP-COMMENTS No-Token-Validation", 3)]
    [InlineData("-- generator: sort-input sort-output no-table-models async-method-suffix", 60)]
    [InlineData("-- generator: default", 0)]
    [InlineData("-- generator: DEFAULT", 0)]
    public void Read_KnownParameters_SetTheList(string line, int expected)
    {
        var (scope, errors) = Read(line);

        errors.ShouldBeEmpty();
        ((int?)scope.Parameters).ShouldBe(expected);
    }

    [Fact]
    public void Read_TwoMarkersOfOneScope_AddUp()
    {
        var (scope, errors) = Read("-- generator: keep-comments", "-- generator: sort-input keep-comments");

        errors.ShouldBeEmpty();
        scope.Parameters.ShouldBe(GeneratorParameters.KeepComments | GeneratorParameters.SortInput);
    }

    [Theory]
    [InlineData("keep-comment")]
    // The word was a generator parameter once: omitting no-token-validation says it now.
    [InlineData("token-validation")]
    [InlineData("preserve-comments")]
    [InlineData("strip-comments")]
    [InlineData("foo=bar")]
    // The token-ignore marker was a generator parameter once.
    [InlineData("token-ignore=a")]
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
    [InlineData("keep-comments=1")]
    [InlineData("default=")]
    [InlineData("sort-input=true")]
    public void Read_ParameterWithAValue_IsInvalid(string word)
    {
        var line = "-- generator: " + word;

        var (_, errors) = Read(line);

        errors.ShouldBe([SqlParseError.Create(SqlParseErrorKind.InvalidMarkerValue, SpanOf(line, word), word)]);
    }

    [Theory]
    [InlineData(new[] { "-- generator: default keep-comments" }, "keep-comments")]
    [InlineData(new[] { "-- generator: keep-comments default" }, "default")]
    [InlineData(new[] { "-- generator: default", "-- generator: sort-input" }, "sort-input")]
    [InlineData(new[] { "-- generator: sort-input", "-- generator: default" }, "default")]
    public void Read_DefaultBesideAnotherParameter_ConflictsAtTheSecond(string[] lines, string second)
    {
        var (_, errors) = Read(lines);

        var error = errors.ShouldHaveSingleItem();
        error.Kind.ShouldBe(SqlParseErrorKind.ConflictingSettings);
        error.Arguments.ShouldBe([second]);
    }

    [Fact]
    public void Read_DefaultTwice_IsNotAConflict()
    {
        var (scope, errors) = Read("-- generator: default", "-- generator: default");

        errors.ShouldBeEmpty();
        scope.Parameters.ShouldBe(GeneratorParameters.None);
    }

    [Fact]
    public void Read_ErrorInOneGeneratorParameter_StillAppliesTheOthers()
    {
        var (scope, errors) = Read("-- generator: bogus keep-comments");

        errors.Count.ShouldBe(1);
        scope.Parameters.ShouldBe(GeneratorParameters.KeepComments);
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
