using System;
using System.Linq;
using Microsoft.CodeAnalysis.Text;
using Shouldly;
using SqlSource.Parsing;
using SqlSource.Parsing.Tokens;
using SqlSource.Settings;
using Xunit;
using static SqlSource.Tests.Parsing.ParseOf;

namespace SqlSource.Tests.Parsing;

// The parser's blocks: names and their shape, the preamble, summaries, errors as a whole, line endings and equality.
public sealed class SqlFileParserTests
{
    [Fact]
    public void Parse_FileWithoutNameMarker_IsOneBlockNamedAfterTheFile()
    {
        var block = Blocks("SELECT 1;\n", "GetUser.sql").ShouldHaveSingleItem();

        block.Name.ShouldBe("GetUser");
        block.NameSpan.ShouldBe(new TextSpan(0, 0));
        block.Summary.ShouldBeNull();
        block.Markers.ShouldBeSameAs(SettingsLevel.None);
        block.KeptSegments.ShouldBeNull();
        block.KeptErrors.ShouldBeEmpty();
        block.Segments.ShouldBe([new SqlSegment(SqlSegmentKind.Literal, "SELECT 1;")]);
    }

    [Fact]
    public void Parse_FileWithoutNameMarker_ReadsSummaryAndGeneratorParametersBeforeAndAmongItsSql()
    {
        const string Text =
            "-- summary: First.\nSELECT 1 -- c\n-- generator: keep-comments no-token-validation\n"
            + "-- summary: Second.\nFROM t\n";

        var block = Blocks(Text).ShouldHaveSingleItem();

        block.Summary.ShouldBe("First. Second.");
        block.Markers.Parameters.ShouldBe(GeneratorParameters.KeepComments | GeneratorParameters.NoTokenValidation);
        Sql(block).ShouldBe("SELECT 1\nFROM t");
        Kept(block).ShouldBe("SELECT 1 -- c\nFROM t");
    }

    [Theory]
    [InlineData("Query.sql", "Query")]
    [InlineData("Query.SQL", "Query")]
    [InlineData("Query", "Query")]
    [InlineData("_q1.sql", "_q1")]
    [InlineData("where.sql", "where")]
    public void Parse_FileWithoutNameMarker_TakesTheNameUpToTheLastDot(string fileName, string expected) =>
        Blocks("SELECT 1", fileName).ShouldHaveSingleItem().Name.ShouldBe(expected);

    [Theory]
    [InlineData("get-user.sql")]
    [InlineData("001_init.sql")]
    [InlineData("class.sql")]
    [InlineData("a.b.sql")]
    [InlineData(".sql")]
    [InlineData("")]
    public void Parse_FileWithoutNameMarkerAndUnusableFileName_IsAnError(string fileName)
    {
        Errors("SELECT 1", fileName)
            .ShouldBe([SqlParseError.Create(SqlParseErrorKind.InvalidFileName, new TextSpan(0, 0), fileName)]);
    }

    [Fact]
    public void Parse_FileWithNameMarkers_IgnoresTheFileName() =>
        Blocks("-- name: A\nSELECT 1", "001-not-a-name.sql").ShouldHaveSingleItem().Name.ShouldBe("A");

    [Fact]
    public void Parse_NameMarkers_SplitTheFileIntoBlocks()
    {
        const string Text = "-- name: GetUser\nSELECT 1;\n\n  -- name: ListUsers\n  SELECT 2;\n";

        var blocks = Blocks(Text);

        blocks.Select(static block => block.Name).ShouldBe(["GetUser", "ListUsers"]);
        blocks.Select(Sql).ShouldBe(["SELECT 1;", "  SELECT 2;"]);
        blocks[0].NameSpan.ShouldBe(TextSpans.Of(Text, "GetUser"));
        blocks[1].NameSpan.ShouldBe(TextSpans.Of(Text, "ListUsers"));
    }

    [Theory]
    [InlineData("-- name: a\nSELECT 1\n-- name: A\nSELECT 2", "a", "A")]
    [InlineData("-- name: where\nSELECT 1\n-- name: Größe\nSELECT 2", "where", "Größe")]
    public void Parse_UnusualButValidNames_AreAccepted(string text, string first, string second) =>
        Blocks(text).Select(static block => block.Name).ShouldBe([first, second]);

    [Fact]
    public void Parse_Preamble_DiscardsItsCommentsEvenWhenPreserving()
    {
        const string Text =
            "-- Copyright\n/* header */\n-- generator: keep-comments\n\n-- name: A\n-- kept\nSELECT 1\n";

        var block = Blocks(Text).ShouldHaveSingleItem();

        block.Markers.Parameters.ShouldBe(GeneratorParameters.KeepComments);
        Sql(block).ShouldBe("SELECT 1");
        Kept(block).ShouldBe("-- kept\nSELECT 1");
    }

