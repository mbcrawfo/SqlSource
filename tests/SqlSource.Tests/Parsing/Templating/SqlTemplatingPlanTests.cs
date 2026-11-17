using System;
using System.Linq;
using Microsoft.CodeAnalysis.Text;
using Shouldly;
using SqlSource.Parsing;
using SqlSource.Parsing.Templating;
using Xunit;

namespace SqlSource.Tests.Parsing.Templating;

// The plan of a file that the sqlfluff templater mirrors: what it replaces, where, and the error that stops it.
public sealed class SqlTemplatingPlanTests
{
    [Fact]
    public void Plan_TokenWithInlineDefault_IsOneTokenSpan()
    {
        const string Text = "SELECT {{t:users}};";

        Spans(Text).ShouldBe([(SqlTemplatingSpanKind.Token, "{{t:users}}", "t", "users")]);
    }

    // Both ends are mapped from the built SQL to the file, though the token's default runs over two lines.
    [Fact]
    public void Plan_TokenOverLines_SpansItsLines()
    {
        const string Text = "SELECT 1 /* c */ {{f:AND a = 1\r\n    AND b = 2}}\r\n";

        Spans(Text)
            .ShouldBe([
                (SqlTemplatingSpanKind.Token, "{{f:AND a = 1\r\n    AND b = 2}}", "f", "AND a = 1\n    AND b = 2"),
            ]);
    }

    [Fact]
    public void Plan_TokenDefaultFromMarker_AppliesToItsQueryOnly()
    {
        const string Text =
            "-- name: ByA\n-- token: {{cond:a = 1}}\nSELECT * FROM t WHERE {{cond}};\n\n"
            + "-- name: ByB\nSELECT * FROM t WHERE 1 = 1 {{cond:AND b = 2}};\n";

        Spans(Text)
            .Where(static span => span.Kind == SqlTemplatingSpanKind.Token)
            .ShouldBe([
                (SqlTemplatingSpanKind.Token, "{{cond}}", "cond", "a = 1"),
                (SqlTemplatingSpanKind.Token, "{{cond:AND b = 2}}", "cond", "AND b = 2"),
            ]);
    }

    [Fact]
    public void Plan_IgnoredName_IsNoSpan()
    {
        const string Text = "-- token-ignore: name\nSELECT 'Hello, {{name}}', {{t:users}}";

        Spans(Text)
            .ShouldBe([
                (SqlTemplatingSpanKind.MarkerComment, "-- token-ignore: name", null, null),
                (SqlTemplatingSpanKind.Token, "{{t:users}}", "t", "users"),
            ]);
    }

    [Fact]
    public void Plan_TokenInComment_IsNoSpan()
    {
        const string Text = "SELECT 1 /* {{note}} */ FROM {{t:users}}";

        Spans(Text).ShouldBe([(SqlTemplatingSpanKind.Token, "{{t:users}}", "t", "users")]);
    }

    [Fact]
    public void Plan_UnclosedOpenerInComment_SwallowsNothing()
    {
        const string Text = "SELECT * FROM t -- TODO: add {{filter: ...\nWHERE {{cond:id > 0}}\n";

        Spans(Text).ShouldBe([(SqlTemplatingSpanKind.Token, "{{cond:id > 0}}", "cond", "id > 0")]);
    }

    [Fact]
    public void Plan_ParameterInDefault_IsInsideTheToken()
    {
        const string Text = "-- param: @page int\nSELECT * FROM t ORDER BY id {{paging:LIMIT 20 OFFSET @page}}\n";

        var plan = Plan(Text);

        Spans(Text, plan)
            .ShouldBe([
                (SqlTemplatingSpanKind.MarkerComment, "-- param: @page int", null, null),
                (SqlTemplatingSpanKind.Token, "{{paging:LIMIT 20 OFFSET @page}}", "paging", "LIMIT 20 OFFSET @page"),
                (SqlTemplatingSpanKind.Parameter, "@page", "page", null),
            ]);
        plan.Spans[1].Span.Contains(plan.Spans[2].Span).ShouldBeTrue();
    }

    [Fact]
    public void Plan_List_IsListSpan()
    {
        const string Text = "SELECT * FROM t WHERE id IN @ids AND name = @name";

        Spans(Text)
            .ShouldBe([
                (SqlTemplatingSpanKind.List, "@ids", "ids", null),
                (SqlTemplatingSpanKind.Parameter, "@name", "name", null),
            ]);
    }

