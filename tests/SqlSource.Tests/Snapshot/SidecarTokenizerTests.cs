using System.Collections.Generic;
using Microsoft.CodeAnalysis.Text;
using Shouldly;
using SqlSource.Snapshot;
using Xunit;

namespace SqlSource.Tests.Snapshot;

public class SidecarTokenizerTests
{
    [Fact]
    public void Next_EachToken_HasItsKindAndItsSpan() =>
        Tokens("{ } [ ] : , \"a\" 12 true false null")
            .ShouldBe([
                "ObjectStart:{",
                "ObjectEnd:}",
                "ArrayStart:[",
                "ArrayEnd:]",
                "Colon::",
                "Comma:,",
                "String:\"a\"",
                "Number:12",
                "True:true",
                "False:false",
                "Null:null",
            ]);

    [Fact]
    public void Next_WhiteSpaceOfJson_IsSkipped() => Tokens(" \t\r\n1 \t\r\n").ShouldBe(["Number:1"]);

    [Fact]
    public void Next_EndOfText_IsAnEmptySpanAtTheEnd()
    {
        var tokens = new SidecarTokenizer("1 ");
        _ = tokens.Next();

        tokens.Next().ShouldBe(new SidecarToken(SidecarTokenKind.End, new TextSpan(2, 0)));
        tokens.Next().ShouldBe(new SidecarToken(SidecarTokenKind.End, new TextSpan(2, 0)));
    }

    [Theory]
    // A byte order mark, a no-break space and a form feed are not white space in JSON.
    [InlineData("\uFEFF{}", 0, 1)]
    [InlineData("\u00A0", 0, 1)]
    [InlineData("\f", 0, 1)]
    // A comment, a single-quoted string, a bare word.
    [InlineData("/* c */", 0, 1)]
    [InlineData("'a'", 0, 1)]
    [InlineData("tru", 0, 1)]
    [InlineData("nul", 0, 1)]
    [InlineData("False", 0, 1)]
    // A string: an escape JSON lacks, bad hex, a control character, and one left open.
    [InlineData("\"a\\qb\"", 3, 1)]
    [InlineData("\"\\u12G4\"", 5, 1)]
    [InlineData("\"a\tb\"", 2, 1)]
    [InlineData("\"a\nb\"", 2, 1)]
    [InlineData("\"abc", 4, 0)]
    [InlineData("\"abc\\", 5, 0)]
    [InlineData("\"\\u12", 5, 0)]
    // A number: no digit, a leading zero, a fraction or an exponent without digits, a sign JSON lacks.
    [InlineData("-", 1, 0)]
    [InlineData("-x", 1, 1)]
    [InlineData("01", 1, 1)]
    [InlineData("1.", 2, 0)]
    [InlineData("1.x", 2, 1)]
    [InlineData("1e", 2, 0)]
    [InlineData("1e+", 3, 0)]
    [InlineData(".5", 0, 1)]
    [InlineData("+1", 0, 1)]
    public void Next_TextThatIsNotJson_IsInvalidAtTheCharacter(string text, int start, int length)
    {
        var tokens = new SidecarTokenizer(text);

        tokens.Next().ShouldBe(new SidecarToken(SidecarTokenKind.Invalid, new TextSpan(start, length)));
        tokens.Next().Kind.ShouldBe(SidecarTokenKind.End);
    }

    [Theory]
    [InlineData("\"\"", "")]
    [InlineData("\"abc\"", "abc")]
    [InlineData("\"größe / 日本 😀\"", "größe / 日本 😀")]
    public void GetString_StringWithoutAnEscape_IsPlainAndIsItsText(string json, string expected)
    {
        var token = new SidecarTokenizer(json).Next();

        token.ShouldBe(new SidecarToken(SidecarTokenKind.String, new TextSpan(0, json.Length), true));
        SidecarTokenizer.GetString(json, token).ShouldBe(expected);
    }

