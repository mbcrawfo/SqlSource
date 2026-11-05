using System.Collections.Generic;
using Microsoft.CodeAnalysis.Text;
using Shouldly;
using SqlSource.Parsing;
using SqlSource.Parsing.Markers;
using SqlSource.Settings;
using Xunit;

namespace SqlSource.Tests.Parsing.Markers;

public sealed class SqlGeneratorParameterScopeTests
{
    private static readonly GeneratorParameterChoices KeepCommentsOn = GeneratorParameterChoices.None.With(
        GeneratorParameters.KeepComments,
        GeneratorParameterState.On
    );

    [Fact]
    public void Parameters_OfANewScope_AreEmpty() => new SqlGeneratorParameterScope().Parameters.IsEmpty.ShouldBeTrue();

    [Theory]
    // An internal enum cannot be the parameter of a public test method, so a row gives the flags as numbers: the
    // parameters the scope names, and those of them it turns on.
    [InlineData("-- generator: keep-comments", 1, 1)]
    [InlineData("-- generator: KEEP-COMMENTS=Off", 1, 0)]
    [InlineData("-- generator: keep-comments=ON", 1, 1)]
    [InlineData("-- generator: keep-comments=default", 1, 0)]
    [InlineData("-- generator: token-validation=off", 2, 0)]
    [InlineData("-- generator: array-pool=OFF table-models=off", 80, 0)]
    [InlineData("-- generator: array-pool", 64, 64)]
    [InlineData("-- generator: KEEP-COMMENTS TOKEN-VALIDATION", 3, 3)]
    [InlineData("-- generator: sort-input sort-output table-models async-method-suffix", 60, 60)]
    [InlineData("-- generator: keep-comments=off sort-input", 5, 4)]
    public void Read_Words_NameTheirStates(string line, int named, int on)
    {
        var (scope, errors) = Read(line);

        errors.ShouldBeEmpty();
        ((int)scope.Parameters.Named).ShouldBe(named);
        ((int)scope.Parameters.On).ShouldBe(on);
    }

    [Theory]
    [InlineData("-- generator: default")]
    [InlineData("-- generator: DEFAULT")]
    [InlineData("-- generator: default default")]
    public void Read_Default_IsTheReset(string line)
    {
        var (scope, errors) = Read(line);

        errors.ShouldBeEmpty();
        scope.Parameters.ShouldBe(GeneratorParameterChoices.Reset);
    }

    [Theory]
    [InlineData([new[] { "-- generator: default keep-comments" }])]
    [InlineData([new[] { "-- generator: keep-comments default" }])]
    [InlineData([new[] { "-- generator: keep-comments", "-- generator: default" }])]
    public void Read_DefaultBesideOtherWords_ResetsThenAppliesThemInAnyOrder(string[] lines)
    {
        var (scope, errors) = Read(lines);

        errors.ShouldBeEmpty();
        scope.Parameters.ShouldBe(
            GeneratorParameterChoices.Reset.With(GeneratorParameters.KeepComments, GeneratorParameterState.On)
        );
    }

    [Fact]
    public void Read_TwoMarkersOfOneScope_AddUp()
    {
        var (scope, errors) = Read("-- generator: keep-comments", "-- generator: sort-input keep-comments");

        errors.ShouldBeEmpty();
        scope.Parameters.ShouldBe(KeepCommentsOn.With(GeneratorParameters.SortInput, GeneratorParameterState.On));
    }

    // The later word is not applied, so the earlier state stays.
    [Theory]
    [InlineData(new[] { "-- generator: keep-comments keep-comments=off" }, "keep-comments=off", true)]
    [InlineData(new[] { "-- generator: keep-comments=default keep-comments=off" }, "keep-comments=off", false)]
    [InlineData(new[] { "-- generator: keep-comments=off", "-- generator: keep-comments" }, "keep-comments", false)]
    public void Read_OneOptionInTwoStates_ConflictsAtTheLater(string[] lines, string later, bool keepsComments)
    {
        var (scope, errors) = Read(lines);

        var error = errors.ShouldHaveSingleItem();
        error.Kind.ShouldBe(SqlParseErrorKind.ConflictingSettings);
        error.Span.ShouldBe(TextSpans.Of(lines[^1], later, last: true));
        error.Arguments.ShouldBe([later]);
        scope.Parameters.StateOf(GeneratorParameters.KeepComments).ShouldBe(keepsComments);
    }

    [Fact]
    public void Read_OneStateTwice_IsNotAConflict()
    {
        var (scope, errors) = Read("-- generator: keep-comments keep-comments=on KEEP-COMMENTS");

        errors.ShouldBeEmpty();
        scope.Parameters.ShouldBe(KeepCommentsOn);
    }

    [Theory]
    [InlineData("keep-comment")]
    [InlineData("no-arraypool")]
    // The words that turned the three options off before they were named for what they do.
    [InlineData("no-token-validation")]
    [InlineData("no-table-models")]
    [InlineData("no-array-pool")]
    [InlineData("NO-ARRAY-POOL")]
    [InlineData("preserve-comments")]
    [InlineData("strip-comments")]
    [InlineData("foo=bar")]
    // The token-ignore marker was a generator parameter once.
    [InlineData("token-ignore=a")]
    [InlineData("=x")]
    [InlineData("=off")]
    [InlineData("keep-comment=off")]
    // The dialect is a marker of its own.  As a generator parameter the word means nothing.
    [InlineData("dialect=mysql")]
    [InlineData("dialect")]
    [InlineData("DIALECT=postgres")]
    public void Read_UnknownGeneratorParameter_IsAnError(string parameter)
    {
        var line = "-- generator: " + parameter;

        var (_, errors) = Read(line);

        errors.ShouldBe([
            SqlParseError.Create(
                SqlParseErrorKind.UnknownGeneratorParameter,
                TextSpans.Of(line, parameter, last: true),
                parameter
            ),
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
    [InlineData("keep-comments=")]
    [InlineData("sort-input=true")]
    [InlineData("keep-comments=off=on")]
    [InlineData("default=")]
    [InlineData("default=off")]
    public void Read_ParameterWithAValueThatIsNoState_IsInvalid(string word)
    {
        var line = "-- generator: " + word;

        var (scope, errors) = Read(line);

        errors.ShouldBe([
            SqlParseError.Create(SqlParseErrorKind.InvalidMarkerValue, TextSpans.Of(line, word, last: true), word, ""),
        ]);
        scope.Parameters.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void Read_ErrorInOneGeneratorParameter_StillAppliesTheOthers()
    {
        var (scope, errors) = Read("-- generator: bogus keep-comments");

        errors.Count.ShouldBe(1);
        scope.Parameters.ShouldBe(KeepCommentsOn);
    }

    [Fact]
    public void Read_WordsSeparatedByTabsAndRunsOfSpaces_AreEachReportedAtTheirOwnPlace()
    {
        const string Line = "-- generator: keep-comments \t nope   foo=bar";

        var (scope, errors) = Read(Line);

        scope.Parameters.ShouldBe(KeepCommentsOn);
        errors.ShouldBe([
            SqlParseError.Create(
                SqlParseErrorKind.UnknownGeneratorParameter,
                TextSpans.Of(Line, "nope", last: true),
                "nope"
            ),
            SqlParseError.Create(
                SqlParseErrorKind.UnknownGeneratorParameter,
                TextSpans.Of(Line, "foo=bar", last: true),
                "foo=bar"
            ),
        ]);
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
}
