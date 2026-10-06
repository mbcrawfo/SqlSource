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
