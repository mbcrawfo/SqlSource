using System;
using System.Linq;
using Microsoft.CodeAnalysis.Text;
using Shouldly;
using SqlSource.Parsing;
using SqlSource.Settings;
using Xunit;

namespace SqlSource.Tests.Parsing;

public class SqlFileParserTests
{
    [Theory]
    [InlineData("SELECT 1;\n", "MySql", "MySql")]
    [InlineData("-- dialect: mssql\nSELECT 1;\n", "MySql", "SqlServer")]
    [InlineData("-- dialect: nope\nSELECT 'open\n", "Oracle", "Oracle")]
    public void Parse_Result_NamesTheDialectTheFileWasReadBy(string text, string given, string expected) =>
        SqlFileParser
            .Parse(text, "Query.sql", new SqlDialectChoice(Enum.Parse<SqlDialect>(given), SqlDialectOptions.None))
            .Dialect.ShouldBe(Enum.Parse<SqlDialect>(expected));

    [Fact]
    public void Parse_FileWithoutNameMarker_IsOneBlockNamedAfterTheFile()
    {
        var block = Blocks("SELECT 1;\n", "GetUser.sql").ShouldHaveSingleItem();

        block.Name.ShouldBe("GetUser");
        block.NameSpan.ShouldBe(new TextSpan(0, 0));
        block.Summary.ShouldBeNull();
        block.Markers.ShouldBeSameAs(SettingsLevel.None);
        block.KeptSegments.ShouldBeNull();
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
        blocks[0].NameSpan.ShouldBe(SpanOf(Text, "GetUser"));
        blocks[1].NameSpan.ShouldBe(SpanOf(Text, "ListUsers"));
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

    [Fact]
    public void Parse_PreambleGeneratorParameters_ApplyToEveryBlock()
    {
        const string Text = "-- generator: no-token-validation\n" + "-- name: A\nSELECT 1\n" + "-- name: B\nSELECT 2\n";

        var blocks = Blocks(Text);

        blocks[0].Markers.Parameters.ShouldBe(GeneratorParameters.NoTokenValidation);
        blocks[1].Markers.Parameters.ShouldBe(GeneratorParameters.NoTokenValidation);
    }

    [Theory]
    // The query's own list decides, whatever the file's input says.
    [InlineData("-- name: Q\n-- generator: keep-comments\nSELECT 1 -- c\n", false, "SELECT 1 -- c")]
    [InlineData("-- name: Q\n-- generator: sort-input\nSELECT 1 -- c\n", true, null)]
    [InlineData("-- name: Q\n-- generator: default\nSELECT 1 -- c\n", true, null)]
    // The preamble's list, for a query without one.
    [InlineData("-- generator: keep-comments\n-- name: Q\nSELECT 1 -- c\n", false, "SELECT 1 -- c")]
    [InlineData("-- generator: keep-comments\n-- name: Q\n-- generator: default\nSELECT 1 -- c\n", true, null)]
    [InlineData("-- generator: sort-input\n-- name: Q\nSELECT 1 -- c\n", true, null)]
    // No list at all: the file's input.
    [InlineData("-- name: Q\nSELECT 1 -- c\n", true, "SELECT 1 -- c")]
    [InlineData("-- name: Q\nSELECT 1 -- c\n", false, null)]
    // Nothing to keep: one form serves.
    [InlineData("-- name: Q\nSELECT 1\n", true, null)]
    public void Parse_KeptForm_IsBuiltOnlyWhenItMayBeWanted(string text, bool commentsWanted, string? expected)
    {
        var block = Block(text, commentsWanted);

        Sql(block).ShouldBe("SELECT 1");
        Kept(block).ShouldBe(expected);
    }

    [Fact]
    public void Parse_TokenThatStandsOnlyInAComment_IsATokenOfTheKeptFormAlone()
    {
        var block = Block("-- name: Q\n-- generator: keep-comments\nSELECT 1 /* {{note}} */ FROM {{t:users}}\n", false);

        Sql(block).ShouldBe("SELECT 1   FROM {{t}}");
        Kept(block).ShouldBe("SELECT 1 /* {{note}} */ FROM {{t}}");
        Tokens(block).ShouldBe(["t=users"]);
    }

    [Theory]
    [InlineData("SELECT {{class}} -- c\n", "{{class}}")]
    [InlineData("SELECT 1 -- {{class}}\n", "{{class}}")]
    public void Parse_ReservedTokenInEitherForm_IsReportedOnce(string sql, string at)
    {
        var text = "-- name: Q\n-- generator: keep-comments\n" + sql;

        Errors(text).ShouldBe([SqlParseError.Create(SqlParseErrorKind.ReservedTokenName, SpanOf(text, at), "class")]);
    }

    [Fact]
    public void Parse_QueryWithoutMarkers_SharesTheEmptyLevel() =>
        Blocks("-- name: Q\nSELECT 1\n").ShouldHaveSingleItem().Markers.ShouldBeSameAs(SettingsLevel.None);

    [Theory]
    [InlineData("-- generator: keep-comments\n-- name: Q\nSELECT 1\n", 1)]
    [InlineData("-- generator: keep-comments\n-- name: Q\n-- generator: sort-input\nSELECT 1\n", 4)]
    [InlineData("-- generator: keep-comments\n-- name: Q\n-- generator: default\nSELECT 1\n", 0)]
    public void Parse_Markers_HoldTheQuerysListOverThePreambles(string text, int expected) =>
        ((int?)Blocks(text).ShouldHaveSingleItem().Markers.Parameters).ShouldBe(expected);

    [Fact]
    public void Parse_BlockGeneratorParameter_DoesNotLeakIntoTheNextBlock()
    {
        const string Text =
            "-- name: A\n-- generator: keep-comments no-token-validation\n-- token-ignore: x\nSELECT {{x}} -- c\n"
            + "-- name: B\nSELECT {{x}} -- c\n";

        var blocks = Blocks(Text);

        blocks[1].Markers.ShouldBeSameAs(SettingsLevel.None);
        Sql(blocks[1]).ShouldBe("SELECT {{x}}");
        blocks[1].Segments[1].ShouldBe(new SqlSegment(SqlSegmentKind.Token, "x"));
    }

    [Theory]
    [InlineData("SELECT 0;\nSELECT 9;\n-- name: A\nSELECT 1", "SELECT 0;\nSELECT 9;")]
    [InlineData("  'x' y\n/*+ h */\n-- name: A\nSELECT 1", "'x'")]
    [InlineData("-- c\n/*+ h */ z\n-- name: A\nSELECT 1", "/*+ h */")]
    public void Parse_SqlInThePreamble_IsReportedOnceAtItsStart(string text, string content) =>
        Errors(text).ShouldBe([SqlParseError.Create(SqlParseErrorKind.SqlBeforeFirstName, SpanOf(text, content))]);

    [Fact]
    public void Parse_SummaryInThePreamble_IsAnError()
    {
        const string Text = "-- summary: nope\n-- name: A\nSELECT 1";

        Errors(Text)
            .ShouldBe([
                SqlParseError.Create(SqlParseErrorKind.SummaryBeforeFirstName, SpanOf(Text, "-- summary: nope")),
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

        Errors(text).ShouldBe([SqlParseError.Create(SqlParseErrorKind.InvalidName, SpanOf(text, name), name)]);
    }

    [Fact]
    public void Parse_NameMarkerWithoutAName_IsAnErrorAtTheMarker()
    {
        const string Text = "-- name:  \nSELECT 1";

        Errors(Text)
            .ShouldBe([SqlParseError.Create(SqlParseErrorKind.InvalidName, SpanOf(Text, "-- name:  "), string.Empty)]);
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
    public void Parse_BlockWithoutSql_IsAnErrorAtItsName(string text)
    {
        var errors = Errors(text);

        errors.ShouldBe([SqlParseError.Create(SqlParseErrorKind.EmptyBlock, SpanOf(text, "A"))]);
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
        Errors(text).ShouldBe([SqlParseError.Create(SqlParseErrorKind.MarkerAtEndOfBlock, SpanOf(text, marker))]);

    [Fact]
    public void Parse_SeveralMarkersAfterTheLastSqlOfABlock_AreEachAnError()
    {
        const string Text =
            "-- name: A\nSELECT 1\n-- generator: no-token-validation\n-- summary: s\n-- name: B\nSELECT 2";

        Errors(Text)
            .ShouldBe([
                SqlParseError.Create(
                    SqlParseErrorKind.MarkerAtEndOfBlock,
                    SpanOf(Text, "-- generator: no-token-validation")
                ),
                SqlParseError.Create(SqlParseErrorKind.MarkerAtEndOfBlock, SpanOf(Text, "-- summary: s")),
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

    [Fact]
    public void Parse_Tokens_BecomeSegmentsInOrder()
    {
        const string Text = "-- name: A\nSELECT * FROM {{table}} WHERE {{ col }} = 1 AND {{table}}.x = 2";

        Blocks(Text)
            .ShouldHaveSingleItem()
            .Segments.ShouldBe([
                new SqlSegment(SqlSegmentKind.Literal, "SELECT * FROM "),
                new SqlSegment(SqlSegmentKind.Token, "table"),
                new SqlSegment(SqlSegmentKind.Literal, " WHERE "),
                new SqlSegment(SqlSegmentKind.Token, "col"),
                new SqlSegment(SqlSegmentKind.Literal, " = 1 AND "),
                new SqlSegment(SqlSegmentKind.Token, "table"),
                new SqlSegment(SqlSegmentKind.Literal, ".x = 2"),
            ]);
    }

    [Fact]
    public void Parse_TokenInAStrippedComment_IsNotAToken()
    {
        const string Text = "-- name: A\nSELECT 1 -- {{x}}\n/* {{y}} */\n";

        Blocks(Text).ShouldHaveSingleItem().Segments.ShouldBe([new SqlSegment(SqlSegmentKind.Literal, "SELECT 1")]);
    }

    [Fact]
    public void Parse_TokenInAStringLiteralOrAPreservedComment_IsAToken()
    {
        const string Text = "-- name: A\n-- generator: keep-comments\nSELECT '{{a}}' -- {{b}}";

        var block = Blocks(Text).ShouldHaveSingleItem();

        TokenNames(block.Segments).ShouldBe(["a"]);
        TokenNames(block.KeptSegments.ShouldNotBeNull()).ShouldBe(["a", "b"]);
    }

    [Fact]
    public void Parse_ReservedKeywordToken_IsAnErrorAtTheTokenInTheFile()
    {
        const string Text = "-- name: A\r\n/* c */ SELECT 1 -- x\r\n\r\n-- summary: s\r\n  FROM {{ class }}";

        Errors(Text)
            .ShouldBe([
                SqlParseError.Create(SqlParseErrorKind.ReservedTokenName, SpanOf(Text, "{{ class }}"), "class"),
            ]);
    }

    [Fact]
    public void Parse_ReservedKeywordTokenAroundAStrippedComment_IsReportedOverTheWholeToken()
    {
        const string Token = "{{ /* c */ class }}";
        const string Text = "-- name: A\nSELECT " + Token;

        Errors(Text)
            .ShouldBe([SqlParseError.Create(SqlParseErrorKind.ReservedTokenName, SpanOf(Text, Token), "class")]);
    }

    [Fact]
    public void Parse_ReservedKeywordTokenThatIsIgnored_IsLiteral()
    {
        const string Text = "-- name: A\n-- token-ignore: class\nSELECT {{class}}";

        Sql(Blocks(Text).ShouldHaveSingleItem()).ShouldBe("SELECT {{class}}");
    }

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
    [InlineData("-- generator: keep-comment", nameof(SqlParseErrorKind.UnknownGeneratorParameter), "keep-comment")]
    [InlineData("-- generator:", nameof(SqlParseErrorKind.EmptyGeneratorLine), "-- generator:")]
    [InlineData("-- generator: keep-comments=", nameof(SqlParseErrorKind.InvalidMarkerValue), "keep-comments=")]
    [InlineData(
        "-- generator: default no-token-validation",
        nameof(SqlParseErrorKind.ConflictingSettings),
        "no-token-validation"
    )]
    public void Parse_GeneratorParameterProblem_IsReportedAtItsPlaceInTheFile(string line, string kind, string place)
    {
        var text = "-- name: A\n" + line + "\nSELECT 1";

        var error = Errors(text).ShouldHaveSingleItem();

        error.Kind.ToString().ShouldBe(kind);
        error.Span.ShouldBe(SpanOf(text, place));
    }

    // A comment that has the form of a marker is one, whatever its author meant by it.
    [Fact]
    public void Parse_OrdinaryCommentThatStartsWithGenerator_IsReadAsGeneratorParameters()
    {
        const string Text = "-- generator: pgloader\n-- name: A\nSELECT 1\n";

        Errors(Text)
            .ShouldBe([
                SqlParseError.Create(SqlParseErrorKind.UnknownGeneratorParameter, SpanOf(Text, "pgloader"), "pgloader"),
            ]);
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

    // ANSI ends the string at the second quote, takes the rest of the line for a comment, and would keep the #.
    [Theory]
    [InlineData(nameof(SqlDialect.Ansi), "SELECT 'a\\'b")]
    [InlineData(nameof(SqlDialect.MySql), "SELECT 'a\\'b -- c', 2")]
    public void Parse_Dialect_DecidesHowTheSqlIsRead(string dialect, string expected)
    {
        const string Text = "-- name: A\nSELECT 'a\\'b -- c', 2 # d\n";

        Sql(Blocks(Text, dialect: Enum.Parse<SqlDialect>(dialect)).ShouldHaveSingleItem()).ShouldBe(expected);
    }

    [Fact]
    public void Parse_DialectMarkerInThePreamble_AppliesToEveryBlockAndIsNotInTheSql()
    {
        const string Text =
            "/* Copyright (c) Example */\n-- dialect: mysql\n\n"
            + "-- name: A\nSELECT 'a\\'b' # c\n-- name: B\nSELECT 5--3 # d\n";

        Blocks(Text).Select(Sql).ShouldBe(["SELECT 'a\\'b'", "SELECT 5--3"]);
    }

    [Fact]
    public void Parse_DialectMarker_ReplacesTheDialectOfTheProject()
    {
        const string Text = "-- dialect: mssql\n-- name: A\nSELECT [a'b] -- c\n";

        Sql(Blocks(Text, dialect: SqlDialect.MySql).ShouldHaveSingleItem()).ShouldBe("SELECT [a'b]");
    }

    // Under plain MySQL the backslash takes the closing quote with it, and the string is not closed.
    [Fact]
    public void Parse_DialectMarkerWithAnOption_ReadsTheFileByThatOption()
    {
        const string Text = "-- dialect: mysql,no-backslash-escapes\n-- name: A\nSELECT 'C:\\temp\\' AS path -- c\n";

        Sql(Blocks(Text).ShouldHaveSingleItem()).ShouldBe("SELECT 'C:\\temp\\' AS path");
    }

    [Fact]
    public void Parse_OptionOfTheProject_IsUsedByAFileWithoutAMarker()
    {
        const string Text = "-- name: A\nSELECT \"a\\\" AS b -- c\n";
        var project = new SqlDialectChoice(SqlDialect.MySql, SqlDialectOptions.AnsiQuotes);

        Sql(Blocks(Text, dialect: project).ShouldHaveSingleItem()).ShouldBe("SELECT \"a\\\" AS b");
    }

    // The marker names no option, so the file has none: the option of the project is not kept.
    [Fact]
    public void Parse_DialectMarkerWithoutOptions_ReplacesTheOptionsOfTheProjectToo()
    {
        const string Text = "-- dialect: mysql\n-- name: A\nSELECT 'it\\'s' -- c\n";
        var project = new SqlDialectChoice(SqlDialect.MySql, SqlDialectOptions.NoBackslashEscapes);

        Sql(Blocks(Text, dialect: project).ShouldHaveSingleItem()).ShouldBe("SELECT 'it\\'s'");
    }

    [Fact]
    public void Parse_TwoDialectMarkersThatDifferOnlyInOptions_IsAnError()
    {
        const string Text = "-- dialect: mysql\n-- dialect: mysql, ansi-quotes\n-- name: A\nSELECT 1\n";

        Errors(Text)
            .ShouldBe([
                SqlParseError.Create(
                    SqlParseErrorKind.ConflictingSettings,
                    SpanOf(Text, "mysql, ansi-quotes"),
                    "dialect: mysql, ansi-quotes"
                ),
            ]);
    }

    // The comments between the parts are removed with the rest.  The line break stays, so PostgreSQL still reads
    // one string.
    [Fact]
    public void Parse_ContinuedStringWithCommentsBetweenItsParts_StripsThemAndKeepsTheLineBreak()
    {
        const string Text =
            "-- name: A\nSELECT E'it' -- first  \n\n    -- second\n    '\\'s -- not a comment' AS note; -- c\n";

        Sql(Blocks(Text, dialect: SqlDialect.PostgreSql).ShouldHaveSingleItem())
            .ShouldBe("SELECT E'it'\n    '\\'s -- not a comment' AS note;");
    }

    [Fact]
    public void Parse_ContinuedStringUnderKeepComments_KeepsTheCommentsBetweenItsParts()
    {
        const string Text =
            "-- generator: keep-comments\n-- name: A\nSELECT E'it' -- first\n    '\\'s' AS note; -- c\n";

        Kept(Blocks(Text, dialect: SqlDialect.PostgreSql).ShouldHaveSingleItem())
            .ShouldBe("SELECT E'it' -- first\n    '\\'s' AS note; -- c");
    }

    // A marker is a marker wherever a comment can be.  Both halves are still read as PostgreSQL reads them.
    [Fact]
    public void Parse_NameMarkerBetweenThePartsOfAContinuedString_StartsABlockThere()
    {
        const string Text = "-- name: A\nSELECT E'a'\n-- name: B\n'b\\'c' AS x; -- d\n";

        Blocks(Text, dialect: SqlDialect.PostgreSql).Select(Sql).ShouldBe(["SELECT E'a'", "'b\\'c' AS x;"]);
    }

    [Fact]
    public void Parse_ContinuedStringWithAnUnclosedPart_IsAnErrorAtTheQuoteOfThatPart()
    {
        const string Text = "-- name: A\nSELECT E'a' -- c\n    'b\\' AS x;\n";

        Errors(Text, dialect: SqlDialect.PostgreSql)
            .ShouldBe([
                SqlParseError.Create(
                    SqlParseErrorKind.UnterminatedQuote,
                    new TextSpan(Text.IndexOf("'b", StringComparison.Ordinal), 1)
                ),
            ]);
    }

    [Fact]
    public void Parse_DialectMarkerInAFileWithoutANameMarker_GoesAboveItsSql()
    {
        const string Text = "-- summary: S\n-- dialect: oracle\n-- generator: keep-comments\nSELECT q'[it's]' --+ h\n";

        var block = Blocks(Text).ShouldHaveSingleItem();

        Sql(block).ShouldBe("SELECT q'[it's]' --+ h");
        block.Summary.ShouldBe("S");
        block.Markers.Parameters.ShouldBe(GeneratorParameters.KeepComments);
    }

    // The header is read under the dialect of the project, and the rest of the file under the marker's.
    [Fact]
    public void Parse_CommentAboveTheDialectMarker_IsReadUnderTheDialectOfTheProject()
    {
        const string Text = "# licence\n-- dialect: postgres\n-- name: A\nSELECT 1 # 2\n";

        Sql(Blocks(Text, dialect: SqlDialect.MySql).ShouldHaveSingleItem()).ShouldBe("SELECT 1 # 2");
    }

    // The place is checked before the value, so text that was never meant as a dialect is reported the same way.
    [Theory]
    [InlineData("-- name: A\n-- dialect: mysql\nSELECT 1\n")]
    [InlineData("-- dialect: mysql\n-- name: A\n-- dialect: mysql\nSELECT 1\n")]
    [InlineData("SELECT 1\n-- dialect: mysql\nFROM t\n")]
    [InlineData("-- name: A\nSELECT 1\n-- name: B\n-- dialect: nope\nSELECT 2\n")]
    [InlineData("-- name: A\n-- dialect: see the wiki\nSELECT 1\n")]
    [InlineData("-- name: A\n-- dialect:\nSELECT 1\n")]
    public void Parse_DialectMarkerInsideAQueryOrAfterSql_IsMisplaced(string text)
    {
        var error = Errors(text).ShouldHaveSingleItem();

        error.Kind.ShouldBe(SqlParseErrorKind.MisplacedDialect);
        error.Span.Start.ShouldBe(text.LastIndexOf("-- dialect:", StringComparison.Ordinal));
        error.Span.End.ShouldBe(text.IndexOf('\n', error.Span.Start));
        error.Arguments.Count.ShouldBe(0);
    }

    // A marker after the last SQL of its block is reported as that.  A dialect marker is reported as misplaced too,
    // so that the user learns at once that it belongs at the top of the file.
    [Theory]
    [InlineData("SELECT 1;\n-- dialect: mysql\n")]
    [InlineData("-- name: A\nSELECT 1;\n-- dialect: nope\n")]
    [InlineData("-- name: A\nSELECT 1;\n-- DIALECT:\n-- name: B\nSELECT 2;\n")]
    public void Parse_DialectMarkerAfterTheLastSqlOfItsBlock_IsMisplacedAsWellAsAtTheEndOfItsBlock(string text)
    {
        var errors = Errors(text);
        var marker = text.IndexOf("-- dialect:", StringComparison.OrdinalIgnoreCase);

        errors
            .Select(static error => error.Kind)
            .ShouldBe([SqlParseErrorKind.MarkerAtEndOfBlock, SqlParseErrorKind.MisplacedDialect]);
        errors[0].Span.Start.ShouldBe(marker);
        errors[1].Span.ShouldBe(errors[0].Span);
    }

    // A misplaced marker does not change how the file is read: the # would be a comment under MySQL.
    [Fact]
    public void Parse_MisplacedDialectMarker_IsNotApplied() =>
        Errors("-- name: A\n-- dialect: mysql\nSELECT 'it''s' # '\n")
            .ShouldHaveSingleItem()
            .Kind.ShouldBe(SqlParseErrorKind.UnterminatedQuote);

    [Fact]
    public void Parse_DialectMarkerAfterSqlInThePreamble_IsReportedWithTheSql()
    {
        const string Text = "SELECT 0;\n-- dialect: mysql\n-- name: A\nSELECT 1\n";

        Errors(Text)
            .Select(static error => error.Kind)
            .ShouldBe([SqlParseErrorKind.SqlBeforeFirstName, SqlParseErrorKind.MisplacedDialect]);
    }

    [Theory]
    [InlineData("-- dialect: pgsql\n-- name: A\nSELECT 1\n", "pgsql")]
    [InlineData("-- dialect: postgres,ansi-quotes\nSELECT 1\n", "postgres,ansi-quotes")]
    [InlineData("-- dialect: mysql,\nSELECT 1\n", "mysql,")]
    // Several words on one line were the old form.  The line is one value, and it is not a dialect.
    [InlineData("-- dialect: mysql keep-comments\nSELECT 1\n", "mysql keep-comments")]
    [InlineData("-- dialect: mysql -- legacy\nSELECT 1\n", "mysql -- legacy")]
    public void Parse_DialectMarkerWithoutAValidValue_IsAnErrorAtTheValue(string text, string value) =>
        Errors(text)
            .ShouldBe([
                SqlParseError.Create(SqlParseErrorKind.InvalidMarkerValue, SpanOf(text, value), "dialect: " + value),
            ]);

    [Fact]
    public void Parse_DialectMarkerWithoutAValue_IsAnErrorAtTheMarker()
    {
        const string Text = "-- dialect:\nSELECT 1\n";

        Errors(Text)
            .ShouldBe([
                SqlParseError.Create(SqlParseErrorKind.InvalidMarkerValue, SpanOf(Text, "-- dialect:"), "dialect:"),
            ]);
    }

    [Fact]
    public void Parse_TwoDialectsInOneHeader_IsAnErrorAtTheSecondAndTheSameDialectTwiceIsNot()
    {
        const string Conflict = "-- dialect: mysql\n-- dialect: oracle\n-- name: A\nSELECT 1\n";
        const string Repeat = "-- dialect: mysql\n-- dialect: MYSQL\n-- name: A\nSELECT 1 # c\n";

        Errors(Conflict)
            .ShouldBe([
                SqlParseError.Create(
                    SqlParseErrorKind.ConflictingSettings,
                    SpanOf(Conflict, "oracle"),
                    "dialect: oracle"
                ),
            ]);
        Sql(Blocks(Repeat).ShouldHaveSingleItem()).ShouldBe("SELECT 1");
    }

    // Two markers conflict when they name different dialects or options, not when they are written differently.
    [Theory]
    [InlineData("-- dialect: mssql\n-- dialect: tsql\n-- dialect: SqlServer\n-- name: A\nSELECT [a'b] -- c\n")]
    [InlineData(
        "-- dialect: mysql,ansi-quotes,no-backslash-escapes\n-- dialect: MySQL, NO_BACKSLASH_ESCAPES, ANSI_QUOTES\n"
            + "-- name: A\nSELECT 1 # c\n"
    )]
    public void Parse_SameDialectWrittenAnotherWay_IsNotAConflict(string text) =>
        Blocks(text).ShouldHaveSingleItem().Name.ShouldBe("A");

    // An invalid marker does not hide a conflict between the two valid ones around it.
    [Fact]
    public void Parse_InvalidDialectMarkerBetweenTwoThatDiffer_ReportsBoth()
    {
        const string Text = "-- dialect: mysql\n-- dialect: nope\n-- dialect: oracle\nSELECT 1\n";

        Errors(Text)
            .Select(static error => error.Kind)
            .ShouldBe([SqlParseErrorKind.InvalidMarkerValue, SqlParseErrorKind.ConflictingSettings]);
    }

    [Fact]
    public void Parse_DialectMarkerInAFileWithWindowsLineEndings_IsRead()
    {
        const string Text =
            "-- dialect: mysql, no-backslash-escapes  \r\n-- name: A\r\nSELECT 'C:\\temp\\' AS p # c\r\n";

        Sql(Blocks(Text).ShouldHaveSingleItem()).ShouldBe("SELECT 'C:\\temp\\' AS p");
    }

    [Fact]
    public void Parse_DialectAsAGeneratorParameter_IsNotKnownAndIsNotApplied()
    {
        const string Text = "-- generator: dialect=mysql\n-- name: A\nSELECT 1\n";

        Errors(Text)
            .ShouldBe([
                SqlParseError.Create(
                    SqlParseErrorKind.UnknownGeneratorParameter,
                    SpanOf(Text, "dialect=mysql"),
                    "dialect=mysql"
                ),
            ]);
    }

    [Fact]
    public void Parse_SameTextUnderTwoDialects_GivesUnequalResultsOnlyWhereTheyReadItDifferently()
    {
        // The result names the dialect it was read by, so only what was read is compared.
        ReadOf("SELECT 1 # c", SqlDialect.MySql).ShouldNotBe(ReadOf("SELECT 1 # c", SqlDialect.Ansi));
        ReadOf("SELECT 1 -- c", SqlDialect.MySql).ShouldBe(ReadOf("SELECT 1 -- c", SqlDialect.Ansi));
    }

    private static (EquatableArray<SqlBlock> Blocks, EquatableArray<SqlParseError> Errors) ReadOf(
        string text,
        SqlDialect dialect
    )
    {
        var result = SqlFileParser.Parse(text, "Query.sql", dialect);
        return (result.Blocks, result.Errors);
    }

    [Theory]
    [InlineData("SELECT @a, @b, @a", new[] { "a", "b" })]
    [InlineData("SELECT @Id, @ID, @id", new[] { "Id" })]
    [InlineData("SELECT '@x', @y -- @z", new[] { "y" })]
    [InlineData("SELECT @@ROWCOUNT, a @> b", new string[0])]
    public void Parse_Parameters_AreThoseOfTheSqlInOrderOfFirstAppearance(string sql, string[] expected)
    {
        var block = Blocks("-- name: Q\n" + sql + "\n").ShouldHaveSingleItem();

        block.Parameters.Select(static parameter => parameter.Name).ShouldBe(expected);
        block.Parameters.ShouldAllBe(static parameter =>
            parameter.Type == null && parameter.Nullable == null && !parameter.IsDeclared
        );
        Sql(block).ShouldBe(sql.Replace(" -- @z", string.Empty, StringComparison.Ordinal));
    }

    [Fact]
    public void Parse_KeptComment_HoldsNoParameter() =>
        Blocks("-- name: Q\n-- generator: keep-comments\nSELECT @a -- @b\n")
            .ShouldHaveSingleItem()
            .Parameters.Select(static parameter => parameter.Name)
            .ShouldBe(["a"]);

    private static string[] Tokens(SqlBlock block) =>
        [.. block.Tokens.Select(static token => token.Name + "=" + (token.Default ?? "<none>"))];

    [Fact]
    public void Parse_Tokens_AreListedOnceInOrderOfFirstAppearanceWithTheirDefaults()
    {
        const string Text =
            "-- name: Q\n-- token: {{filter:AND x = 1}}\nSELECT {{cols}} FROM {{table:users}} "
            + "WHERE 1 = 1 {{filter}} {{table}} {{filter}}\n";

        var block = Blocks(Text).ShouldHaveSingleItem();

        Tokens(block).ShouldBe(["cols=<none>", "table=users", "filter=AND x = 1"]);
        Sql(block).ShouldBe("SELECT {{cols}} FROM {{table}} WHERE 1 = 1 {{filter}} {{table}} {{filter}}");
    }

    [Fact]
    public void Parse_TokenMarkerForATokenTheQueryLacks_IsNotAnError() =>
        Tokens(Blocks("-- name: Q\n-- token: {{other:x}}\nSELECT {{a}}\n").ShouldHaveSingleItem())
            .ShouldBe(["a=<none>"]);

    [Fact]
    public void Parse_TheSameDefaultTwice_IsNotAConflict() =>
        Tokens(
                Blocks("-- name: Q\n-- token: {{a:x}}\n-- token: {{a:x}}\nSELECT {{a:x}} {{a: x }}\n")
                    .ShouldHaveSingleItem()
            )
            .ShouldBe(["a=x"]);

    [Theory]
    // Two inline defaults: at the second.
    [InlineData("-- name: Q\nSELECT {{a:x}} {{a:y}}\n", "{{a:y}}", "{{a:y}}")]
    // Two markers: at the second marker's value.
    [InlineData("-- name: Q\n-- token: {{a:x}}\n-- token: {{a:y}}\nSELECT {{a}}\n", "{{a:y}}", "token: {{a:y}}")]
    // A marker, then an inline default: at the inline one.
    [InlineData("-- name: Q\n-- token: {{a:x}}\nSELECT {{a:y}}\n", "{{a:y}}", "{{a:y}}")]
    // An inline default, then a marker: at the marker's value.
    [InlineData("-- name: Q\nSELECT {{a:y}}\n-- token: {{a:x}}\nFROM t\n", "{{a:x}}", "token: {{a:x}}")]
    public void Parse_TwoDefaultsForOneTokenThatDiffer_ConflictAtTheSecond(string text, string at, string argument) =>
        Errors(text)
            .ShouldBe([SqlParseError.Create(SqlParseErrorKind.ConflictingSettings, SpanOf(text, at), argument)]);

    [Theory]
    [InlineData("{{a}}")]
    [InlineData("{{a:x}} y")]
    [InlineData("x {{a:y}}")]
    [InlineData("{{a:x}}{{b:y}}")]
    [InlineData("see below")]
    [InlineData("{{a:'open}}")]
    [InlineData("{{a:/* open}}")]
    public void Parse_TokenMarkerThatIsNotOneTokenWithADefault_IsInvalid(string value)
    {
        var text = "-- name: Q\n-- token: " + value + "\nSELECT {{a}}\n";

        Errors(text)
            .ShouldBe([
                SqlParseError.Create(SqlParseErrorKind.InvalidMarkerValue, SpanOf(text, value), "token: " + value),
            ]);
    }

    [Fact]
    public void Parse_TokenMarkerWithoutAValue_IsInvalidAtTheMarker()
    {
        const string Text = "-- name: Q\n-- token:\nSELECT 1\n";

        Errors(Text)
            .ShouldBe([
                SqlParseError.Create(SqlParseErrorKind.InvalidMarkerValue, SpanOf(Text, "-- token:"), "token:"),
            ]);
    }

    [Fact]
    public void Parse_TokenMarkerWithAReservedName_IsAnError()
    {
        const string Text = "-- name: Q\n-- token: {{class:x}}\nSELECT 1\n";

        Errors(Text)
            .ShouldBe([
                SqlParseError.Create(SqlParseErrorKind.ReservedTokenName, SpanOf(Text, "{{class:x}}"), "class"),
            ]);
    }

    [Fact]
    public void Parse_TokenMarkerInThePreamble_IsNotAllowedThere()
    {
        const string Text = "-- token: {{a:x}}\n-- name: Q\nSELECT {{a}}\n";

        Errors(Text)
            .ShouldBe([
                SqlParseError.Create(
                    SqlParseErrorKind.MarkerNotAllowedHere,
                    SpanOf(Text, "-- token: {{a:x}}"),
                    "token",
                    "inside a query"
                ),
            ]);
    }

    [Fact]
    public void Parse_TokenMarkerInAFileWithoutNameMarker_IsAllowed() =>
        Tokens(Blocks("-- token: {{a:x}}\nSELECT {{a}}\n").ShouldHaveSingleItem()).ShouldBe(["a=x"]);

    private static string[] Parameters(SqlBlock block) =>
        [
            .. block.Parameters.Select(static parameter =>
                parameter.Name
                + ":"
                + (parameter.Type ?? "<none>")
                + ":"
                + Nullability(parameter.Nullable)
                + (parameter.IsDeclared ? ":declared" : string.Empty)
            ),
        ];

    private static string Nullability(bool? nullable)
    {
        if (nullable is not { } value)
        {
            return "<unsaid>";
        }

        return value ? "null" : "not null";
    }

    [Theory]
    [InlineData("@a", "a:<none>:<unsaid>:declared")]
    [InlineData("@a int", "a:int:<unsaid>:declared")]
    [InlineData("@a null", "a:<none>:null:declared")]
    [InlineData("@a not null", "a:<none>:not null:declared")]
    [InlineData("@a timestamptz null", "a:timestamptz:null:declared")]
    [InlineData("@a decimal(18, 2) not null", "a:decimal(18, 2):not null:declared")]
    [InlineData("@a double precision", "a:double precision:<unsaid>:declared")]
    // As people type it.
    [InlineData("@a\tint\tNULL", "a:int:null:declared")]
    [InlineData("@a int NOT  NULL", "a:int:not null:declared")]
    [InlineData("@A int", "a:int:<unsaid>:declared")]
    // A type that only ends in the letters of the word.
    [InlineData("@a mynull", "a:mynull:<unsaid>:declared")]
    [InlineData("@a knot null", "a:knot:null:declared")]
    public void Parse_ParamMarker_GivesItsParameterATypeAndNullability(string value, string expected) =>
        Parameters(Blocks("-- name: Q\n-- param: " + value + "\nSELECT @a\n").ShouldHaveSingleItem())
            .ShouldBe([expected]);

    [Fact]
    public void Parse_ParamMarkerWithWindowsLineEndings_IsRead() =>
        Parameters(Blocks("-- name: Q\r\n-- param: @a int null  \r\nSELECT @a\r\n").ShouldHaveSingleItem())
            .ShouldBe(["a:int:null:declared"]);

    [Fact]
    public void Parse_ParameterList_IsTheSqlsParametersThenTheDeclaredOnesInMarkerOrder()
    {
        const string Text =
            "-- name: Q\n-- param: @z int\n-- param: @b text null\n-- param: @y int\nSELECT @a, @b {{f}}\n";

        Parameters(Blocks(Text).ShouldHaveSingleItem())
            .ShouldBe([
                "a:<none>:<unsaid>",
                "b:text:null:declared",
                "z:int:<unsaid>:declared",
                "y:int:<unsaid>:declared",
            ]);
    }

    [Fact]
    public void Parse_TheSameDeclarationTwice_IsNotAConflict() =>
        Parameters(
                Blocks("-- name: Q\n-- param: @a int null\n-- param: @A int null\nSELECT @a\n").ShouldHaveSingleItem()
            )
            .ShouldBe(["a:int:null:declared"]);

    [Theory]
    [InlineData("@a text")]
    [InlineData("@a int null")]
    [InlineData("@a INT")]
    [InlineData("@a")]
    public void Parse_TwoDeclarationsOfOneParameterThatDiffer_Conflict(string second)
    {
        var text = "-- name: Q\n-- param: @a int\n-- param: " + second + "\nSELECT @a\n";

        Errors(text)
            .ShouldBe([
                SqlParseError.Create(
                    SqlParseErrorKind.ConflictingSettings,
                    new TextSpan(
                        text.IndexOf("-- param: " + second + "\n", StringComparison.Ordinal) + 10,
                        second.Length
                    ),
                    "param: " + second
                ),
            ]);
    }

    [Theory]
    [InlineData("a int")]
    [InlineData("@")]
    [InlineData("@ a")]
    [InlineData("@a-b int")]
    [InlineData("@a,@b")]
    [InlineData(":a int")]
    public void Parse_ParamMarkerWithoutAPrefixedNameOnItsOwn_IsInvalid(string value)
    {
        var text = "-- name: Q\n-- param: " + value + "\nSELECT 1\n";

        Errors(text)
            .ShouldBe([
                SqlParseError.Create(SqlParseErrorKind.InvalidMarkerValue, SpanOf(text, value), "param: " + value),
            ]);
    }

    [Fact]
    public void Parse_ParamMarkerWithoutAValue_IsInvalid()
    {
        const string Text = "-- name: Q\n-- param:\nSELECT 1\n";

        Errors(Text)
            .ShouldBe([
                SqlParseError.Create(SqlParseErrorKind.InvalidMarkerValue, SpanOf(Text, "-- param:"), "param:"),
            ]);
    }

    [Fact]
    public void Parse_ParamMarkerInThePreamble_IsNotAllowedThere()
    {
        const string Text = "-- param: @a int\n-- name: Q\nSELECT @a\n";

        Errors(Text)
            .ShouldBe([
                SqlParseError.Create(
                    SqlParseErrorKind.MarkerNotAllowedHere,
                    SpanOf(Text, "-- param: @a int"),
                    "param",
                    "inside a query"
                ),
            ]);
    }

    [Theory]
    [InlineData("@page")]
    [InlineData("@page null")]
    [InlineData("@page not null")]
    public void Parse_DeclaredOnlyParameterWithoutAType_IsAnError(string value)
    {
        var text = "-- name: Q\n-- param: " + value + "\nSELECT 1 {{tail:LIMIT @page}}\n";

        Errors(text)
            .ShouldBe([SqlParseError.Create(SqlParseErrorKind.MissingParameterType, SpanOf(text, value), "@page")]);
    }

    [Fact]
    public void Parse_ParameterOnlyInAnInlineDefault_MustBeDeclared()
    {
        const string Text = "-- name: Q\nSELECT @a {{f:AND x = @b AND y = @a}}\n";

        Errors(Text).ShouldBe([SqlParseError.Create(SqlParseErrorKind.UndeclaredParameter, SpanOf(Text, "@b"), "@b")]);
    }

    [Fact]
    public void Parse_ParameterOnlyInAMarkersDefault_MustBeDeclared()
    {
        const string Text = "-- name: Q\n-- token: {{f:AND x = @b AND y = '@c'}}\nSELECT 1 {{f}}\n";

        Errors(Text).ShouldBe([SqlParseError.Create(SqlParseErrorKind.UndeclaredParameter, SpanOf(Text, "@b"), "@b")]);
    }

    [Fact]
    public void Parse_ParameterInADefaultThatIsDeclaredWithAType_IsADeclaredOnlyParameter() =>
        Parameters(
                Blocks("-- name: Q\n-- param: @b int\n-- token: {{g:OFFSET @b}}\nSELECT @a {{f:LIMIT @b}} {{g}}\n")
                    .ShouldHaveSingleItem()
            )
            .ShouldBe(["a:<none>:<unsaid>", "b:int:<unsaid>:declared"]);

    // The query does not hold the token, so its marker's default is not the query's.
    [Fact]
    public void Parse_ParameterInTheDefaultOfATokenTheQueryLacks_IsNotChecked() =>
        Blocks("-- name: Q\n-- token: {{other:@x}}\nSELECT 1\n").ShouldHaveSingleItem().Parameters.ShouldBeEmpty();

    // A fragment passed at run time may use it.
    [Fact]
    public void Parse_DeclaredParameterThatNothingHolds_IsKept() =>
        Parameters(Blocks("-- name: Q\n-- param: @later int\nSELECT 1 {{f}}\n").ShouldHaveSingleItem())
            .ShouldBe(["later:int:<unsaid>:declared"]);

    [Fact]
    public void Parse_ParamMarkerInAFileWithoutNameMarker_IsAllowed() =>
        Parameters(Blocks("-- param: @a int\nSELECT @a\n").ShouldHaveSingleItem())
            .ShouldBe(["a:int:<unsaid>:declared"]);

    [Fact]
    public void Parse_ParameterInsideAToken_IsNotAParameterOfTheSql()
    {
        var block = Blocks("-- name: Q\n-- param: @b int\nSELECT @a {{f:AND x = @b}} {{t}}@c\n").ShouldHaveSingleItem();

        block.Parameters.Select(static parameter => parameter.Name).ShouldBe(["a", "c", "b"]);
    }

    [Fact]
    public void Parse_ParameterInsideAnIgnoredToken_IsAParameterOfTheSql() =>
        Blocks("-- name: Q\n-- token-ignore: f\nSELECT {{f:@b}}\n")
            .ShouldHaveSingleItem()
            .Parameters.Select(static parameter => parameter.Name)
            .ShouldBe(["b"]);

    [Fact]
    public void Parse_TokenIgnoreMarkers_KeepEachNamedTokenAsText()
    {
        const string Text =
            "-- name: Q\n-- token-ignore: a\n-- TOKEN-IGNORE: b\nSELECT '{{a}}', '{{b:x}}', {{c}}\n"
            + "-- name: R\nSELECT {{a}}\n";

        var blocks = Blocks(Text);

        Sql(blocks[0]).ShouldBe("SELECT '{{a}}', '{{b:x}}', {{c}}");
        Tokens(blocks[0]).ShouldBe(["c=<none>"]);
        // One query's marker does not reach the next.
        Tokens(blocks[1]).ShouldBe(["a=<none>"]);
    }

    [Theory]
    [InlineData("a b")]
    [InlineData("a-b")]
    [InlineData("{{a}}")]
    [InlineData("1a")]
    public void Parse_TokenIgnoreMarkerThatIsNotOneName_IsInvalid(string value)
    {
        var text = "-- name: Q\n-- token-ignore: " + value + "\nSELECT 1\n";

        Errors(text)
            .ShouldBe([
                SqlParseError.Create(
                    SqlParseErrorKind.InvalidMarkerValue,
                    SpanOf(text, value),
                    "token-ignore: " + value
                ),
            ]);
    }

    [Fact]
    public void Parse_TokenIgnoreMarkerWithoutAName_IsInvalidAtTheMarker()
    {
        const string Text = "-- name: Q\n-- token-ignore:\nSELECT 1\n";

        Errors(Text)
            .ShouldBe([
                SqlParseError.Create(
                    SqlParseErrorKind.InvalidMarkerValue,
                    SpanOf(Text, "-- token-ignore:"),
                    "token-ignore:"
                ),
            ]);
    }

    [Fact]
    public void Parse_TokenIgnoreMarkerInThePreamble_IsNotAllowedThere()
    {
        const string Text = "-- token-ignore: a\n-- name: Q\nSELECT {{a}}\n";

        Errors(Text)
            .ShouldBe([
                SqlParseError.Create(
                    SqlParseErrorKind.MarkerNotAllowedHere,
                    SpanOf(Text, "-- token-ignore: a"),
                    "token-ignore",
                    "inside a query"
                ),
            ]);
    }

    [Fact]
    public void Parse_TokenIgnoreAsAGeneratorParameter_IsNotKnown()
    {
        const string Text = "-- name: Q\n-- generator: token-ignore=a\nSELECT {{a}}\n";

        Errors(Text)
            .ShouldBe([
                SqlParseError.Create(
                    SqlParseErrorKind.UnknownGeneratorParameter,
                    SpanOf(Text, "token-ignore=a"),
                    "token-ignore=a"
                ),
            ]);
    }

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
        block.NameSpan.ShouldBe(SpanOf(text, name));
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
            .ShouldBe([SqlParseError.Create(SqlParseErrorKind.InvalidMarkerValue, SpanOf(text, at), "name: " + value)]);
    }

    [Fact]
    public void Parse_NameMarkerWithAShapeAndNoName_IsAnInvalidName()
    {
        const string Text = "-- name: -> one\nSELECT 1\n";

        Errors(Text)
            .ShouldBe([
                SqlParseError.Create(SqlParseErrorKind.InvalidName, SpanOf(Text, "-- name: -> one"), string.Empty),
            ]);
    }

    [Fact]
    public void Parse_FileWithoutNameMarker_HasNoShape() =>
        Blocks("SELECT 1\n").ShouldHaveSingleItem().Shape.ShouldBeNull();

    [Theory]
    [InlineData("-- output: models\n-- database: billing\n-- name: Q\nSELECT 1\n", "Models", "billing")]
    [InlineData("-- output: models\n-- name: Q\n-- output: SQL\n-- database: app\nSELECT 1\n", "Sql", "app")]
    [InlineData("-- database: billing\n-- name: Q\n-- output: code-gen\nSELECT 1\n", "CodeGen", "billing")]
    [InlineData("-- output: sql\n-- output: Sql\n-- database: a\n-- database: a\nSELECT 1\n", "Sql", "a")]
    public void Parse_OutputAndDatabaseMarkers_AreCarriedWithTheQuerysOverThePreambles(
        string text,
        string output,
        string database
    )
    {
        var markers = Blocks(text).ShouldHaveSingleItem().Markers;

        markers.Output.ShouldBe(Enum.Parse<OutputKind>(output));
        markers.Database.ShouldBe(database);
        markers.Parameters.ShouldBeNull();
    }

    [Theory]
    [InlineData("output", "models", "sql")]
    [InlineData("database", "billing", "Billing")]
    public void Parse_TwoValuesOfOneMarkerInOneScope_ConflictAtTheSecond(string word, string first, string second)
    {
        var text = "-- name: Q\n-- " + word + ": " + first + "\n-- " + word + ": " + second + "\nSELECT 1\n";

        Errors(text)
            .ShouldBe([
                SqlParseError.Create(SqlParseErrorKind.ConflictingSettings, SpanOf(text, second), word + ": " + second),
            ]);
    }

    // A comment that only starts like a marker is a marker now, and says so at its value.
    [Theory]
    [InlineData("output", "the rows we need")]
    [InlineData("output", "model")]
    [InlineData("database", "see the wiki")]
    [InlineData("database", "a/b")]
    public void Parse_MarkerWithAValueItDoesNotTake_IsInvalidAtTheValue(string word, string value)
    {
        var text = "-- name: Q\n-- " + word + ": " + value + "\nSELECT 1\n";

        Errors(text)
            .ShouldBe([
                SqlParseError.Create(SqlParseErrorKind.InvalidMarkerValue, SpanOf(text, value), word + ": " + value),
            ]);
    }

    [Theory]
    [InlineData("output")]
    [InlineData("database")]
    public void Parse_MarkerWithoutAValue_IsInvalidAtTheMarker(string word)
    {
        var text = "-- name: Q\n-- " + word + ":\nSELECT 1\n";

        Errors(text)
            .ShouldBe([
                SqlParseError.Create(
                    SqlParseErrorKind.InvalidMarkerValue,
                    SpanOf(text, "-- " + word + ":"),
                    word + ":"
                ),
            ]);
    }

    [Fact]
    public void Parse_PreambleSettings_AreOneLevelSharedByTheQueriesThatHaveNoMarkersOfTheirOwn()
    {
        var blocks = Blocks("-- output: models\n-- name: A\nSELECT 1\n-- name: B\nSELECT 2\n");

        blocks[0].Markers.Output.ShouldBe(OutputKind.Models);
        blocks[1].Markers.ShouldBeSameAs(blocks[0].Markers);
    }

    [Fact]
    public void Parse_ModelMarkers_AreCarriedWithTheQuerysOverThePreambles()
    {
        const string Text =
            "-- input-model-suffix: Args\n-- output-model-suffix: Row\n-- model-namespace: App.Models\n"
            + "-- input-model-type: class\n-- output-model-type: record\n-- collection-type: list\n"
            + "-- name: Q\n-- output-model-type: sealed class\n-- collection-type: IReadOnlyList\n"
            + "-- input-model: FindArgs\n-- output-model: App.Shared.UserRow\nSELECT @a\n";

        var block = Blocks(Text).ShouldHaveSingleItem();

        block.Markers.ShouldBe(
            new SettingsLevel
            {
                InputModelSuffix = "Args",
                OutputModelSuffix = "Row",
                ModelNamespace = "App.Models",
                InputModelType = ModelKind.Class,
                OutputModelType = ModelKind.SealedClass,
                CollectionType = CollectionKind.IReadOnlyList,
            }
        );
        block.InputModelName.ShouldBe("FindArgs");
        block.OutputModelName.ShouldBe("App.Shared.UserRow");
    }

    [Theory]
    // A marker for the file, written inside a query; and a marker for a query, written before the first one.
    [InlineData("input-model-suffix", "Args", false, "before the file's first query")]
    [InlineData("output-model-suffix", "Row", false, "before the file's first query")]
    [InlineData("model-namespace", "App", false, "before the file's first query")]
    [InlineData("input-model", "FindArgs", true, "inside a query")]
    [InlineData("output-model", "UserRow", true, "inside a query")]
    public void Parse_ModelMarkerInTheWrongScope_IsNotAllowedThere(
        string word,
        string value,
        bool inPreamble,
        string allowed
    )
    {
        var marker = "-- " + word + ": " + value;
        var text = inPreamble ? marker + "\n-- name: Q\nSELECT @a\n" : "-- name: Q\n" + marker + "\nSELECT @a\n";

        Errors(text)
            .ShouldBe([
                SqlParseError.Create(SqlParseErrorKind.MarkerNotAllowedHere, SpanOf(text, marker), word, allowed),
            ]);
    }

    [Theory]
    [InlineData("input-model-suffix", "A B")]
    [InlineData("output-model-suffix", "A.B")]
    [InlineData("model-namespace", "App.")]
    [InlineData("input-model-type", "struct")]
    [InlineData("output-model-type", "sealed")]
    [InlineData("collection-type", "HashSet")]
    [InlineData("input-model", "class")]
    [InlineData("output-model", "User Row")]
    public void Parse_ModelMarkerWithAValueItDoesNotTake_IsInvalidAtTheValue(string word, string value)
    {
        var text = "-- " + word + ": " + value + "\nSELECT @a\n";

        Errors(text)
            .ShouldBe([
                SqlParseError.Create(SqlParseErrorKind.InvalidMarkerValue, SpanOf(text, value), word + ": " + value),
            ]);
    }

    [Fact]
    public void Parse_TwoNamesForOneModel_ConflictAtTheSecond()
    {
        const string Text = "-- name: Q\n-- output-model: A\n-- output-model: B\nSELECT 1\n";

        Errors(Text)
            .ShouldBe([
                SqlParseError.Create(
                    SqlParseErrorKind.ConflictingSettings,
                    new TextSpan(Text.IndexOf("-- output-model: B", StringComparison.Ordinal) + 17, 1),
                    "output-model: B"
                ),
            ]);
    }

    [Fact]
    public void Parse_InputModelOnAQueryWithoutParameters_IsAnError()
    {
        const string Text = "-- name: Q\n-- input-model: FindArgs\nSELECT 1 {{f}}\n";

        Errors(Text)
            .ShouldBe([
                SqlParseError.Create(
                    SqlParseErrorKind.InputModelWithoutParameters,
                    SpanOf(Text, "-- input-model: FindArgs")
                ),
            ]);
    }

    // A parameter that only a marker declares is a parameter.
    [Fact]
    public void Parse_InputModelOnAQueryWithADeclaredOnlyParameter_IsFine() =>
        Blocks("-- name: Q\n-- param: @page int\n-- input-model: FindArgs\nSELECT 1 {{f}}\n")
            .ShouldHaveSingleItem()
            .InputModelName.ShouldBe("FindArgs");

    private static SqlBlock[] Blocks(string text, string fileName = "Query.sql", SqlDialectChoice dialect = default)
    {
        var result = SqlFileParser.Parse(text, fileName, dialect);
        result.Errors.ShouldBeEmpty();
        return [.. result.Blocks];
    }

    private static SqlParseError[] Errors(
        string text,
        string fileName = "Query.sql",
        SqlDialectChoice dialect = default
    )
    {
        var result = SqlFileParser.Parse(text, fileName, dialect);
        result.Blocks.ShouldBeEmpty();
        return [.. result.Errors];
    }

    private static string[] TokenNames(EquatableArray<SqlSegment> segments) =>
        [
            .. segments
                .Where(static segment => segment.Kind == SqlSegmentKind.Token)
                .Select(static segment => segment.Text),
        ];

    private static string? Kept(SqlBlock block) =>
        block.KeptSegments is { } segments
            ? string.Concat(
                segments.Select(static segment =>
                    segment.Kind == SqlSegmentKind.Token ? "{{" + segment.Text + "}}" : segment.Text
                )
            )
            : null;

    private static SqlBlock Block(string text, bool commentsWanted) =>
        SqlFileParser.Parse(text, "Query.sql", default, commentsWanted).Blocks.ShouldHaveSingleItem();

    private static string Sql(SqlBlock block) =>
        string.Concat(
            block.Segments.Select(static segment =>
                segment.Kind == SqlSegmentKind.Token ? "{{" + segment.Text + "}}" : segment.Text
            )
        );

    private static TextSpan SpanOf(string text, string value) =>
        new(text.IndexOf(value, StringComparison.Ordinal), value.Length);
}
