using Shouldly;
using SqlSource.Parsing.Quoting;
using Xunit;

namespace SqlSource.Tests.Parsing.Quoting;

public class BracketReaderTests
{
    private static readonly BracketReader SqlServer = new(doubledCloserEscapes: true);

    private static readonly BracketReader Sqlite = new(doubledCloserEscapes: false);

    [Theory]
    [InlineData("[a] x", "[a]")]
    [InlineData("x.[order details] y", "[order details]")]
    [InlineData("[] x", "[]")]
    [InlineData("[a'b] x", "[a'b]")]
    [InlineData("[a--b] x", "[a--b]")]
    [InlineData("[a/*b] x", "[a/*b]")]
    [InlineData("[a[b] x", "[a[b]")]
    [InlineData("[a\nb] x", "[a\nb]")]
    public void FindEnd_ClosedIdentifier_EndsAfterItsBracket(string text, string expected)
    {
        Region.Read(SqlServer, text, '[').ShouldBe(expected);
        Region.Read(Sqlite, text, '[').ShouldBe(expected);
    }

    [Theory]
    [InlineData("[a]]b] x", "[a]]b]")]
    [InlineData("[a]]] x", "[a]]]")]
    [InlineData("[]]] x", "[]]]")]
    [InlineData("[a]]]]b] x", "[a]]]]b]")]
    public void FindEnd_DoubledCloserInSqlServer_StandsForOneBracket(string text, string expected) =>
        Region.Read(SqlServer, text, '[').ShouldBe(expected);

    [Theory]
    [InlineData("[a]]b] x", "[a]")]
    [InlineData("[a]]] x", "[a]")]
    public void FindEnd_DoubledCloserInSqlite_EndsAtTheFirst(string text, string expected) =>
        Region.Read(Sqlite, text, '[').ShouldBe(expected);

    [Theory]
    [InlineData("[abc")]
    [InlineData("[")]
    public void FindEnd_IdentifierWithoutAClosingBracket_IsUnterminated(string text)
    {
        Region.Read(SqlServer, text, '[').ShouldBe(Region.Unterminated);
        Region.Read(Sqlite, text, '[').ShouldBe(Region.Unterminated);
    }

    [Theory]
    [InlineData("[abc]]")]
    [InlineData("[abc]]def")]
    public void FindEnd_IdentifierThatEndsInAnEscapedBracketInSqlServer_IsUnterminated(string text) =>
        Region.Read(SqlServer, text, '[').ShouldBe(Region.Unterminated);
}
