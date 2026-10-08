using System;
using System.Linq;
using Microsoft.CodeAnalysis.Text;
using Shouldly;
using SqlSource.Parsing;
using Xunit;

namespace SqlSource.Tests.Parsing;

public class SqlFileParserTests
{
    [Fact]
    public void Parse_FileWithoutNameMarker_IsOneBlockNamedAfterTheFile()
    {
        var block = Blocks("SELECT 1;\n", "GetUser.sql").ShouldHaveSingleItem();

        block.Name.ShouldBe("GetUser");
        block.NameSpan.ShouldBe(new TextSpan(0, 0));
        block.Summary.ShouldBeNull();
        block.KeepComments.ShouldBeFalse();
        block.TokenValidation.ShouldBeNull();
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
        block.KeepComments.ShouldBeTrue();
        block.TokenValidation.ShouldBe(false);
        Sql(block).ShouldBe("SELECT 1 -- c\nFROM t");
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

        block.KeepComments.ShouldBeTrue();
        Sql(block).ShouldBe("-- kept\nSELECT 1");
    }

    [Fact]
    public void Parse_PreambleGeneratorParameters_ApplyToEveryBlock()
    {
        const string Text =
            "-- generator: no-token-validation token-ignore=x\n"
            + "-- name: A\nSELECT {{x}}\n"
            + "-- name: B\nSELECT {{x}} {{y}}\n";

        var blocks = Blocks(Text);

        blocks[0].TokenValidation.ShouldBe(false);
        blocks[1].TokenValidation.ShouldBe(false);
        blocks[0].Segments.ShouldBe([new SqlSegment(SqlSegmentKind.Literal, "SELECT {{x}}")]);
        blocks[1]
            .Segments.ShouldBe([
                new SqlSegment(SqlSegmentKind.Literal, "SELECT {{x}} "),
                new SqlSegment(SqlSegmentKind.Token, "y"),
            ]);
    }

    [Fact]
    public void Parse_BlockValidationGeneratorParameter_OverridesThePreamble()
    {
        const string Text =
            "-- generator: no-token-validation\n"
            + "-- name: A\n-- generator: token-validation\nSELECT 1\n"
            + "-- name: B\nSELECT 2\n";

        var blocks = Blocks(Text);

        blocks[0].TokenValidation.ShouldBe(true);
        blocks[1].TokenValidation.ShouldBe(false);
    }

    [Fact]
    public void Parse_BlockTokenIgnore_AddsToThePreamble()
    {
        const string Text =
            "-- generator: token-ignore=x\n-- name: A\n-- generator: token-ignore=y\nSELECT {{x}} {{y}} {{z}}\n";

        Blocks(Text)
            .ShouldHaveSingleItem()
            .Segments.ShouldBe([
                new SqlSegment(SqlSegmentKind.Literal, "SELECT {{x}} {{y}} "),
                new SqlSegment(SqlSegmentKind.Token, "z"),
            ]);
    }