    [Theory]
    [InlineData("SELECT 0;\nSELECT 9;\n-- name: A\nSELECT 1", "SELECT 0;\nSELECT 9;")]
    [InlineData("  'x' y\n/*+ h */\n-- name: A\nSELECT 1", "'x'")]
    [InlineData("-- c\n/*+ h */ z\n-- name: A\nSELECT 1", "/*+ h */")]
    public void Parse_SqlInThePreamble_IsReportedOnceAtItsStart(string text, string content) =>
        Errors(text)
            .ShouldBe([SqlParseError.Create(SqlParseErrorKind.SqlBeforeFirstName, TextSpans.Of(text, content))]);

    [Fact]
    public void Parse_SummaryInThePreamble_IsAnError()
    {
        const string Text = "-- summary: nope\n-- name: A\nSELECT 1";

        Errors(Text)
            .ShouldBe([
                SqlParseError.Create(SqlParseErrorKind.SummaryBeforeFirstName, TextSpans.Of(Text, "-- summary: nope")),
            ]);
    }

    [Theory]
    [InlineData("1abc")]
    [InlineData("get-user")]
    [InlineData("class")]
    [InlineData("A B")]
    [InlineData("@class")]
    [InlineData("A -- note")]
    [InlineData("😀")]
    [InlineData("A\u200B")]
    public void Parse_UnusableName_IsAnErrorAtTheName(string name)
    {
        var text = $"-- name: {name}\nSELECT 1";

        Errors(text).ShouldBe([SqlParseError.Create(SqlParseErrorKind.InvalidName, TextSpans.Of(text, name), name)]);
    }

    [Fact]
    public void Parse_NameMarkerWithoutAName_IsAnErrorAtTheMarker()
    {
        const string Text = "-- name:  \nSELECT 1";

        Errors(Text)
            .ShouldBe([
                SqlParseError.Create(SqlParseErrorKind.InvalidName, TextSpans.Of(Text, "-- name:  "), string.Empty),
            ]);
    }

    [Fact]
    public void Parse_RepeatedName_IsAnErrorAtTheSecondOccurrence()
    {
        const string Text = "-- name: A\nSELECT 1\n-- name: A\nSELECT 2";

        Errors(Text)
            .ShouldBe([
                SqlParseError.Create(SqlParseErrorKind.DuplicateName, new TextSpan(Text.LastIndexOf('A'), 1), "A"),
            ]);
    }