    [Theory]
    [InlineData("\"\\\"\"", "\"")]
    [InlineData("\"\\\\\"", "\\")]
    [InlineData("\"\\/\"", "/")]
    [InlineData("\"\\b\"", "\b")]
    [InlineData("\"\\f\"", "\f")]
    [InlineData("\"\\n\"", "\n")]
    [InlineData("\"\\r\"", "\r")]
    [InlineData("\"\\t\"", "\t")]
    [InlineData("\"\\u0041\"", "A")]
    [InlineData("\"\\u00e9\\u00E9\"", "éé")]
    [InlineData("\"\\u0000\"", "\0")]
    // A surrogate pair.
    [InlineData("\"\\uD83D\\uDE00\"", "😀")]
    [InlineData("\"a\\tb\\nc\"", "a\tb\nc")]
    [InlineData("\"\\u0041bc\\\\\"", "Abc\\")]
    public void GetString_StringWithAnEscape_IsNotPlainAndIsDecoded(string json, string expected)
    {
        var token = new SidecarTokenizer(json).Next();

        token.ShouldBe(new SidecarToken(SidecarTokenKind.String, new TextSpan(0, json.Length)));
        SidecarTokenizer.GetString(json, token).ShouldBe(expected);
    }

    // JSON allows half a surrogate pair alone.  An attribute cannot hold one, so it is built here.
    [Fact]
    public void GetString_EscapeOfOneSurrogateAlone_IsThatCharacter()
    {
        const string Json = "\"\\uD83D\"";

        SidecarTokenizer.GetString(Json, new SidecarTokenizer(Json).Next()).ShouldBe(((char)0xD83D).ToString());
    }

    [Theory]
    [InlineData("\"name\"", "name", true)]
    [InlineData("\"name\"", "nam", false)]
    [InlineData("\"name\"", "names", false)]
    [InlineData("\"Name\"", "name", false)]
    [InlineData("\"\"", "", true)]
    // A key written with an escape is the same key.
    [InlineData("\"na\\u006de\"", "name", true)]
    [InlineData("\"na\\u006de\"", "nome", false)]
    public void StringEquals_String_ComparesItsDecodedText(string json, string value, bool expected) =>
        SidecarTokenizer.StringEquals(json, new SidecarTokenizer(json).Next(), value).ShouldBe(expected);

    [Theory]
    [InlineData("0", true)]
    [InlineData("-0", true)]
    [InlineData("12", true)]
    [InlineData("-12", true)]
    [InlineData("1.5", false)]
    [InlineData("-0.0", false)]
    [InlineData("1e5", false)]
    [InlineData("1E+5", false)]
    [InlineData("1.5e-3", false)]
    public void Next_Number_IsPlainWhenItHasNoFractionAndNoExponent(string json, bool plain) =>
        new SidecarTokenizer(json)
            .Next()
            .ShouldBe(new SidecarToken(SidecarTokenKind.Number, new TextSpan(0, json.Length), plain));

    [Theory]
    [InlineData("0", 0)]
    [InlineData("-0", 0)]
    [InlineData("7", 7)]
    [InlineData("-1", -1)]
    [InlineData("2147483647", int.MaxValue)]
    [InlineData("-2147483648", int.MinValue)]
    public void TryGetInt32_IntegerOfThirtyTwoBits_IsRead(string json, int expected)
    {
        SidecarTokenizer.TryGetInt32(json, new SidecarTokenizer(json).Next(), out var value).ShouldBeTrue();

        value.ShouldBe(expected);
    }

    [Theory]
    [InlineData("2147483648")]
    [InlineData("-2147483649")]
    [InlineData("99999999999999999999999999")]
    [InlineData("1.0")]
    [InlineData("1e2")]
    [InlineData("\"1\"")]
    [InlineData("true")]
    public void TryGetInt32_AnythingElse_IsNotRead(string json) =>
        SidecarTokenizer.TryGetInt32(json, new SidecarTokenizer(json).Next(), out _).ShouldBeFalse();