    [Fact]
    public void Plan_NegatedList_IsListSpan()
    {
        const string Text = "SELECT * FROM t WHERE id NOT IN @ids";

        Spans(Text).ShouldBe([(SqlTemplatingSpanKind.List, "@ids", "ids", null)]);
    }

    [Fact]
    public void Plan_ListAfterComment_IsListSpan()
    {
        const string Text = "SELECT * FROM t WHERE id IN /* c */ @ids";

        Spans(Text, dialect: SqlDialect.PostgreSql).ShouldBe([(SqlTemplatingSpanKind.List, "@ids", "ids", null)]);
    }

    [Fact]
    public void Plan_Position_IsNoList()
    {
        const string Text = "SELECT * FROM t WHERE position('@' IN @email) > 0";

        Spans(Text, dialect: SqlDialect.PostgreSql)
            .ShouldBe([(SqlTemplatingSpanKind.Parameter, "@email", "email", null)]);
    }

    [Fact]
    public void Plan_SystemVariable_IsNoParameter()
    {
        const string Text = "SELECT @id, @@rowcount";

        Spans(Text, dialect: SqlDialect.SqlServer).ShouldBe([(SqlTemplatingSpanKind.Parameter, "@id", "id", null)]);
    }

    [Fact]
    public void Plan_AtInString_IsNoParameter()
    {
        const string Text = "SELECT 'a@b.c', @id";

        Spans(Text).ShouldBe([(SqlTemplatingSpanKind.Parameter, "@id", "id", null)]);
    }

    // A declared variable is no parameter of the query, but sqlfluff must see it as one where it stands.
    [Fact]
    public void Plan_DeclaredVariable_IsParameterSpan()
    {
        const string Text = "DECLARE @n int = 1;\nSELECT @n";

        Spans(Text, dialect: SqlDialect.SqlServer)
            .ShouldBe([
                (SqlTemplatingSpanKind.Parameter, "@n", "n", null),
                (SqlTemplatingSpanKind.Parameter, "@n", "n", null),
            ]);
    }

    [Theory]
    // The first query has no semicolon: its end is right after its last SQL, before the comment that follows it.
    [InlineData("-- name: A\nSELECT 1 -- note\n\n-- name: B\nSELECT 2\n", "SELECT 1", 1)]
    [InlineData("-- name: A\nSELECT 'a'\n-- name: B\nSELECT 2\n", "SELECT 'a'", 1)]
    // It has one: no end is added.
    [InlineData("-- name: A\nSELECT 1;\n-- name: B\nSELECT 2\n", "SELECT 1", 0)]
    [InlineData("-- name: A\nSELECT 1; -- note\n-- name: B\nSELECT 2;\n", "SELECT 1", 0)]
    // A file with one query has no query after it.
    [InlineData("SELECT 1\n", "SELECT 1", 0)]
    public void Plan_QueryWithoutSemicolon_HasQueryEnd(string text, string lastSql, int expected)
    {
        var ends = Plan(text).Spans.Where(static span => span.Kind == SqlTemplatingSpanKind.QueryEnd).ToArray();

        ends.Length.ShouldBe(expected);
        if (expected == 1)
        {
            ends[0]
                .ShouldBe(
                    new SqlTemplatingSpan(
                        SqlTemplatingSpanKind.QueryEnd,
                        new TextSpan(text.IndexOf(lastSql, StringComparison.Ordinal) + lastSql.Length, 0),
                        null,
                        null
                    )
                );
        }
    }

    [Fact]
    public void Plan_MarkerForm_IsProtected()
    {
        const string Text = "-- name: Q\n-- token: {{a:x}}\n-- just a note\nSELECT {{a}} -- summary: x\n";

        Spans(Text)
            .ShouldBe([
                (SqlTemplatingSpanKind.MarkerComment, "-- name: Q", null, null),
                (SqlTemplatingSpanKind.MarkerComment, "-- token: {{a:x}}", null, null),
                (SqlTemplatingSpanKind.Token, "{{a}}", "a", "x"),
                (SqlTemplatingSpanKind.MarkerComment, "-- summary: x", null, null),
            ]);
    }

    [Fact]
    public void Plan_UnterminatedString_IsError()
    {
        const string Text = "SELECT {{t:users}}, 'abc";

        var plan = Plan(Text);

        plan.Error.ShouldNotBeNull().Kind.ShouldBe(SqlParseErrorKind.UnterminatedQuote);
        plan.Spans.ShouldBeEmpty();
        plan.Lexemes.ShouldBeEmpty();
        plan.HasOtherErrors.ShouldBeFalse();
    }

