using System;
using System.Linq;
using System.Text;
using Shouldly;
using SqlSource.Generation;
using SqlSource.Parsing;
using Xunit;

namespace SqlSource.Tests.Generation;

public class MethodWriterTests
{
    [Fact]
    public void Append_SeveralTokensWithOneRepeated_WritesTheDocumentationAndTheMethod()
    {
        var builder = new StringBuilder("before\n");

        MethodWriter.Append(
            builder,
            "    ",
            "Lists <c>rows</c>.",
            "ListFrom",
            TestModels.Array(
                Text("SELECT id FROM "),
                Token("table"),
                Text(" WHERE "),
                Token("filter"),
                Text(" ORDER BY "),
                Token("table"),
                Text(".id;")
            ),
            validate: true
        );

        builder
            .ToString()
            .ShouldBe(
                """
                before
                    /// <summary>
                    /// Lists <c>rows</c>.
                    /// </summary>
                    /// <remarks>
                    /// <code>
                    /// SELECT id FROM {{table}} WHERE {{filter}} ORDER BY {{table}}.id;
                    /// </code>
                    /// </remarks>
                    /// <param name="table">The text that replaces <c>{{table}}</c>.</param>
                    /// <param name="filter">The text that replaces <c>{{filter}}</c>.</param>
                    /// <returns>The SQL with each token replaced by its argument.</returns>
                    public static string ListFrom(string table, string filter)
                    {
                        global::System.ArgumentException.ThrowIfNullOrWhiteSpace(table);
                        global::System.ArgumentException.ThrowIfNullOrWhiteSpace(filter);
                        return string.Create(
                            checked(36 + table.Length * 2 + filter.Length),
                            (table, filter),
                            static (span, state) =>
                            {
                                "SELECT id FROM ".CopyTo(span);
                                span = span.Slice(15);
                                state.Item1.CopyTo(span);
                                span = span.Slice(state.Item1.Length);
                                " WHERE ".CopyTo(span);
                                span = span.Slice(7);
                                state.Item2.CopyTo(span);
                                span = span.Slice(state.Item2.Length);
                                " ORDER BY ".CopyTo(span);
                                span = span.Slice(10);
                                state.Item1.CopyTo(span);
                                span = span.Slice(state.Item1.Length);
                                ".id;".CopyTo(span);
                            }
                        );
                    }

                """
            );
    }

    [Fact]
    public void Append_ValidationOff_WritesNoCheckAndTheSameMethodOtherwise()
    {
        SqlSegment[] segments = [Text("SELECT * FROM "), Token("table"), Text(" WHERE "), Token("filter")];

        var validated = Write(validate: true, segments);
        var unvalidated = Write(validate: false, segments);

        unvalidated.ShouldNotContain("Throw");
        validated.ShouldBe(
            unvalidated.Replace(
                "{\n    return",
                "{\n    global::System.ArgumentException.ThrowIfNullOrWhiteSpace(table);\n"
                    + "    global::System.ArgumentException.ThrowIfNullOrWhiteSpace(filter);\n    return",
                StringComparison.Ordinal
            )
        );
    }

    [Fact]
    public void Append_OneToken_PassesTheParameterItselfAsTheState() =>
        Body(Text("SELECT * FROM "), Token("table"), Text(";"))
            .ShouldBe(
                """
                public static string Q(string table)
                {
                    return string.Create(
                        checked(15 + table.Length),
                        table,
                        static (span, state) =>
                        {
                            "SELECT * FROM ".CopyTo(span);
                            span = span.Slice(14);
                            state.CopyTo(span);
                            span = span.Slice(state.Length);
                            ";".CopyTo(span);
                        }
                    );
                }

                """
            );

    [Fact]
    public void Append_TokenAlone_HasNoLiteralLengthAndNoSlice() =>
        Body(Token("sql"))
            .ShouldBe(
                """
                public static string Q(string sql)
                {
                    return string.Create(
                        sql.Length,
                        sql,
                        static (span, state) =>
                        {
                            state.CopyTo(span);
                        }
                    );
                }

                """
            );

    [Fact]
    public void Append_TokensFirstLastAndAdjacent_CopiesEachSegmentInOrder() =>
        Body(Token("a"), Token("b"), Text(" x "), Token("a"), Token("a"))
            .ShouldBe(
                """
                public static string Q(string a, string b)
                {
                    return string.Create(
                        checked(3 + a.Length * 3 + b.Length),
                        (a, b),
                        static (span, state) =>
                        {
                            state.Item1.CopyTo(span);
                            span = span.Slice(state.Item1.Length);
                            state.Item2.CopyTo(span);
                            span = span.Slice(state.Item2.Length);
                            " x ".CopyTo(span);
                            span = span.Slice(3);
                            state.Item1.CopyTo(span);
                            span = span.Slice(state.Item1.Length);
                            state.Item1.CopyTo(span);
                        }
                    );
                }

                """
            );