    [Fact]
    public void Parse_BlockGeneratorParameter_DoesNotLeakIntoTheNextBlock()
    {
        const string Text =
            "-- name: A\n-- generator: keep-comments token-validation token-ignore=x\nSELECT 1 -- c\n"
            + "-- name: B\nSELECT {{x}} -- c\n";

        var blocks = Blocks(Text);

        blocks[1].KeepComments.ShouldBeFalse();
        blocks[1].TokenValidation.ShouldBeNull();
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
    [InlineData(
        "-- name: A\nSELECT 1\n  -- generator: token-ignore=x\n-- name: B\nSELECT 2",
        "-- generator: token-ignore=x"
    )]
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
        block.TokenValidation.ShouldBe(false);
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

        Blocks(Text)
            .ShouldHaveSingleItem()
            .Segments.Where(static segment => segment.Kind == SqlSegmentKind.Token)
            .Select(static segment => segment.Text)
            .ShouldBe(["a", "b"]);
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
        const string Text = "-- name: A\n-- generator: token-ignore=class\nSELECT {{class}}";

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
    [InlineData("-- generator: token-ignore=", nameof(SqlParseErrorKind.InvalidMarkerValue), "token-ignore=")]
    [InlineData(
        "-- generator: token-validation no-token-validation",
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
        windowsBlocks.Select(static block => block.TokenValidation).ShouldBe([false, false]);
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
    public void Parse_DialectGeneratorParameterInThePreamble_AppliesToEveryBlockAndIsNotInTheSql()
    {
        const string Text =
            "/* Copyright (c) Example */\n-- generator: dialect=mysql\n\n"
            + "-- name: A\nSELECT 'a\\'b' # c\n-- name: B\nSELECT 5--3 # d\n";

        Blocks(Text).Select(Sql).ShouldBe(["SELECT 'a\\'b'", "SELECT 5--3"]);
    }

    [Fact]
    public void Parse_DialectGeneratorParameter_ReplacesTheDialectOfTheProject()
    {
        const string Text = "-- generator: dialect=mssql\n-- name: A\nSELECT [a'b] -- c\n";

        Sql(Blocks(Text, dialect: SqlDialect.MySql).ShouldHaveSingleItem()).ShouldBe("SELECT [a'b]");
    }

    // Under plain MySQL the backslash takes the closing quote with it, and the string is not closed.
    [Fact]
    public void Parse_DialectGeneratorParameterWithAnOption_ReadsTheFileByThatOption()
    {
        const string Text =
            "-- generator: dialect=mysql,no-backslash-escapes\n-- name: A\nSELECT 'C:\\temp\\' AS path -- c\n";

        Sql(Blocks(Text).ShouldHaveSingleItem()).ShouldBe("SELECT 'C:\\temp\\' AS path");
    }

    [Fact]
    public void Parse_OptionOfTheProject_IsUsedByAFileWithoutAGeneratorParameter()
    {
        const string Text = "-- name: A\nSELECT \"a\\\" AS b -- c\n";
        var project = new SqlDialectChoice(SqlDialect.MySql, SqlDialectOptions.AnsiQuotes);

        Sql(Blocks(Text, dialect: project).ShouldHaveSingleItem()).ShouldBe("SELECT \"a\\\" AS b");
    }

    // The generator parameter names no option, so the file has none: the option of the project is not kept.
    [Fact]
    public void Parse_DialectGeneratorParameterWithoutOptions_ReplacesTheOptionsOfTheProjectToo()
    {
        const string Text = "-- generator: dialect=mysql\n-- name: A\nSELECT 'it\\'s' -- c\n";
        var project = new SqlDialectChoice(SqlDialect.MySql, SqlDialectOptions.NoBackslashEscapes);

        Sql(Blocks(Text, dialect: project).ShouldHaveSingleItem()).ShouldBe("SELECT 'it\\'s'");
    }

    [Fact]
    public void Parse_TwoDialectGeneratorParametersThatDifferOnlyInOptions_IsAnError()
    {
        const string Text =
            "-- generator: dialect=mysql\n-- generator: dialect=mysql,ansi-quotes\n-- name: A\nSELECT 1\n";

        Errors(Text)
            .ShouldBe([
                SqlParseError.Create(
                    SqlParseErrorKind.ConflictingSettings,
                    SpanOf(Text, "dialect=mysql,ansi-quotes"),
                    "dialect=mysql,ansi-quotes"
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

        Sql(Blocks(Text, dialect: SqlDialect.PostgreSql).ShouldHaveSingleItem())
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
    public void Parse_DialectGeneratorParameterInAFileWithoutANameMarker_GoesAboveItsSql()
    {
        const string Text = "-- summary: S\n-- generator: dialect=oracle keep-comments\nSELECT q'[it's]' --+ h\n";

        var block = Blocks(Text).ShouldHaveSingleItem();

        Sql(block).ShouldBe("SELECT q'[it's]' --+ h");
        block.Summary.ShouldBe("S");
        block.KeepComments.ShouldBeTrue();
    }

    // The header is read under the dialect of the project, and the rest of the file under the generator parameter's.
    [Fact]
    public void Parse_CommentAboveTheDialectGeneratorParameter_IsReadUnderTheDialectOfTheProject()
    {
        const string Text = "# licence\n-- generator: dialect=postgres\n-- name: A\nSELECT 1 # 2\n";

        Sql(Blocks(Text, dialect: SqlDialect.MySql).ShouldHaveSingleItem()).ShouldBe("SELECT 1 # 2");
    }

    [Theory]
    [InlineData("-- name: A\n-- generator: dialect=mysql\nSELECT 1\n")]
    [InlineData("-- generator: dialect=mysql\n-- name: A\n-- generator: dialect=mysql\nSELECT 1\n")]
    [InlineData("SELECT 1\n-- generator: dialect=mysql\nFROM t\n")]
    [InlineData("-- name: A\nSELECT 1\n-- name: B\n-- generator: dialect=nope\nSELECT 2\n")]
    public void Parse_DialectGeneratorParameterInsideAQueryOrAfterSql_IsMisplaced(string text)
    {
        var error = Errors(text).ShouldHaveSingleItem();

        error.Kind.ShouldBe(SqlParseErrorKind.MisplacedDialect);
        error.Span.Start.ShouldBe(text.LastIndexOf("dialect=", StringComparison.Ordinal));
    }

    // A marker after the last SQL of its block is reported as that.  A dialect generator parameter in it is reported as
    // misplaced too, so that the user learns at once that it belongs at the top of the file.
    [Theory]
    [InlineData("SELECT 1;\n-- generator: dialect=mysql\n")]
    [InlineData("-- name: A\nSELECT 1;\n-- generator: keep-comments dialect=nope\n")]
    [InlineData("-- name: A\nSELECT 1;\n-- generator: DIALECT\n-- name: B\nSELECT 2;\n")]
    public void Parse_DialectGeneratorParameterAfterTheLastSqlOfItsBlock_IsMisplacedAsWellAsAtTheEndOfItsBlock(
        string text
    )
    {
        var errors = Errors(text);

        errors
            .Select(static error => error.Kind)
            .ShouldBe([SqlParseErrorKind.MarkerAtEndOfBlock, SqlParseErrorKind.MisplacedDialect]);
        errors[0].Span.Start.ShouldBe(text.IndexOf("-- generator:", StringComparison.Ordinal));
        errors[1].Span.Start.ShouldBe(text.IndexOf("dialect", StringComparison.OrdinalIgnoreCase));
    }

    // A misplaced generator parameter does not change how the file is read: the # would be a comment under MySQL.
    [Fact]
    public void Parse_MisplacedDialectGeneratorParameter_IsNotApplied() =>
        Errors("-- name: A\n-- generator: dialect=mysql\nSELECT 'it''s' # '\n")
            .ShouldHaveSingleItem()
            .Kind.ShouldBe(SqlParseErrorKind.UnterminatedQuote);

    [Fact]
    public void Parse_DialectGeneratorParameterAfterSqlInThePreamble_IsReportedWithTheSql()
    {
        const string Text = "SELECT 0;\n-- generator: dialect=mysql\n-- name: A\nSELECT 1\n";

        Errors(Text)
            .Select(static error => error.Kind)
            .ShouldBe([SqlParseErrorKind.SqlBeforeFirstName, SqlParseErrorKind.MisplacedDialect]);
    }

    [Theory]
    [InlineData("-- generator: dialect=pgsql\n-- name: A\nSELECT 1\n", "dialect=pgsql")]
    [InlineData("-- generator: dialect\nSELECT 1\n", "dialect")]
    public void Parse_DialectGeneratorParameterWithoutAValidName_IsAnErrorAtTheGeneratorParameter(
        string text,
        string parameter
    ) =>
        Errors(text)
            .ShouldBe([SqlParseError.Create(SqlParseErrorKind.InvalidMarkerValue, SpanOf(text, parameter), parameter)]);

    [Fact]
    public void Parse_TwoDialectsInOneHeader_IsAnErrorAtTheSecondAndTheSameDialectTwiceIsNot()
    {
        const string Conflict = "-- generator: dialect=mysql\n-- generator: dialect=oracle\n-- name: A\nSELECT 1\n";
        const string Repeat = "-- generator: dialect=mysql\n-- generator: dialect=MYSQL\n-- name: A\nSELECT 1 # c\n";

        Errors(Conflict)
            .ShouldBe([
                SqlParseError.Create(
                    SqlParseErrorKind.ConflictingSettings,
                    SpanOf(Conflict, "dialect=oracle"),
                    "dialect=oracle"
                ),
            ]);
        Sql(Blocks(Repeat).ShouldHaveSingleItem()).ShouldBe("SELECT 1");
    }

    [Fact]
    public void Parse_SameTextUnderTwoDialects_GivesUnequalResultsOnlyWhereTheyReadItDifferently()
    {
        SqlFileParser
            .Parse("SELECT 1 # c", "Query.sql", SqlDialect.MySql)
            .ShouldNotBe(SqlFileParser.Parse("SELECT 1 # c", "Query.sql", SqlDialect.Ansi));
        SqlFileParser
            .Parse("SELECT 1 -- c", "Query.sql", SqlDialect.MySql)
            .ShouldBe(SqlFileParser.Parse("SELECT 1 -- c", "Query.sql", SqlDialect.Ansi));
    }

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

    private static string Sql(SqlBlock block) =>
        string.Concat(
            block.Segments.Select(static segment =>
                segment.Kind == SqlSegmentKind.Token ? "{{" + segment.Text + "}}" : segment.Text
            )
        );

    private static TextSpan SpanOf(string text, string value) =>
        new(text.IndexOf(value, StringComparison.Ordinal), value.Length);
}