    [Theory]
    [InlineData("1")]
    [InlineData("\"a\"")]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("{ }")]
    [InlineData("[1, 2.5, -3e10, \"x\", true, false, null]")]
    [InlineData( /*lang=json,strict*/
        "{\"a\": 1, \"b\": {\"c\": [[], {}]}, \"d\": \"}]\"}"
    )]
    public void TrySkipValue_Value_EndsAfterIt(string json)
    {
        var text = json + " 7";
        var tokens = new SidecarTokenizer(text);

        tokens.TrySkipValue(tokens.Next(), 0, out _).ShouldBeTrue();

        tokens.Position.ShouldBe(json.Length);
        tokens.Next().ShouldBe(new SidecarToken(SidecarTokenKind.Number, new TextSpan(json.Length + 1, 1), true));
    }

    [Theory]
    [InlineData("{\"a\" 1}", 5, 1)]
    [InlineData("{\"a\":}", 5, 1)]
    [InlineData( /*lang=json*/
        "{\"a\":1,}",
        7,
        1
    )]
    [InlineData("{\"a\":1 \"b\":2}", 7, 3)]
    [InlineData("{,}", 1, 1)]
    [InlineData( /*lang=json*/
        "{1:2}",
        1,
        1
    )]
    [InlineData("{\"a\":1", 6, 0)]
    [InlineData("{\"a\"", 4, 0)]
    [InlineData("{", 1, 0)]
    [InlineData("[1,]", 3, 1)]
    [InlineData("[1 2]", 3, 1)]
    [InlineData("[,1]", 1, 1)]
    [InlineData("[1", 2, 0)]
    [InlineData("[1, // c\n2]", 4, 1)]
    [InlineData("[\"a\\q\"]", 4, 1)]
    [InlineData("}", 0, 1)]
    [InlineData(":", 0, 1)]
    [InlineData("", 0, 0)]
    public void TrySkipValue_BrokenGrammar_FailsAtTheToken(string json, int start, int length)
    {
        var tokens = new SidecarTokenizer(json);

        tokens.TrySkipValue(tokens.Next(), 0, out var error).ShouldBeFalse();

        error.ShouldBe(new TextSpan(start, length));
    }

    [Theory]
    [InlineData('[', ']', "")]
    [InlineData('{', '}', "\"a\":")]
    public void TrySkipValue_SixtyFourLevels_IsRead(char open, char close, string key)
    {
        var json = Nested(open, close, key, SidecarTokenizer.MaxDepth);
        var tokens = new SidecarTokenizer(json);

        tokens.TrySkipValue(tokens.Next(), 0, out _).ShouldBeTrue();

        tokens.Position.ShouldBe(json.Length);
    }

    [Theory]
    [InlineData('[', ']', "")]
    [InlineData('{', '}', "\"a\":")]
    public void TrySkipValue_SixtyFiveLevels_FailsAtTheBracketThatOpensTheLast(char open, char close, string key)
    {
        var json = Nested(open, close, key, SidecarTokenizer.MaxDepth + 1);
        var tokens = new SidecarTokenizer(json);

        tokens.TrySkipValue(tokens.Next(), 0, out var error).ShouldBeFalse();

        error.ShouldBe(new TextSpan(SidecarTokenizer.MaxDepth * (1 + key.Length), 1));
    }

    // The depth an outer reader has already opened counts.
    [Fact]
    public void TrySkipValue_DepthOfTheCaller_Counts()
    {
        var json = Nested('[', ']', string.Empty, 2);
        var tokens = new SidecarTokenizer(json);

        tokens.TrySkipValue(tokens.Next(), SidecarTokenizer.MaxDepth - 1, out var error).ShouldBeFalse();

        error.ShouldBe(new TextSpan(1, 1));
    }

    // The limit is what keeps such a file from overflowing the stack.
    [Fact]
    public void TrySkipValue_HundredThousandOpenBrackets_FailsWithoutOverflow()
    {
        var json = new string('[', 100_000);
        var tokens = new SidecarTokenizer(json);

        tokens.TrySkipValue(tokens.Next(), 0, out var error).ShouldBeFalse();

        error.ShouldBe(new TextSpan(SidecarTokenizer.MaxDepth, 1));
    }

    [Fact]
    public void Tokenizer_StartedAtAnOffset_ReadsFromThere()
    {
        var tokens = new SidecarTokenizer("[1, 22]", 3);

        tokens.Next().ShouldBe(new SidecarToken(SidecarTokenKind.Number, new TextSpan(4, 2), true));
    }

    // `levels` containers, each inside the one before: [[[]]] or {"a":{"a":{}}}.
    private static string Nested(char open, char close, string key, int levels)
    {
        var text = new System.Text.StringBuilder();
        for (var level = 0; level < levels; level++)
        {
            _ = text.Append(open);
            if (level < levels - 1)
            {
                _ = text.Append(key);
            }
        }

        return text.Append(close, levels).ToString();
    }

    private static List<string> Tokens(string text)
    {
        var tokens = new SidecarTokenizer(text);
        var found = new List<string>();
        for (var token = tokens.Next(); token.Kind != SidecarTokenKind.End; token = tokens.Next())
        {
            found.Add(token.Kind + ":" + text.Substring(token.Span.Start, token.Span.Length));
        }

        return found;
    }
}
