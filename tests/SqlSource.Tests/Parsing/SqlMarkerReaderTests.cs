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
    [InlineData("-- SqlSource: preserve-comments  token-ignore=a", "Directives:preserve-comments  token-ignore=a")]
    [InlineData("-- SQLSOURCE: x", "Directives:x")]
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
    public void Read_AnythingElse_IsNotAMarker(string text) => Markers(text).ShouldBeEmpty();

    [Fact]
    public void Read_Marker_ReportsTheCommentAndTheTrimmedValue()
    {
        const string Text = "SELECT 1\n  -- name:  GetUser  \nSELECT 2";
        var lexeme = SqlLexer.Lex(Text).Lexemes[1];

        var marker = SqlMarkerReader.Read(Text, lexeme);

        _ = marker.ShouldNotBeNull();
        marker.Value.Span.ShouldBe(lexeme.Span);
        marker.Value.ValueSpan.ShouldBe(new TextSpan(Text.IndexOf("GetUser", StringComparison.Ordinal), 7));
    }

    [Fact]
    public void Read_MarkerWithoutAValue_HasAnEmptyValueSpan()
    {
        const string Text = "-- name:  ";

        var marker = SqlMarkerReader.Read(Text, SqlLexer.Lex(Text).Lexemes[0]);

        _ = marker.ShouldNotBeNull();
        marker.Value.ValueSpan.IsEmpty.ShouldBeTrue();
    }

    private static string[] Markers(string text)
    {
        var lexed = SqlLexer.Lex(text);
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
