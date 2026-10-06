using System.Text;
using Shouldly;
using SqlSource.Generation;
using Xunit;

namespace SqlSource.Tests.Generation;

public class XmlDocWriterTests
{
    [Theory]
    [InlineData("SELECT 1", "SELECT 1")]
    [InlineData("", "")]
    [InlineData("a < b", "a &lt; b")]
    [InlineData("a > b && c", "a &gt; b &amp;&amp; c")]
    [InlineData("<&>", "&lt;&amp;&gt;")]
    [InlineData("x &amp; y", "x &amp;amp; y")]
    [InlineData("'quotes' \"stay\"", "'quotes' \"stay\"")]
    public void Escape_Text_ReplacesTheCharactersXmlReserves(string text, string expected) =>
        XmlDocWriter.Escape(text).ShouldBe(expected);

    [Theory]
    [InlineData("a\0b")]
    [InlineData("a\u0001b")]
    [InlineData("a\vb")]
    [InlineData("a\fb")]
    [InlineData("a\u001Ab")]
    [InlineData("a\uFFFEb")]
    [InlineData("a\uFFFFb")]
    public void Escape_CharacterThatXmlCannotHold_BecomesTheReplacementCharacter(string text) =>
        XmlDocWriter.Escape(text).ShouldBe("a\uFFFDb");

    // A string with half of a surrogate pair cannot be written as an attribute argument: the compiler stores those as
    // UTF-8, which has no form for it.
    [Theory]
    [InlineData(0xD800)]
    [InlineData(0xDBFF)]
    [InlineData(0xDC00)]
    [InlineData(0xDFFF)]
    public void Escape_SurrogateWithoutItsPair_BecomesTheReplacementCharacter(int surrogate)
    {
        var half = (char)surrogate;

        XmlDocWriter.Escape("a" + half + "b").ShouldBe("a\uFFFDb");
        XmlDocWriter.Escape("a" + half).ShouldBe("a\uFFFD");
        XmlDocWriter.Escape(half + "\uD83D\uDE00").ShouldBe("\uFFFD\uD83D\uDE00");
    }

    [Theory]
    [InlineData("a\tb")]
    [InlineData("a\uD83D\uDE00b")]
    [InlineData("a\uFFFDb")]
    [InlineData("a\u0085\u2028b")]
    public void Escape_CharacterThatXmlCanHold_IsKept(string text) => XmlDocWriter.Escape(text).ShouldBeSameAs(text);

    [Fact]
    public void Escape_TextWithNothingToEscape_IsTheSameString()
    {
        const string Text = "SELECT id FROM users";

        XmlDocWriter.Escape(Text).ShouldBeSameAs(Text);
    }

    [Fact]
    public void AppendMember_SummaryAndSql_WritesOneCommentLineForEachLine()
    {
        var builder = new StringBuilder("before\n");

        XmlDocWriter.AppendMember(builder, "    ", "Loads <c>one</c> user.", "SELECT id\nFROM users\nWHERE id < @max;");

        builder
            .ToString()
            .ShouldBe(
                """
                before
                    /// <summary>
                    /// Loads <c>one</c> user.
                    /// </summary>
                    /// <remarks>
                    /// <code>
                    /// SELECT id
                    /// FROM users
                    /// WHERE id &lt; @max;
                    /// </code>
                    /// </remarks>

                """
            );
    }

    [Fact]
    public void AppendParam_Name_WritesOneLineThatShowsTheToken()
    {
        var builder = new StringBuilder("before\n");

        XmlDocWriter.AppendParam(builder, "    ", "table");

        builder
            .ToString()
            .ShouldBe("before\n    /// <param name=\"table\">The text that replaces <c>{{table}}</c>.</param>\n");
    }

    [Fact]
    public void AppendMember_EmptyLine_HasNoTrailingSpace()
    {
        var builder = new StringBuilder();

        XmlDocWriter.AppendMember(builder, "", "S", "SELECT '\n\n';");

        builder.ToString().ShouldContain("/// SELECT '\n///\n/// ';\n");
    }

    [Theory]
    [InlineData("\n")]
    [InlineData("\r")]
    [InlineData("\r\n")]
    [InlineData("\u0085")]
    [InlineData("\u2028")]
    [InlineData("\u2029")]
    public void AppendMember_AnyLineTerminatorOfCSharp_StartsANewCommentLine(string terminator)
    {
        var builder = new StringBuilder();

        XmlDocWriter.AppendMember(builder, "  ", "a" + terminator + "b", "SELECT '" + terminator + "';");

        builder
            .ToString()
            .ShouldBe(
                "  /// <summary>\n  /// a\n  /// b\n  /// </summary>\n  /// <remarks>\n  /// <code>\n"
                    + "  /// SELECT '\n  /// ';\n  /// </code>\n  /// </remarks>\n"
            );
    }

    [Fact]
    public void AppendMember_TextEndingInALineTerminator_EndsWithAnEmptyCommentLine()
    {
        var builder = new StringBuilder();

        XmlDocWriter.AppendMember(builder, "", "S", "SELECT 1\n");

        builder.ToString().ShouldContain("/// <code>\n/// SELECT 1\n///\n/// </code>\n");
    }
}
