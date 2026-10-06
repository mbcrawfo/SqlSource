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
        var scope = new SqlDirectiveScope(headerEnd: 0);

        scope.KeepComments.ShouldBeFalse();
        scope.TokenValidation.ShouldBeNull();
        scope.Dialect.ShouldBeNull();
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

    [Theory]
    [InlineData("dialect=mysql", nameof(SqlDialect.MySql))]
    [InlineData("DIALECT=Postgres", nameof(SqlDialect.PostgreSql))]
    [InlineData("Dialect=TSQL", nameof(SqlDialect.SqlServer))]
    [InlineData("dialect=ansi", nameof(SqlDialect.Ansi))]
    public void Read_Dialect_KeepsTheDialectItNames(string directive, string expected)
    {
        var (scope, errors) = Read("-- SqlSource: " + directive);

        errors.ShouldBeEmpty();
        scope.Dialect.ToString().ShouldBe(expected);
    }

    [Theory]
    [InlineData("dialect")]
    [InlineData("dialect=")]
    [InlineData("dialect=pgsql")]
    [InlineData("dialect=mysql,postgres")]
    [InlineData("dialect=mysql=x")]
    public void Read_DialectWithoutAValueOrWithAnUnknownName_IsAnError(string directive)
    {
        var line = "-- SqlSource: " + directive;

        var (scope, errors) = Read(line);

        errors.ShouldBe([
            SqlParseError.Create(SqlParseErrorKind.InvalidDirectiveValue, SpanOf(line, directive), directive),
        ]);
        scope.Dialect.ShouldBeNull();
    }

    [Fact]
    public void Read_SameDialectTwice_IsAllowed()
    {
        var (scope, errors) = Read("-- SqlSource: dialect=mssql dialect=tsql", "-- SqlSource: dialect=SqlServer");

        errors.ShouldBeEmpty();
        scope.Dialect.ShouldBe(SqlDialect.SqlServer);
    }

    [Fact]
    public void Read_TwoDialectsOnOneLine_ReportsTheSecondAndKeepsTheFirst()
    {
        const string Line = "-- SqlSource: dialect=mysql dialect=oracle";

        var (scope, errors) = Read(Line);

        errors.ShouldBe([
            SqlParseError.Create(
                SqlParseErrorKind.ConflictingDirectives,
                SpanOf(Line, "dialect=oracle"),
                "dialect=oracle"
            ),
        ]);
        scope.Dialect.ShouldBe(SqlDialect.MySql);
    }

    [Fact]
    public void Read_TwoDialectsOnSeparateLines_IsAnError()
    {
        var (scope, errors) = Read("-- SqlSource: dialect=mysql", "-- SqlSource: dialect=mariadb");

        errors.ShouldHaveSingleItem().Kind.ShouldBe(SqlParseErrorKind.ConflictingDirectives);
        scope.Dialect.ShouldBe(SqlDialect.MySql);
    }

    // The directive starts at offset 14 of the line.
    [Theory]
    [InlineData(0)]
    [InlineData(14)]
    public void Read_DialectAtOrAfterTheHeaderEnd_IsMisplacedWhateverItNames(int headerEnd)
    {
        var (scope, errors) = Read(headerEnd, "-- SqlSource: dialect=mysql keep-comments", "-- SqlSource: dialect=x");

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
        var (scope, errors) = Read(15, "-- SqlSource: dialect=mysql");

        errors.ShouldBeEmpty();
        scope.Dialect.ShouldBe(SqlDialect.MySql);
    }

    [Theory]
    [InlineData("-- SqlSource: dialect=mysql", nameof(SqlDialect.MySql))]
    [InlineData("-- SqlSource: keep-comments DIALECT=Oracle token-ignore=a", nameof(SqlDialect.Oracle))]
    [InlineData("-- SqlSource: dialect=nope dialect= dialect dialect=sqlite", nameof(SqlDialect.Sqlite))]
    [InlineData("-- SqlSource: dialect=mysql dialect=oracle", nameof(SqlDialect.MySql))]
    public void TryFindDialect_MarkerWithADialect_GivesTheFirstThatIsValid(string line, string expected)
    {
        SqlDirectiveScope.TryFindDialect(line, Marker(line), out var dialect).ShouldBeTrue();

        dialect.ToString().ShouldBe(expected);
    }

    [Theory]
    [InlineData("-- SqlSource: keep-comments")]
    [InlineData("-- SqlSource:")]
    [InlineData("-- SqlSource: dialect")]
    [InlineData("-- SqlSource: dialect=")]
    [InlineData("-- SqlSource: dialect=pgsql")]
    [InlineData("-- SqlSource: xdialect=mysql")]
    [InlineData("-- SqlSource: dialects=mysql")]
    [InlineData("-- SqlSource: dialect:mysql")]
    [InlineData("-- SqlSource: dialect = mysql")]
    public void TryFindDialect_MarkerWithoutAValidDialect_FindsNone(string line)
    {
        SqlDirectiveScope.TryFindDialect(line, Marker(line), out var dialect).ShouldBeFalse();

        dialect.ShouldBe(SqlDialect.Ansi);
    }

    private static (SqlDirectiveScope Scope, List<SqlParseError> Errors) Read(params string[] lines) =>
        Read(int.MaxValue, lines);

    // Each line is lexed alone, so every directive is at the offset it has in its own line.
    private static (SqlDirectiveScope Scope, List<SqlParseError> Errors) Read(int headerEnd, params string[] lines)
    {
        var scope = new SqlDirectiveScope(headerEnd);
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
