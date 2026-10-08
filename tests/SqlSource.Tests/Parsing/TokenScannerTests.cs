using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis.Text;
using Shouldly;
using SqlSource.Parsing;
using Xunit;

namespace SqlSource.Tests.Parsing;

public class TokenScannerTests
{
    [Fact]
    public void Scan_EmptySql_ReturnsNoSegments() => Scan(string.Empty).ShouldBeEmpty();

    [Fact]
    public void Scan_SqlWithoutTokens_ReturnsOneLiteral() => Scan("SELECT 1").ShouldBe(["L:SELECT 1"]);

    [Fact]
    public void Scan_TokenInTheMiddle_SplitsTheSql() =>
        Scan("SELECT * FROM {{table}} WHERE x").ShouldBe(["L:SELECT * FROM ", "T:table", "L: WHERE x"]);

    [Theory]
    [InlineData("{{a}}", new[] { "T:a" })]
    [InlineData("{{a}} x {{b}}", new[] { "T:a", "L: x ", "T:b" })]
    [InlineData("{{a}}{{b}}", new[] { "T:a", "T:b" })]
    public void Scan_TokenAtAnEdge_AddsNoEmptyLiteral(string sql, string[] expected) => Scan(sql).ShouldBe(expected);

    [Fact]
    public void Scan_RepeatedToken_RecordsEachOccurrenceInOrder() =>
        Scan("{{a}} {{b}} {{a}}").ShouldBe(["T:a", "L: ", "T:b", "L: ", "T:a"]);

    [Theory]
    [InlineData("{{ a }}")]
    [InlineData("{{\ta\t}}")]
    [InlineData("{{a  }}")]
    [InlineData("{{  a}}")]
    public void Scan_BlanksInsideTheBraces_AreIgnored(string sql) => Scan(sql).ShouldBe(["T:a"]);

    [Theory]
    [InlineData("{{1,2},{3,4}}")]
    [InlineData("{{table name}}")]
    [InlineData("{{}}")]
    [InlineData("{{ }}")]
    [InlineData("{{1abc}}")]
    [InlineData("{{a.b}}")]
    [InlineData("{{a-b}}")]
    [InlineData("{{\na}}")]
    [InlineData("{{a\n}}")]
    [InlineData("{name}")]
    [InlineData("{ {name}}")]
    [InlineData("{{name}")]
    [InlineData("{{name} }")]
    [InlineData("{{😀}}")]
    [InlineData("{{a\u200B}}")]
    public void Scan_TextThatIsNotExactlyAToken_IsLiteral(string sql) => Scan(sql).ShouldBe(["L:" + sql]);

    [Fact]
    public void Scan_TokenInsideExtraBraces_IsStillAToken() => Scan("{{{name}}}").ShouldBe(["L:{", "T:name", "L:}"]);

    [Fact]
    public void Scan_NamesThatDifferByCase_AreDifferentTokens() =>
        Scan("{{Table}} {{table}}").ShouldBe(["T:Table", "L: ", "T:table"]);

    [Theory]
    [InlineData("{{where}}", "T:where")]
    [InlineData("{{var}}", "T:var")]
    [InlineData("{{_x1}}", "T:_x1")]
    [InlineData("{{größe}}", "T:größe")]
    public void Scan_ContextualKeywordOrUnusualIdentifier_IsAToken(string sql, string expected) =>
        Scan(sql).ShouldBe([expected]);

    [Fact]
    public void Scan_IgnoredToken_StaysLiteralExactlyAsWritten() =>
        Scan("a {{ skip }} b {{keep}}", "skip").ShouldBe(["L:a {{ skip }} b ", "T:keep"]);

    [Fact]
    public void Scan_IgnoredName_IsCaseSensitive() => Scan("{{Skip}}", "skip").ShouldBe(["T:Skip"]);

    [Fact]
    public void Scan_ReservedKeywordToken_IsAnError()
    {
        var result = TokenScanner.Scan("SELECT {{ class }} x", new HashSet<string>());

        result.Errors.ShouldBe([
            SqlParseError.Create(SqlParseErrorKind.ReservedTokenName, new TextSpan(7, 11), "class"),
        ]);
    }

    [Fact]
    public void Scan_ReservedKeywordTokenThatIsIgnored_IsLiteral() =>
        Scan("SELECT {{class}}", "class").ShouldBe(["L:SELECT {{class}}"]);

    [Theory]
    [InlineData("x {{")]
    [InlineData("x {{a")]
    [InlineData("x {{a}")]
    [InlineData("x {{ ")]
    [InlineData("{")]
    public void Scan_SqlEndingInsideAToken_IsLiteral(string sql) => Scan(sql).ShouldBe(["L:" + sql]);

