using System;
using System.Linq;
using Microsoft.CodeAnalysis.Text;
using Shouldly;
using SqlSource.Parsing;
using Xunit;

namespace SqlSource.Tests.Parsing;

public class SqlMarkerReaderTests
{
    [Theory]
    [InlineData("-- name: GetUser", "Name:GetUser")]
    [InlineData("--name:GetUser", "Name:GetUser")]
    [InlineData("-- NAME: GetUser", "Name:GetUser")]
    [InlineData("--\t Name:\tGetUser  ", "Name:GetUser")]
    [InlineData("-- summary: Loads a user.", "Summary:Loads a user.")]
    [InlineData("-- Summary:Loads: a -- user", "Summary:Loads: a -- user")]
    [InlineData("-- generator: keep-comments  sort-input", "GeneratorParameters:keep-comments  sort-input")]
    [InlineData("-- GENERATOR: x", "GeneratorParameters:x")]
    [InlineData("-- dialect: postgres", "Dialect:postgres")]
    [InlineData("-- DIALECT: MySql", "Dialect:MySql")]
    [InlineData("--dialect:mysql, ansi-quotes  ", "Dialect:mysql, ansi-quotes")]
    [InlineData("-- dialect:", "Dialect:")]
    [InlineData("-- token-ignore: a", "TokenIgnore:a")]
    [InlineData("-- TOKEN-IGNORE:a", "TokenIgnore:a")]
    [InlineData("-- param: @a int", "Param:@a int")]
    [InlineData("--PARAM:@a", "Param:@a")]
    [InlineData("-- param:", "Param:")]
    [InlineData("-- output: sql", "Output:sql")]
    [InlineData("-- OUTPUT:code-gen", "Output:code-gen")]
    [InlineData("-- database: a", "Database:a")]
    [InlineData("--Database:  billing-v2 ", "Database:billing-v2")]
    [InlineData("-- token: {{a:b}}", "Token:{{a:b}}")]
    [InlineData("-- TOKEN: x", "Token:x")]
    [InlineData("--token:", "Token:")]
    [InlineData("-- name:", "Name:")]
    [InlineData("-- name:   ", "Name:")]
    public void Read_MarkerComment_ReturnsItsKindAndValue(string text, string expected) =>
        Markers(text).ShouldBe([expected]);

    [Theory]
    [InlineData("  \t-- name: A")]
    [InlineData("SELECT 1\n-- name: A")]
    [InlineData("SELECT 1\r\n-- name: A")]
    [InlineData("SELECT 1\r-- name: A")]
    [InlineData("SELECT 1\n\t  -- name: A\nSELECT 2")]
    [InlineData("/* x */\n-- name: A")]
    [InlineData("'x'\n-- name: A")]
    public void Read_CommentThatStartsItsLine_IsAMarker(string text) => Markers(text).ShouldBe(["Name:A"]);

    [Theory]
    [InlineData("SELECT 1 -- name: A")]
    [InlineData("/* x */ -- name: A")]
    [InlineData("'x' -- name: A")]
    [InlineData("/*+ h */-- name: A")]
    [InlineData("-- name : A")]
    [InlineData("-- names: A")]
    [InlineData("-- rename: A")]
    [InlineData("-- name A")]
    [InlineData("-- -- name: A")]
    [InlineData("- - name: A")]
    [InlineData("--")]
    [InlineData("-- name")]
    [InlineData("-- a comment")]
    [InlineData("/*\n-- name: A\n*/")]
    [InlineData("'\n-- name: A\n'")]
    [InlineData("$$\n-- name: A\n$$")]
    [InlineData("-- SqlSource: keep-comments")]
    [InlineData("-- generators: keep-comments")]
    [InlineData("-- dialect=mysql")]
    [InlineData("-- dialects: mysql")]
    [InlineData("-- params: @a")]
    [InlineData("-- param @a")]
    [InlineData("-- outputs: x")]
    [InlineData("-- output sql")]
    [InlineData("-- databases: x")]
    [InlineData("SELECT 1 -- database: a")]
    [InlineData("SELECT 1 -- param: @a")]
    [InlineData("-- tokens: x")]
    [InlineData("-- token x")]
    [InlineData("SELECT 1 -- dialect: mysql")]
    public void Read_AnythingElse_IsNotAMarker(string text) => Markers(text).ShouldBeEmpty();

