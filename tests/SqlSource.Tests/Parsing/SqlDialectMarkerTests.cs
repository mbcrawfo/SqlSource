using System;
using System.Linq;
using Shouldly;
using SqlSource.Parsing;
using Xunit;

namespace SqlSource.Tests.Parsing;

public class SqlDialectMarkerTests
{
    [Theory]
    [InlineData("-- dialect: mysql\n-- name: A\nSELECT 1")]
    [InlineData("-- DIALECT: MySql\n-- name: A\nSELECT 1")]
    [InlineData("--dialect:mysql\n-- name: A\nSELECT 1")]
    [InlineData("-- dialect: mysql  \r\n-- name: A\r\nSELECT 1")]
    [InlineData("\n\n  -- dialect: mysql\nSELECT 1")]
    [InlineData("-- a comment\n-- generator: keep-comments\n-- dialect: mysql\nSELECT 1")]
    [InlineData("/* Copyright\n   (c) Example */\n-- dialect: mysql\n-- name: A\nSELECT 1")]
    public void Apply_MarkerInTheHeader_SwitchesTheLexer(string text)
    {
        var lexer = new SqlLexer(text, SqlDialectRules.Ansi);

        _ = SqlDialectMarker.Apply(lexer, text);

        lexer.Rules.ShouldBeSameAs(SqlDialectRules.MySql);
    }

    [Theory]
    [InlineData("-- dialect: mysql,no-backslash-escapes\nSELECT 'C:\\temp\\'")]
    [InlineData("-- dialect: mysql, no-backslash-escapes\nSELECT 'C:\\temp\\'")]
    [InlineData("-- dialect: MySQL ,\tNO_BACKSLASH_ESCAPES\nSELECT 'C:\\temp\\'")]
    public void Apply_MarkerWithAnOption_SwitchesTheLexerToTheRulesOfThatOption(string text)
    {
        var lexer = new SqlLexer(text, SqlDialectRules.Ansi);

        _ = SqlDialectMarker.Apply(lexer, text);

        lexer.Rules.ShouldBeSameAs(
            SqlDialectRules.For(new SqlDialectChoice(SqlDialect.MySql, SqlDialectOptions.NoBackslashEscapes))
        );
        lexer.Rules.ShouldNotBeSameAs(SqlDialectRules.MySql);
    }

    [Theory]
    // No marker.
    [InlineData("SELECT 1")]
    [InlineData("-- name: A\nSELECT 1")]
    [InlineData("")]
    // After SQL.
    [InlineData("SELECT 1;\n-- dialect: mysql\n")]
    [InlineData("-- a\nSELECT 1; -- dialect: mysql")]
    // After a hint, which is kept in the SQL.
    [InlineData("/*+ h */\n-- dialect: mysql\nSELECT 1")]
    // Inside a named query.
    [InlineData("-- name: A\n-- dialect: mysql\nSELECT 1")]
    // Not a dialect.
    [InlineData("-- dialect: pgsql\nSELECT 1")]
    [InlineData("-- dialect:\nSELECT 1")]
    [InlineData("-- dialect: mysql keep-comments\nSELECT 1")]
    [InlineData("-- dialect: postgres,ansi-quotes\nSELECT 1")]
    // Not a marker.
    [InlineData("-- dialect=mysql\nSELECT 1")]
    [InlineData("-- dialects: mysql\nSELECT 1")]
    [InlineData("/* -- dialect: mysql */\nSELECT 1")]
    // The old form: a generator parameter that is not known.
    [InlineData("-- generator: dialect=mysql\nSELECT 1")]
    public void Apply_NoValidMarkerInTheHeader_LeavesTheLexerAsItWas(string text)
    {
        var lexer = new SqlLexer(text, SqlDialectRules.Ansi);

        _ = SqlDialectMarker.Apply(lexer, text);

        lexer.Rules.ShouldBeSameAs(SqlDialectRules.Ansi);
    }

    [Fact]
    public void Apply_SeveralMarkers_TakesTheFirstThatNamesADialect()
    {
        const string Text = "-- dialect: nope\n-- dialect: oracle\n-- dialect: mysql\n-- name: A\nSELECT 1";
        var lexer = new SqlLexer(Text, SqlDialectRules.Ansi);

        _ = SqlDialectMarker.Apply(lexer, Text);

        lexer.Rules.ShouldBeSameAs(SqlDialectRules.Oracle);
    }

