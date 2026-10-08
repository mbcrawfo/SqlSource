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
        var scope = new SqlGeneratorParameterScope(headerEnd: 0);

        scope.KeepComments.ShouldBeFalse();
        scope.TokenValidation.ShouldBeNull();
        scope.Dialect.ShouldBeNull();
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

    [Theory]
    [InlineData("dialect=mysql", nameof(SqlDialect.MySql))]
    [InlineData("DIALECT=Postgres", nameof(SqlDialect.PostgreSql))]
    [InlineData("Dialect=TSQL", nameof(SqlDialect.SqlServer))]
    [InlineData("dialect=ansi", nameof(SqlDialect.Ansi))]
    public void Read_Dialect_KeepsTheDialectItNames(string parameter, string expected)
    {
        var (scope, errors) = Read("-- generator: " + parameter);

        errors.ShouldBeEmpty();
        scope.Dialect.ShouldBe(Plain(Enum.Parse<SqlDialect>(expected)));
    }

    [Theory]
    [InlineData("dialect")]
    [InlineData("dialect=")]
    [InlineData("dialect=pgsql")]
    [InlineData("dialect=mysql,postgres")]
    [InlineData("dialect=mysql=x")]
    [InlineData("dialect=mysql,")]
    [InlineData("dialect=mysql,nope")]
    [InlineData("dialect=postgres,ansi-quotes")]
    public void Read_DialectWithoutAValueOrWithAnUnknownName_IsAnError(string parameter)
    {
        var line = "-- generator: " + parameter;

        var (scope, errors) = Read(line);

        errors.ShouldBe([
            SqlParseError.Create(SqlParseErrorKind.InvalidMarkerValue, SpanOf(line, parameter), parameter),
        ]);
        scope.Dialect.ShouldBeNull();
    }

    [Fact]
    public void Read_SameDialectTwice_IsAllowed()
    {
        var (scope, errors) = Read("-- generator: dialect=mssql dialect=tsql", "-- generator: dialect=SqlServer");

        errors.ShouldBeEmpty();
        scope.Dialect.ShouldBe(Plain(SqlDialect.SqlServer));
    }

    [Fact]
    public void Read_DialectWithOptions_KeepsTheDialectAndItsOptions()
    {
        var (scope, errors) = Read("-- generator: keep-comments DIALECT=MySql,ANSI_QUOTES");

        errors.ShouldBeEmpty();
        scope.Dialect.ShouldBe(new SqlDialectChoice(SqlDialect.MySql, SqlDialectOptions.AnsiQuotes));
    }

    [Fact]
    public void Read_SameDialectAndOptionsTwice_IsAllowed()
    {
        var (scope, errors) = Read(
            "-- generator: dialect=mysql,ansi-quotes,no-backslash-escapes",
            "-- generator: dialect=MySQL,NO_BACKSLASH_ESCAPES,ANSI_QUOTES"
        );

        errors.ShouldBeEmpty();
        scope.Dialect.ShouldBe(
            new SqlDialectChoice(SqlDialect.MySql, SqlDialectOptions.AnsiQuotes | SqlDialectOptions.NoBackslashEscapes)
        );
    }

    [Fact]
    public void Read_SameDialectWithOtherOptions_ReportsTheSecondAndKeepsTheFirst()
    {
        const string Line = "-- generator: dialect=mysql dialect=mysql,ansi-quotes";

        var (scope, errors) = Read(Line);

        errors.ShouldBe([
            SqlParseError.Create(
                SqlParseErrorKind.ConflictingSettings,
                SpanOf(Line, "dialect=mysql,ansi-quotes"),
                "dialect=mysql,ansi-quotes"
            ),
        ]);
        scope.Dialect.ShouldBe(Plain(SqlDialect.MySql));
    }

    // A generator parameter is one word.  A space after the comma ends it, and the option is read as a generator
    // parameter of its own.
    [Fact]
    public void Read_SpaceAfterTheCommaOfADialect_IsTwoErrors()
    {
        const string Line = "-- generator: dialect=mysql, ansi-quotes";

        var (scope, errors) = Read(Line);

        errors.ShouldBe([
            SqlParseError.Create(
                SqlParseErrorKind.InvalidMarkerValue,
                SpanOf(Line, "dialect=mysql,"),
                "dialect=mysql,"
            ),
            SqlParseError.Create(
                SqlParseErrorKind.UnknownGeneratorParameter,
                SpanOf(Line, "ansi-quotes"),
                "ansi-quotes"
            ),
        ]);
        scope.Dialect.ShouldBeNull();
    }

    [Fact]
    public void TryFindDialect_MarkerWithOptions_GivesTheFirstValueThatIsValid()
    {
        const string Line = "-- generator: dialect=mysql, dialect=mariadb,no-backslash-escapes dialect=mysql";

        SqlGeneratorParameterScope.TryFindDialect(Line, Marker(Line), out var dialect).ShouldBeTrue();

        dialect.ShouldBe(new SqlDialectChoice(SqlDialect.MariaDb, SqlDialectOptions.NoBackslashEscapes));
    }

    [Fact]
    public void Read_TwoDialectsOnOneLine_ReportsTheSecondAndKeepsTheFirst()
    {
        const string Line = "-- generator: dialect=mysql dialect=oracle";

        var (scope, errors) = Read(Line);

        errors.ShouldBe([
            SqlParseError.Create(
                SqlParseErrorKind.ConflictingSettings,
                SpanOf(Line, "dialect=oracle"),
                "dialect=oracle"
            ),
        ]);
        scope.Dialect.ShouldBe(Plain(SqlDialect.MySql));
    }

    [Fact]
    public void Read_TwoDialectsOnSeparateLines_IsAnError()
    {
        var (scope, errors) = Read("-- generator: dialect=mysql", "-- generator: dialect=mariadb");

        errors.ShouldHaveSingleItem().Kind.ShouldBe(SqlParseErrorKind.ConflictingSettings);
        scope.Dialect.ShouldBe(Plain(SqlDialect.MySql));
    }

    // The generator parameter starts at offset 14 of the line.
    [Theory]
    [InlineData(0)]
    [InlineData(14)]
    public void Read_DialectAtOrAfterTheHeaderEnd_IsMisplacedWhateverItNames(int headerEnd)
    {
        var (scope, errors) = Read(headerEnd, "-- generator: dialect=mysql keep-comments", "-- generator: dialect=x");

        errors.ShouldBe([
            SqlParseError.Create(SqlParseErrorKind.MisplacedDialect, new TextSpan(14, 13), "dialect=mysql"),
            SqlParseError.Create(SqlParseErrorKind.MisplacedDialect, new TextSpan(14, 9), "dialect=x"),
        ]);
        scope.Dialect.ShouldBeNull();
        scope.KeepComments.ShouldBeTrue();
    }

    [Fact]
    public void Read_DialectJustBeforeTheHeaderEnd_IsAccepted()
    {
        var (scope, errors) = Read(15, "-- generator: dialect=mysql");

        errors.ShouldBeEmpty();
        scope.Dialect.ShouldBe(Plain(SqlDialect.MySql));
    }

    [Theory]
    [InlineData("-- generator: dialect=mysql", nameof(SqlDialect.MySql))]
    [InlineData("-- generator: keep-comments DIALECT=Oracle token-ignore=a", nameof(SqlDialect.Oracle))]
    [InlineData("-- generator: dialect=nope dialect= dialect dialect=sqlite", nameof(SqlDialect.Sqlite))]
    [InlineData("-- generator: dialect=mysql dialect=oracle", nameof(SqlDialect.MySql))]
    public void TryFindDialect_MarkerWithADialect_GivesTheFirstThatIsValid(string line, string expected)
    {
        SqlGeneratorParameterScope.TryFindDialect(line, Marker(line), out var dialect).ShouldBeTrue();

        dialect.ShouldBe(Plain(Enum.Parse<SqlDialect>(expected)));
    }

    [Theory]
    [InlineData("-- generator: keep-comments")]
    [InlineData("-- generator:")]
    [InlineData("-- generator: dialect")]
    [InlineData("-- generator: dialect=")]
    [InlineData("-- generator: dialect=pgsql")]
    [InlineData("-- generator: xdialect=mysql")]
    [InlineData("-- generator: dialects=mysql")]
    [InlineData("-- generator: dialect:mysql")]
    [InlineData("-- generator: dialect = mysql")]
    public void TryFindDialect_MarkerWithoutAValidDialect_FindsNone(string line)
    {
        SqlGeneratorParameterScope.TryFindDialect(line, Marker(line), out var dialect).ShouldBeFalse();

        dialect.ShouldBe(default);
    }

    [Fact]
    public void ReportMisplacedDialects_Marker_ReportsEachDialectGeneratorParameterWhateverItNamesAndNothingElse()
    {
        const string Line = "-- generator: keep-comments dialect=mysql bogus DIALECT dialect=nope dialects=x";
        var errors = new List<SqlParseError>();

        SqlGeneratorParameterScope.ReportMisplacedDialects(Line, Marker(Line), errors);

        errors.ShouldBe([
            SqlParseError.Create(SqlParseErrorKind.MisplacedDialect, SpanOf(Line, "dialect=mysql"), "dialect=mysql"),
            SqlParseError.Create(SqlParseErrorKind.MisplacedDialect, SpanOf(Line, "DIALECT"), "DIALECT"),
            SqlParseError.Create(SqlParseErrorKind.MisplacedDialect, SpanOf(Line, "dialect=nope"), "dialect=nope"),
        ]);
    }

    private static (SqlGeneratorParameterScope Scope, List<SqlParseError> Errors) Read(params string[] lines) =>
        Read(int.MaxValue, lines);

    // Each line is lexed alone, so every generator parameter is at the offset it has in its own line.
    private static (SqlGeneratorParameterScope Scope, List<SqlParseError> Errors) Read(
        int headerEnd,
        params string[] lines
    )
    {
        var scope = new SqlGeneratorParameterScope(headerEnd);
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

    private static SqlDialectChoice Plain(SqlDialect dialect) => new(dialect, SqlDialectOptions.None);
}