    // A # comment is a line comment in MySQL and MariaDB.  A marker starts with two dashes, there as everywhere.
    [Theory]
    [InlineData("# name: A")]
    [InlineData("#-name: A")]
    [InlineData("## summary: x")]
    [InlineData("#  generator: keep-comments")]
    [InlineData("# dialect: mysql")]
    public void Read_HashComment_IsNotAMarker(string text) => Markers(text, SqlDialectRules.MySql).ShouldBeEmpty();

    // MySQL and MariaDB read two dashes as a comment only when whitespace follows them.
    [Fact]
    public void Read_TwoDashesWithoutWhitespaceInMySql_IsNotAMarker()
    {
        Markers("--name: A\nSELECT 1", SqlDialectRules.MySql).ShouldBeEmpty();
        Markers("-- name: A\nSELECT 1", SqlDialectRules.MySql).ShouldBe(["Name:A"]);
        Markers("--\tname: A\nSELECT 1", SqlDialectRules.MariaDb).ShouldBe(["Name:A"]);
    }

    // Oracle's line hint is kept in the SQL, so it is never a marker.
    [Fact]
    public void Read_LineHintOfOracle_IsNotAMarker() =>
        Markers("--+ name: A\nSELECT 1", SqlDialectRules.Oracle).ShouldBeEmpty();

    [Fact]
    public void Read_Marker_ReportsTheCommentAndTheTrimmedValue()
    {
        const string Text = "SELECT 1\n  -- name:  GetUser  \nSELECT 2";
        var lexeme = SqlLexer.Lex(Text, SqlDialectRules.Ansi).Lexemes[1];

        var marker = SqlMarkerReader.Read(Text, lexeme);

        _ = marker.ShouldNotBeNull();
        marker.Value.Span.ShouldBe(lexeme.Span);
        marker.Value.ValueSpan.ShouldBe(new TextSpan(Text.IndexOf("GetUser", StringComparison.Ordinal), 7));
    }

    [Fact]
    public void Read_MarkerWithoutAValue_HasAnEmptyValueSpan()
    {
        const string Text = "-- name:  ";

        var marker = SqlMarkerReader.Read(Text, SqlLexer.Lex(Text, SqlDialectRules.Ansi).Lexemes[0]);

        _ = marker.ShouldNotBeNull();
        marker.Value.ValueSpan.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void WordOf_EveryKind_IsTheKeywordWithoutItsColon()
    {
        SqlMarkerReader.WordOf(SqlMarkerKind.Token).ShouldBe("token");
        SqlMarkerReader.WordOf(SqlMarkerKind.GeneratorParameters).ShouldBe("generator");
        SqlMarkerReader.WordOf(SqlMarkerKind.Param).ShouldBe("param");
        SqlMarkerReader.WordOf(SqlMarkerKind.Output).ShouldBe("output");
        SqlMarkerReader.WordOf(SqlMarkerKind.Database).ShouldBe("database");
    }

    [Theory]
    [InlineData("-- token: {{a:b}}", "token: {{a:b}}")]
    [InlineData("--  token:  x  ", "token: x")]
    [InlineData("-- token:", "token:")]
    public void Describe_Marker_IsItsWordAndItsValue(string text, string expected)
    {
        var marker = SqlMarkerReader.Read(text, SqlLexer.Lex(text, SqlDialectRules.Ansi).Lexemes[0]);

        _ = marker.ShouldNotBeNull();
        SqlMarkerReader.Describe(text, marker.Value).ShouldBe(expected);
    }

    private static string[] Markers(string text) => Markers(text, SqlDialectRules.Ansi);

    private static string[] Markers(string text, SqlDialectRules rules)
    {
        var lexed = SqlLexer.Lex(text, rules);
        lexed.Error.ShouldBeNull();
        return
        [
            .. lexed
                .Lexemes.Select(lexeme => SqlMarkerReader.Read(text, lexeme))
                .OfType<SqlMarker>()
                .Select(marker => $"{marker.Kind}:{text.Substring(marker.ValueSpan.Start, marker.ValueSpan.Length)}"),
        ];
    }
}