    // A sum or a product is checked, so that a result too long for a string is an OverflowException and never a
    // shorter string.  One length by itself cannot overflow.
    [Fact]
    public void Append_OneTokenUsedTwice_ChecksTheProduct() =>
        Body(Token("a"), Token("a")).ShouldContain("        checked(a.Length * 2),\n");

    [Fact]
    public void Append_OnlyTokens_SumsTheirLengthsWithoutALeadingNumber() =>
        Body(Token("a"), Token("b")).ShouldContain("        checked(a.Length + b.Length),\n");

    [Fact]
    public void Append_EightTokens_ReadsTheEighthByPosition()
    {
        string[] names = ["a", "b", "c", "d", "e", "f", "g", "h"];

        var body = Body([.. names.Select(Token)]);

        body.ShouldStartWith(
            "public static string Q(string a, string b, string c, string d, string e, string f, string g, string h)\n"
        );
        body.ShouldContain("        (a, b, c, d, e, f, g, h),\n");
        body.ShouldContain(
            "            state.Item7.CopyTo(span);\n            span = span.Slice(state.Item7.Length);\n"
        );
        body.ShouldContain("            state.Item8.CopyTo(span);\n        }\n");
    }

    [Fact]
    public void Append_TokensNamedLikeTheLambdaParameters_GivesTheLambdaParametersOtherNames() =>
        Body(Token("span"), Token("state"), Token("span1"))
            .ShouldBe(
                """
                public static string Q(string span, string state, string span1)
                {
                    return string.Create(
                        checked(span.Length + state.Length + span1.Length),
                        (span, state, span1),
                        static (span2, state1) =>
                        {
                            state1.Item1.CopyTo(span2);
                            span2 = span2.Slice(state1.Item1.Length);
                            state1.Item2.CopyTo(span2);
                            span2 = span2.Slice(state1.Item2.Length);
                            state1.Item3.CopyTo(span2);
                        }
                    );
                }

                """
            );

    [Fact]
    public void Append_OneTokenNamedState_ReadsTheStateByItsOtherName()
    {
        var body = Body(Token("state"), Text(";"));

        body.ShouldContain("        state,\n        static (span, state1) =>\n");
        body.ShouldContain("            state1.CopyTo(span);\n            span = span.Slice(state1.Length);\n");
    }

    [Fact]
    public void Append_TokenNamedLikeATupleElement_IsStillReadByItsOwnPosition()
    {
        // "Item2" is the name of the second element of every tuple, so it cannot name the first.
        var body = Body(Token("Item2"), Token("Rest"));

        body.ShouldContain("        (Item2, Rest),\n");
        body.ShouldContain(
            "            state.Item1.CopyTo(span);\n            span = span.Slice(state.Item1.Length);\n"
        );
        body.ShouldContain("            state.Item2.CopyTo(span);\n        }\n");
    }

    [Fact]
    public void Append_LiteralThatNeedsEscaping_EscapesItAndCountsItsCharacters()
    {
        // 23 characters: the line feed, the tab, the backslash and each quote are one character each.
        var body = Body(Text("SELECT \"a\",\n'\t', '\\' < "), Token("max"));

        body.ShouldContain("        checked(23 + max.Length),\n");
        body.ShouldContain("            \"SELECT \\\"a\\\",\\n'\\t', '\\\\' < \".CopyTo(span);\n");
        body.ShouldContain("            span = span.Slice(23);\n");
    }

    [Fact]
    public void Append_SqlOverSeveralLinesWithCharactersXmlReserves_DocumentsItLineByLineAndEscaped()
    {
        var text = Write(validate: false, Text("SELECT id\nFROM "), Token("table"), Text("\nWHERE id < @max;"));

        text.ShouldContain(
            "/// <code>\n/// SELECT id\n/// FROM {{table}}\n/// WHERE id &lt; @max;\n/// </code>\n/// </remarks>\n"
                + "/// <param name=\"table\">The text that replaces <c>{{table}}</c>.</param>\n"
                + "/// <returns>The SQL with each token replaced by its argument.</returns>\n"
                + "public static string Q(string table)\n"
        );
    }

    private static SqlSegment Text(string text) => new(SqlSegmentKind.Literal, text);

    private static SqlSegment Token(string name) => new(SqlSegmentKind.Token, name);

    private static string Write(bool validate, params SqlSegment[] segments)
    {
        var builder = new StringBuilder();
        MethodWriter.Append(builder, string.Empty, "S", "Q", TestModels.Array(segments), validate);
        return builder.ToString();
    }

    // The method without its documentation comment, written with no validation.
    private static string Body(params SqlSegment[] segments)
    {
        var text = Write(validate: false, segments);
        return text[text.IndexOf("public static", StringComparison.Ordinal)..];
    }
}