    // The header is read under the dialect the lexer starts with, up to the marker.
    [Fact]
    public void Apply_CommentFormOfTheStartingDialectAboveTheMarker_IsRead()
    {
        const string Text = "# licence\n-- dialect: postgres\nSELECT 1";
        var underMySql = new SqlLexer(Text, SqlDialectRules.MySql);
        var underAnsi = new SqlLexer(Text, SqlDialectRules.Ansi);

        _ = SqlDialectMarker.Apply(underMySql, Text);
        var headerEnd = SqlDialectMarker.Apply(underAnsi, Text);

        underMySql.Rules.ShouldBeSameAs(SqlDialectRules.PostgreSql);

        // To ANSI the first line is SQL, so the header is empty and the marker is not in it.
        underAnsi.Rules.ShouldBeSameAs(SqlDialectRules.Ansi);
        headerEnd.ShouldBe(0);
    }

    // The marker applies from the line after it: a comment form of the new dialect is read there.
    [Fact]
    public void Apply_CommentFormOfTheNewDialectBelowTheMarker_IsPartOfTheHeader()
    {
        const string Text = "-- dialect: mysql\n# c\nSELECT 1";
        var lexer = new SqlLexer(Text, SqlDialectRules.Ansi);

        var headerEnd = SqlDialectMarker.Apply(lexer, Text);

        headerEnd.ShouldBe(Text.IndexOf("\nSELECT", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("-- a\n-- name: A\nSELECT 1", "-- name: A")]
    [InlineData("-- dialect: mysql\n\n  -- name: A\nSELECT 1", "-- name: A")]
    [InlineData("-- name: A\n-- name: B\nSELECT 1", "-- name: A")]
    public void Apply_FileWithANameMarker_EndsTheHeaderAtTheMarker(string text, string marker)
    {
        var lexer = new SqlLexer(text, SqlDialectRules.Ansi);

        SqlDialectMarker.Apply(lexer, text).ShouldBe(text.IndexOf(marker, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("SELECT 1", 0)]
    [InlineData("  \n SELECT 1", 0)]
    [InlineData("", 0)]
    [InlineData("-- a\nSELECT 1", 4)]
    [InlineData("-- a\n/* b */ SELECT 1", 12)]
    [InlineData("-- a\n-- b", 9)]
    [InlineData("-- a\n/*+ h */ SELECT 1", 4)]
    public void Apply_FileWithoutANameMarker_EndsTheHeaderAfterItsLastLeadingComment(string text, int expected)
    {
        var lexer = new SqlLexer(text, SqlDialectRules.Ansi);

        SqlDialectMarker.Apply(lexer, text).ShouldBe(expected);
    }

    [Fact]
    public void Apply_ThenReadToEnd_GivesEveryLexemeOfTheFile()
    {
        const string Text = "-- dialect: mysql\n-- name: A\nSELECT 'a\\'b' # c\n";
        var lexer = new SqlLexer(Text, SqlDialectRules.Ansi);

        _ = SqlDialectMarker.Apply(lexer, Text);
        var result = lexer.ReadToEnd();

        result.Error.ShouldBeNull();
        result.Lexemes[0].Span.Start.ShouldBe(0);
        result.Lexemes[^1].Span.End.ShouldBe(Text.Length);
        result.Lexemes.Count(lexeme => lexeme.Kind == SqlLexemeKind.LineComment).ShouldBe(3);
        result.Lexemes.Count(lexeme => lexeme.Kind == SqlLexemeKind.Quoted).ShouldBe(1);
    }

    [Theory]
    [InlineData("-- dialect: mysql", "dialect: mysql")]
    [InlineData("-- DIALECT:   MySql, ansi-quotes  ", "dialect: MySql, ansi-quotes")]
    [InlineData("-- dialect:", "dialect:")]
    [InlineData("-- dialect:   ", "dialect:")]
    public void Describe_Marker_GivesTheWordAndTheTrimmedValue(string line, string expected)
    {
        var marker = SqlMarkerReader.Read(line, SqlLexer.Lex(line, SqlDialectRules.Ansi).Lexemes[0]).ShouldNotBeNull();

        SqlDialectMarker.Describe(line, marker).ShouldBe(expected);
    }
}