    [Fact]
    public void Plan_InvalidDialectMarker_IsError()
    {
        const string Text = "-- dialect: nonsense\nSELECT {{t:users}}";

        var plan = Plan(Text);

        plan.Error.ShouldBe(
            SqlParseError.Create(
                SqlParseErrorKind.InvalidMarkerValue,
                TextSpans.Of(Text, "nonsense"),
                "dialect: nonsense",
                string.Empty
            )
        );
        plan.Spans.ShouldBeEmpty();
        plan.HasOtherErrors.ShouldBeFalse();
    }

    // The marker comes first in the file, so its error is the one that stops the plan.
    [Fact]
    public void Plan_InvalidDialectMarkerBeforeUnterminatedString_IsTheMarkersError()
    {
        const string Text = "-- dialect: nonsense\nSELECT 'abc";

        var plan = Plan(Text);

        plan.Error.ShouldNotBeNull().Kind.ShouldBe(SqlParseErrorKind.InvalidMarkerValue);
        plan.HasOtherErrors.ShouldBeTrue();
    }

    [Fact]
    public void Plan_CommentHoldingCloseBraces_IsSqlsrc120()
    {
        const string Text = "-- name: Q\nSELECT 1 {{f:AND a = 1 -- note}}\nORDER BY {{o:name}}\n";

        var plan = Plan(Text);

        plan.Error.ShouldBe(
            SqlParseError.Create(SqlParseErrorKind.TokenCrossesComment, TextSpans.Of(Text, "-- note}}"), "f")
        );
        plan.Spans.ShouldBeEmpty();
        plan.Lexemes.ShouldNotBeEmpty();
    }

    [Fact]
    public void Plan_OtherError_IsBestEffort()
    {
        const string Text = "-- name: Q\nSELECT {{t:users}}\n-- name: Q\nSELECT @id\n";

        var plan = Plan(Text);

        plan.Error.ShouldBeNull();
        plan.HasOtherErrors.ShouldBeTrue();
        Spans(Text, plan)
            .ShouldBe([
                (SqlTemplatingSpanKind.MarkerComment, "-- name: Q", null, null),
                (SqlTemplatingSpanKind.Token, "{{t:users}}", "t", "users"),
                (SqlTemplatingSpanKind.QueryEnd, string.Empty, null, null),
                (SqlTemplatingSpanKind.MarkerComment, "-- name: Q", null, null),
                (SqlTemplatingSpanKind.Parameter, "@id", "id", null),
            ]);
    }

    [Fact]
    public void Plan_AcceptedFile_HasNoErrors()
    {
        var plan = Plan("-- name: Q\nSELECT {{t:users}}\n");

        plan.Error.ShouldBeNull();
        plan.HasOtherErrors.ShouldBeFalse();
    }

    [Fact]
    public void Plan_DialectMarker_IsTheResolvedDialect()
    {
        const string Text = "-- dialect: mysql,ansi_quotes\nSELECT \"a\"";

        var plan = Plan(Text);

        plan.Dialect.ShouldBe(new SqlDialectChoice(SqlDialect.MySql, SqlDialectOptions.AnsiQuotes));
        plan.Lexemes.ShouldContain(new SqlLexeme(SqlLexemeKind.Quoted, TextSpans.Of(Text, "\"a\"")));
    }

    [Fact]
    public void Plan_WithoutDialectMarker_IsTheGivenDialect()
    {
        var given = new SqlDialectChoice(SqlDialect.MariaDb, SqlDialectOptions.NoBackslashEscapes);

        Plan("SELECT 1", given).Dialect.ShouldBe(given);
    }

    [Fact]
    public void Plan_Lexemes_AreTheFilesLexemes()
    {
        const string Text = "-- name: Q\nSELECT 1\n";

        Plan(Text).Lexemes.ShouldBe(SqlLexer.Lex(Text, SqlDialectRules.Ansi).Lexemes);
    }

    private static SqlTemplatingPlan Plan(string text, SqlDialectChoice dialect = default) =>
        SqlFileParser.PlanTemplating(text, "Query.sql", dialect);

    private static (SqlTemplatingSpanKind Kind, string Text, string? Name, string? Default)[] Spans(
        string text,
        SqlDialectChoice dialect = default
    ) => Spans(text, Plan(text, dialect));

    private static (SqlTemplatingSpanKind Kind, string Text, string? Name, string? Default)[] Spans(
        string text,
        SqlTemplatingPlan plan
    ) =>
        [
            .. plan.Spans.Select(span =>
                (span.Kind, text.Substring(span.Span.Start, span.Span.Length), span.Name, span.Default)
            ),
        ];
}
