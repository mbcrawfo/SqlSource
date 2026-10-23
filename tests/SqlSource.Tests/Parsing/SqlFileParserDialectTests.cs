using System;
using System.Linq;
using Microsoft.CodeAnalysis.Text;
using Shouldly;
using SqlSource.Parsing;
using SqlSource.Settings;
using Xunit;
using static SqlSource.Tests.Parsing.ParseOf;

namespace SqlSource.Tests.Parsing;

// The parser's dialect: the project's, dialect markers and continued strings.
public sealed class SqlFileParserDialectTests
{
    [Theory]
    [InlineData("SELECT 1;\n", "MySql", "MySql")]
    [InlineData("-- dialect: mssql\nSELECT 1;\n", "MySql", "SqlServer")]
    [InlineData("-- dialect: nope\nSELECT 'open\n", "Oracle", "Oracle")]
    public void Parse_Result_NamesTheDialectTheFileWasReadBy(string text, string given, string expected) =>
        SqlFileParser
            .Parse(text, "Query.sql", new SqlDialectChoice(Enum.Parse<SqlDialect>(given), SqlDialectOptions.None))
            .Dialect.ShouldBe(Enum.Parse<SqlDialect>(expected));

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
                    TextSpans.Of(Text, "mysql, ansi-quotes"),
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
                SqlParseError.Create(
                    SqlParseErrorKind.InvalidMarkerValue,
                    TextSpans.Of(text, value),
                    "dialect: " + value,
                    ""
                ),
            ]);

    [Fact]
    public void Parse_TwoDialectsInOneHeader_IsAnErrorAtTheSecondAndTheSameDialectTwiceIsNot()
    {
        const string Conflict = "-- dialect: mysql\n-- dialect: oracle\n-- name: A\nSELECT 1\n";
        const string Repeat = "-- dialect: mysql\n-- dialect: MYSQL\n-- name: A\nSELECT 1 # c\n";

        Errors(Conflict)
            .ShouldBe([
                SqlParseError.Create(
                    SqlParseErrorKind.ConflictingSettings,
                    TextSpans.Of(Conflict, "oracle"),
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
                    TextSpans.Of(Text, "dialect=mysql"),
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
}