    [Theory]
    [InlineData("-- name: A\n-- name: B\nSELECT 1")]
    [InlineData("-- name: A\n-- c\n/* d */\n\n-- name: B\nSELECT 1")]
    [InlineData("-- name: A\n-- generator: keep-comments\n-- c\n-- name: B\nSELECT 1")]
    [InlineData("-- name: B\nSELECT 1\n-- name: A")]
    [InlineData("-- name: B\nSELECT 1\n-- name: A\n-- summary: s\n")]
    // A marker whose error comes from the query's parameters: no query is built for a block without SQL.
    [InlineData("-- name: A\n-- param: @a\n-- name: B\nSELECT 1")]
    [InlineData("-- name: A\n-- input-model: FindArgs\n-- name: B\nSELECT 1")]
    public void Parse_BlockWithoutSql_IsAnErrorAtItsName(string text)
    {
        var errors = Errors(text);

        errors.ShouldBe([SqlParseError.Create(SqlParseErrorKind.EmptyBlock, TextSpans.Of(text, "A"))]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  \r\n\t")]
    [InlineData("-- only a comment\n/* and another */")]
    [InlineData("-- generator: keep-comments\n-- c")]
    public void Parse_FileWithoutNameMarkerOrSql_IsAnErrorAtTheStart(string text) =>
        Errors(text).ShouldBe([SqlParseError.Create(SqlParseErrorKind.EmptyBlock, new TextSpan(0, 0))]);

    [Fact]
    public void Parse_BlockHoldingOnlyAHint_IsNotEmpty() =>
        Sql(Blocks("-- name: A\n/*+ H */").ShouldHaveSingleItem()).ShouldBe("/*+ H */");

    [Theory]
    [InlineData("-- name: A\nSELECT 1\n\n-- summary: Loads B.\n-- name: B\nSELECT 2", "-- summary: Loads B.")]
    [InlineData("-- name: A\nSELECT 1\n  -- token-ignore: x\n-- name: B\nSELECT 2", "-- token-ignore: x")]
    [InlineData("-- name: A\nSELECT 1\n-- summary: last", "-- summary: last")]
    [InlineData("SELECT 1\n-- generator: keep-comments\n", "-- generator: keep-comments")]
    [InlineData("-- name: A\nSELECT 1\n-- summary: s\n-- a comment\n/* another */\n", "-- summary: s")]
    [InlineData("-- name: A\nSELECT 1\n-- generator: bogus", "-- generator: bogus")]
    [InlineData("-- name: A\nSELECT 1\n-- summary:", "-- summary:")]
    public void Parse_MarkerAfterTheLastSqlOfItsBlock_IsAnErrorAtTheMarker(string text, string marker) =>
        Errors(text).ShouldBe([SqlParseError.Create(SqlParseErrorKind.MarkerAtEndOfBlock, TextSpans.Of(text, marker))]);

    [Fact]
    public void Parse_SeveralMarkersAfterTheLastSqlOfABlock_AreEachAnError()
    {
        const string Text =
            "-- name: A\nSELECT 1\n-- generator: no-token-validation\n-- summary: s\n-- name: B\nSELECT 2";

        Errors(Text)
            .ShouldBe([
                SqlParseError.Create(
                    SqlParseErrorKind.MarkerAtEndOfBlock,
                    TextSpans.Of(Text, "-- generator: no-token-validation")
                ),
                SqlParseError.Create(SqlParseErrorKind.MarkerAtEndOfBlock, TextSpans.Of(Text, "-- summary: s")),
            ]);
    }

    [Theory]
    [InlineData("-- name: A\nSELECT 1\n-- summary: s\n-- generator: no-token-validation\nFROM t", "SELECT 1\nFROM t")]
    [InlineData("-- name: A\n-- summary: s\n-- generator: no-token-validation\n/*+ H */", "/*+ H */")]
    [InlineData("-- name: A\n-- summary: s\n-- generator: no-token-validation\n'x'\n-- c", "'x'")]
    public void Parse_MarkerWithSqlAfterItInItsBlock_IsAccepted(string text, string expectedSql)
    {
        var block = Blocks(text).ShouldHaveSingleItem();

        block.Summary.ShouldBe("s");
        block.Markers.Parameters.ShouldBe(GeneratorParameters.NoTokenValidation);
        Sql(block).ShouldBe(expectedSql);
    }

    [Fact]
    public void Parse_SummaryMarkers_AreJoinedWithOneSpace()
    {
        const string Text = "-- name: A\n-- summary: One.\n-- summary:\n-- summary:   Two.  \nSELECT 1\n";

        var block = Blocks(Text).ShouldHaveSingleItem();

        block.Summary.ShouldBe("One. Two.");
        Sql(block).ShouldBe("SELECT 1");
    }

    [Fact]
    public void Parse_BlockWithOnlyEmptySummaryMarkers_HasNoSummary() =>
        Blocks("-- name: A\n-- summary:\nSELECT 1").ShouldHaveSingleItem().Summary.ShouldBeNull();

    [Theory]
    [InlineData("/*\n-- name: A\n*/\nSELECT 1", "SELECT 1")]
    [InlineData("SELECT 1 -- name: A", "SELECT 1")]
    [InlineData("SELECT '\n-- name: A\n'", "SELECT '\n-- name: A\n'")]
    [InlineData("-- name : A\nSELECT 1", "SELECT 1")]
    public void Parse_NameMarkerLookAlike_DoesNotStartABlock(string text, string expectedSql)
    {
        var block = Blocks(text).ShouldHaveSingleItem();

        block.Name.ShouldBe("Query");
        Sql(block).ShouldBe(expectedSql);
    }

    [Theory]
    [InlineData("-- name: 1x\nSELECT 'abc", nameof(SqlParseErrorKind.UnterminatedQuote))]
    [InlineData("-- name: 1x\nSELECT /* abc", nameof(SqlParseErrorKind.UnterminatedBlockComment))]
    public void Parse_LexerError_IsTheOnlyErrorReported(string text, string kind) =>
        Errors(text).ShouldHaveSingleItem().Kind.ToString().ShouldBe(kind);

    [Fact]
    public void Parse_SeveralErrors_AreAllReportedInFileOrderAndNoBlocksAreReturned()
    {
        const string Text =
            "-- name: 1x\nSELECT 1\n"
            + "-- name: B\n-- generator: bogus\n"
            + "-- name: C\nSELECT {{class}}\n"
            + "-- name: C\nSELECT 3\n";

        var errors = Errors(Text);

        errors
            .Select(static error => error.Kind)
            .ShouldBe([
                SqlParseErrorKind.InvalidName,
                SqlParseErrorKind.EmptyBlock,
                SqlParseErrorKind.UnknownGeneratorParameter,
                SqlParseErrorKind.ReservedTokenName,
                SqlParseErrorKind.DuplicateName,
            ]);
        errors.Select(static error => error.Span.Start).ShouldBeInOrder();
    }

    [Fact]
    public void Parse_MarkerWithUnusableName_StillStartsABlock()
    {
        Errors("-- name: 1x\nSELECT 1\n-- name: B\nSELECT 2")
            .ShouldHaveSingleItem()
            .Kind.ShouldBe(SqlParseErrorKind.InvalidName);
    }

    [Fact]
    public void Parse_WindowsLineEndings_GiveTheSameBlocksAsUnixLineEndings()
    {
        const string Unix =
            "-- generator: no-token-validation\n\n-- name: A\n-- summary: S\nSELECT 1 -- c\nFROM {{t}}\n\n"
            + "-- name: B\nSELECT 'x\ny'\n";
        var windows = Unix.Replace("\n", "\r\n", StringComparison.Ordinal);

        var unixBlocks = Blocks(Unix);
        var windowsBlocks = Blocks(windows);

        windowsBlocks.Select(Sql).ShouldBe(["SELECT 1\nFROM {{t}}", "SELECT 'x\ny'"]);
        windowsBlocks.Select(Sql).ShouldBe(unixBlocks.Select(Sql));
        windowsBlocks.Select(static block => block.Summary).ShouldBe(["S", null]);
        windowsBlocks
            .Select(static block => block.Markers.Parameters)
            .ShouldBe([GeneratorParameters.NoTokenValidation, GeneratorParameters.NoTokenValidation]);
    }

    [Theory]
    [InlineData("-- name: A\n-- summary: s\nSELECT {{a}} -- c\n-- name: B\nSELECT 2\n")]
    [InlineData("-- name: 1x\n-- generator: bogus\nSELECT {{class}}\n")]
    [InlineData("SELECT 'abc")]
    public void Parse_SameTextTwice_GivesEqualResults(string text)
    {
        var first = SqlFileParser.Parse(text, "Query.sql");
        var second = SqlFileParser.Parse(new string(text.ToCharArray()), "Query.sql");

        second.ShouldNotBeSameAs(first);
        second.ShouldBe(first);
        second.GetHashCode().ShouldBe(first.GetHashCode());
    }

    [Fact]
    public void Parse_DifferentText_GivesUnequalResults() =>
        SqlFileParser.Parse("SELECT 1", "Query.sql").ShouldNotBe(SqlFileParser.Parse("SELECT 2", "Query.sql"));

    [Theory]
    [InlineData("-- name: A", "A", null)]
    [InlineData("-- name: A -> many", "A", "Many")]
    [InlineData("-- name: A->one", "A", "One")]
    [InlineData("-- name: A  ->  One-Optional  ", "A", "OneOptional")]
    [InlineData("-- name: A -> rowcount", "A", "RowCount")]
    [InlineData("-- name: A -> none", "A", "None")]
    public void Parse_NameMarker_ReadsTheNameAndTheShape(string marker, string name, string? shape)
    {
        var text = marker + "\nSELECT 1\n";

        var block = Blocks(text).ShouldHaveSingleItem();

        block.Name.ShouldBe(name);
        block.NameSpan.ShouldBe(TextSpans.Of(text, name));
        block.Shape.ShouldBe(shape is null ? null : Enum.Parse<ResultShape>(shape));
    }

    [Theory]
    [InlineData("A ->", "->")]
    [InlineData("A -> several", "-> several")]
    [InlineData("A -> one -> many", "-> one -> many")]
    [InlineData("A -> one, many", "-> one, many")]
    public void Parse_NameMarkerWithAShapeThatIsNotOne_IsInvalidAtWhatFollowsTheName(string value, string at)
    {
        var text = "-- name: " + value + "\nSELECT 1\n";

        Errors(text)
            .ShouldBe([
                SqlParseError.Create(
                    SqlParseErrorKind.InvalidMarkerValue,
                    TextSpans.Of(text, at),
                    "name: " + value,
                    ""
                ),
            ]);
    }

    [Fact]
    public void Parse_NameMarkerWithAShapeAndNoName_IsAnInvalidName()
    {
        const string Text = "-- name: -> one\nSELECT 1\n";

        Errors(Text)
            .ShouldBe([
                SqlParseError.Create(
                    SqlParseErrorKind.InvalidName,
                    TextSpans.Of(Text, "-- name: -> one"),
                    string.Empty
                ),
            ]);
    }

    [Fact]
    public void Parse_FileWithoutNameMarker_HasNoShape() =>
        Blocks("SELECT 1\n").ShouldHaveSingleItem().Shape.ShouldBeNull();
}
