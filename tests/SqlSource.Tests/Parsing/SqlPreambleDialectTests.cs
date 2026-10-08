using System;
using System.Linq;
using Shouldly;
using SqlSource.Parsing;
using Xunit;

namespace SqlSource.Tests.Parsing;

public class SqlPreambleDialectTests
{
    [Theory]
    [InlineData("-- generator: dialect=mysql\n-- name: A\nSELECT 1")]
    [InlineData("-- generator: DIALECT=MySql\n-- name: A\nSELECT 1")]
    [InlineData("-- generator: keep-comments dialect=mysql token-ignore=a\n-- name: A\nSELECT 1")]
    [InlineData("\n\n  -- generator: dialect=mysql\nSELECT 1")]
    [InlineData("-- a comment\n-- generator: keep-comments\n-- generator: dialect=mysql\nSELECT 1")]
    [InlineData("/* Copyright\n   (c) Example */\n-- generator: dialect=mysql\n-- name: A\nSELECT 1")]
    public void Apply_GeneratorParameterInTheHeader_SwitchesTheLexer(string text)
    {
        var lexer = new SqlLexer(text, SqlDialectRules.Ansi);

        _ = SqlPreambleDialect.Apply(lexer, text);

        lexer.Rules.ShouldBeSameAs(SqlDialectRules.MySql);
    }

    [Fact]
    public void Apply_GeneratorParameterWithAnOption_SwitchesTheLexerToTheRulesOfThatOption()
    {
        const string Text = "-- generator: dialect=mysql,no-backslash-escapes\nSELECT 'C:\\temp\\'";
        var lexer = new SqlLexer(Text, SqlDialectRules.Ansi);

        _ = SqlPreambleDialect.Apply(lexer, Text);

        lexer.Rules.ShouldBeSameAs(
            SqlDialectRules.For(new SqlDialectChoice(SqlDialect.MySql, SqlDialectOptions.NoBackslashEscapes))
        );
        lexer.Rules.ShouldNotBeSameAs(SqlDialectRules.MySql);
    }

    [Theory]
    // No generator parameter.
    [InlineData("SELECT 1")]
    [InlineData("-- name: A\nSELECT 1")]
    [InlineData("")]
    // After SQL.
    [InlineData("SELECT 1;\n-- generator: dialect=mysql\n")]
    [InlineData("-- a\nSELECT 1; -- generator: dialect=mysql")]
    // After a hint, which is kept in the SQL.
    [InlineData("/*+ h */\n-- generator: dialect=mysql\nSELECT 1")]
    // Inside a named query.
    [InlineData("-- name: A\n-- generator: dialect=mysql\nSELECT 1")]
    // Not a dialect.
    [InlineData("-- generator: dialect=pgsql\nSELECT 1")]
    [InlineData("-- generator: dialect=\nSELECT 1")]
    [InlineData("-- generator: dialect\nSELECT 1")]
    [InlineData("-- generator: xdialect=mysql dialects=mysql\nSELECT 1")]
    // Not a marker.
    [InlineData("-- dialect=mysql\nSELECT 1")]
    [InlineData("/* -- generator: dialect=mysql */\nSELECT 1")]
    public void Apply_NoValidGeneratorParameterInTheHeader_LeavesTheLexerAsItWas(string text)
    {
        var lexer = new SqlLexer(text, SqlDialectRules.Ansi);

        _ = SqlPreambleDialect.Apply(lexer, text);

        lexer.Rules.ShouldBeSameAs(SqlDialectRules.Ansi);
    }

    [Fact]
    public void Apply_SeveralGeneratorParameters_TakesTheFirstThatNamesADialect()
    {
        const string Text =
            "-- generator: dialect=nope dialect=oracle\n-- generator: dialect=mysql\n-- name: A\nSELECT 1";
        var lexer = new SqlLexer(Text, SqlDialectRules.Ansi);

        _ = SqlPreambleDialect.Apply(lexer, Text);

        lexer.Rules.ShouldBeSameAs(SqlDialectRules.Oracle);
    }

    // The header is read under the dialect the lexer starts with, up to the generator parameter.
    [Fact]
    public void Apply_CommentFormOfTheStartingDialectAboveTheGeneratorParameter_IsRead()
    {
        const string Text = "# licence\n-- generator: dialect=postgres\nSELECT 1";
        var underMySql = new SqlLexer(Text, SqlDialectRules.MySql);
        var underAnsi = new SqlLexer(Text, SqlDialectRules.Ansi);

        _ = SqlPreambleDialect.Apply(underMySql, Text);
        var headerEnd = SqlPreambleDialect.Apply(underAnsi, Text);

        underMySql.Rules.ShouldBeSameAs(SqlDialectRules.PostgreSql);

        // To ANSI the first line is SQL, so the header is empty and the generator parameter is not in it.
        underAnsi.Rules.ShouldBeSameAs(SqlDialectRules.Ansi);
        headerEnd.ShouldBe(0);
    }

    // The generator parameter applies from the line after it: a comment form of the new dialect is read there.
    [Fact]
    public void Apply_CommentFormOfTheNewDialectBelowTheGeneratorParameter_IsPartOfTheHeader()
    {
        const string Text = "-- generator: dialect=mysql\n# c\nSELECT 1";
        var lexer = new SqlLexer(Text, SqlDialectRules.Ansi);

        var headerEnd = SqlPreambleDialect.Apply(lexer, Text);

        headerEnd.ShouldBe(Text.IndexOf("\nSELECT", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("-- a\n-- name: A\nSELECT 1", "-- name: A")]
    [InlineData("-- generator: dialect=mysql\n\n  -- name: A\nSELECT 1", "-- name: A")]
    [InlineData("-- name: A\n-- name: B\nSELECT 1", "-- name: A")]
    public void Apply_FileWithANameMarker_EndsTheHeaderAtTheMarker(string text, string marker)
    {
        var lexer = new SqlLexer(text, SqlDialectRules.Ansi);

        SqlPreambleDialect.Apply(lexer, text).ShouldBe(text.IndexOf(marker, StringComparison.Ordinal));
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

        SqlPreambleDialect.Apply(lexer, text).ShouldBe(expected);
    }

    [Fact]
    public void Apply_ThenReadToEnd_GivesEveryLexemeOfTheFile()
    {
        const string Text = "-- generator: dialect=mysql\n-- name: A\nSELECT 'a\\'b' # c\n";
        var lexer = new SqlLexer(Text, SqlDialectRules.Ansi);

        _ = SqlPreambleDialect.Apply(lexer, Text);
        var result = lexer.ReadToEnd();

        result.Error.ShouldBeNull();
        result.Lexemes[0].Span.Start.ShouldBe(0);
        result.Lexemes[^1].Span.End.ShouldBe(Text.Length);
        result.Lexemes.Count(lexeme => lexeme.Kind == SqlLexemeKind.LineComment).ShouldBe(3);
        result.Lexemes.Count(lexeme => lexeme.Kind == SqlLexemeKind.Quoted).ShouldBe(1);
    }
}