    [Theory]
    [InlineData("{{a:b}}", "a=b")]
    [InlineData("{{ a : b c }}", "a=b c")]
    [InlineData("{{a:}}", "a=")]
    [InlineData("{{a:  }}", "a=")]
    [InlineData("{{cast:x::int}}", "cast=x::int")]
    [InlineData("{{a:x\n  AND y}}", "a=x\n  AND y")]
    [InlineData("{{a:{b} }}", "a={b}")]
    [InlineData("{{a}}", "a=<none>")]
    public void Scan_TokenWithADefault_ReadsTheNameAndTheTrimmedDefault(string sql, string expected)
    {
        Defaults(sql).ShouldBe([expected]);
        Scan(sql).ShouldBe(["T:" + expected[..expected.IndexOf('=', StringComparison.Ordinal)]]);
    }

    [Theory]
    [InlineData("{{a:b}")]
    [InlineData("{{a:b")]
    [InlineData("{{a b:c}}")]
    [InlineData("{{1a:c}}")]
    [InlineData("{{:c}}")]
    public void Scan_TextThatOnlyLooksLikeATokenWithADefault_IsLiteral(string sql) => Scan(sql).ShouldBe(["L:" + sql]);

    [Fact]
    public void Scan_Occurrences_HaveTheirPlaceInTheSql()
    {
        const string Sql = "a {{x}} b {{y:1}} c";

        var occurrences = TokenScanner.Scan(Sql, new HashSet<string>()).Occurrences;

        occurrences.Select(token => Sql.Substring(token.Span.Start, token.Span.Length)).ShouldBe(["{{x}}", "{{y:1}}"]);
    }

    [Fact]
    public void Scan_IgnoredTokenWithADefault_StaysLiteralWithItsDefault()
    {
        Scan("x {{raw:@a}} y", "raw").ShouldBe(["L:x {{raw:@a}} y"]);
        Defaults("x {{raw:@a}} y", "raw").ShouldBeEmpty();
    }

    // A keyword is a keyword with a default too.
    [Fact]
    public void Scan_ReservedNameWithADefault_IsAnError()
    {
        var error = TokenScanner.Scan("{{default:x}}", new HashSet<string>()).Errors.ShouldHaveSingleItem();

        error.Kind.ShouldBe(SqlParseErrorKind.ReservedTokenName);
        error.Span.ShouldBe(new TextSpan(0, 13));
        error.Arguments.ShouldBe(["default"]);
    }

    // Pins the result for openers that have no "}}" after them: each is literal text, however many there are, and
    // whatever the name after the opener is.
    [Theory]
    [InlineData("{{a: {{b: {{c:")]
    [InlineData("x {{a: y {{b: z {{a: w")]
    [InlineData("{{1a: {{b: {{")]
    public void Scan_UnclosedOpenersWithDefaults_AreAllLiteral(string sql)
    {
        Scan(sql).ShouldBe(["L:" + sql]);
        Defaults(sql).ShouldBeEmpty();
    }

    // Pins the rule that a default runs to the first "}}", even over another opener: the first token is `a` with a
    // default that holds the openers after it, and the closed token inside it is not a token of its own.
    [Fact]
    public void Scan_UnclosedOpenersBeforeAClosedToken_TakeTheFirstOpenersDefaultToTheFirstClose()
    {
        Defaults("{{a: {{b: {{c:x}}").ShouldBe(["a={{b: {{c:x"]);
        Scan("{{a: {{b: {{c:x}}").ShouldBe(["T:a"]);
    }

    // Pins that an opener whose name is not an identifier stays literal, and does not hide a closed token after it.
    [Fact]
    public void Scan_OpenerWithABadNameBeforeAClosedToken_StaysLiteral()
    {
        Scan("{{1a: {{b:x}}").ShouldBe(["L:{{1a: ", "T:b"]);
        Defaults("{{1a: {{b:x}}").ShouldBe(["b=x"]);
    }

    private static string[] Defaults(string sql, params string[] ignoredNames) =>
        [
            .. TokenScanner
                .Scan(sql, new HashSet<string>(ignoredNames))
                .Occurrences.Select(token => token.Name + "=" + (token.Default ?? "<none>")),
        ];

    private static string[] Scan(string sql, params string[] ignoredNames)
    {
        var result = TokenScanner.Scan(sql, new HashSet<string>(ignoredNames));
        result.Errors.ShouldBeEmpty();
        return
        [
            .. result.Segments.Select(segment => (segment.Kind == SqlSegmentKind.Token ? "T:" : "L:") + segment.Text),
        ];
    }
}
