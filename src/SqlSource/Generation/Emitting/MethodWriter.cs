using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Microsoft.CodeAnalysis.CSharp;
using SqlSource.Generation.Placement;
using SqlSource.Parsing.Tokens;
using SqlSource.Settings;

namespace SqlSource.Generation.Emitting;

/// <summary>
/// Writes the method of a query that has tokens: one <c>string</c> parameter for each token, and a body that builds
/// the SQL with <c>string.Create</c>, which allocates the result and nothing else.
/// </summary>
/// <remarks>
/// Any identifier that is not a reserved keyword can be a token name, and so a parameter name.  Nothing the method
/// writes may be an identifier that a parameter can hide: the state is read by position, the lambda's parameters get
/// names no token has, and a framework type is written from <c>global::</c>.
/// </remarks>
internal static class MethodWriter
{
    private const string SpanName = "span";

    private const string StateName = "state";

    /// <summary>
    /// Appends the documentation comment and the method.
    /// </summary>
    /// <param name="builder">The source being built.</param>
    /// <param name="indent">The whitespace that starts each line of the member.</param>
    /// <param name="summaryXml">The summary as XML: already escaped, and free to hold tags.</param>
    /// <param name="name">The query's name, which is the method's.</param>
    /// <param name="segments">The query's SQL.  It holds at least one token.</param>
    /// <param name="validate">Whether the method checks that each argument has text.</param>
    /// <param name="accessibility">The method's accessibility.</param>
    public static void Append(
        StringBuilder builder,
        string indent,
        string summaryXml,
        string name,
        EquatableArray<SqlSegment> segments,
        bool validate,
        Visibility accessibility
    )
    {
        var parameters = GetTokens(segments);
        var bodyIndent = indent + GeneratedText.Indent;
        var argumentIndent = bodyIndent + GeneratedText.Indent;
        var span = GetUnusedName(SpanName, parameters);
        var state = GetUnusedName(StateName, parameters);

        XmlDocWriter.AppendMember(builder, indent, summaryXml, GetSql(segments));
        foreach (var parameter in parameters)
        {
            XmlDocWriter.AppendParam(builder, indent, parameter);
        }

        XmlDocWriter.AppendReturns(builder, indent);

        _ = builder
            .Append(indent)
            .Append(PlacementChecks.KeywordOf(accessibility))
            .Append(" static string ")
            .Append(name)
            .Append('(');
        AppendList(builder, parameters, "string ");
        _ = builder.Append(")\n").Append(indent).Append("{\n");

        if (validate)
        {
            foreach (var parameter in parameters)
            {
                _ = builder
                    .Append(bodyIndent)
                    .Append("global::System.ArgumentException.ThrowIfNullOrWhiteSpace(")
                    .Append(parameter)
                    .Append(");\n");
            }
        }

        _ = builder.Append(bodyIndent).Append("return ");
        AppendCreate(
            builder,
            argumentIndent,
            bodyIndent,
            segments,
            parameters,
            parameters,
            span,
            state,
            expandsLists: false
        );
        _ = builder.Append(";\n").Append(indent).Append("}\n");
    }

    /// <summary>
    /// Appends the <c>string.Create(...)</c> expression that builds the SQL from the token arguments, without the
    /// statement around it.  The token method returns it, and a query's method keeps it in a local.
    /// </summary>
    /// <param name="builder">The source being built.</param>
    /// <param name="argumentIndent">The whitespace that starts each argument of <c>string.Create</c>.</param>
    /// <param name="bodyIndent">The whitespace that starts the line of the closing parenthesis.</param>
    /// <param name="segments">The query's SQL.  It holds at least one token.</param>
    /// <param name="tokens">The token names, as <see cref="GetTokens" /> gives them.</param>
    /// <param name="arguments">
    /// How each token's argument is written, in the order of <paramref name="tokens" />: its name, or <c>@await</c>
    /// in an asynchronous method, where <c>await</c> cannot be an identifier.
    /// </param>
    /// <param name="span">The name of the lambda's span, which no token has.</param>
    /// <param name="state">The name of the lambda's state, which no token has.</param>
    /// <param name="expandsLists">
    /// Whether each use of a list is written as the list's values.  When false it is written as the SQL writes it, as
    /// a token method does, whose caller binds no list.
    /// </param>
    internal static void AppendCreate(
        StringBuilder builder,
        string argumentIndent,
        string bodyIndent,
        EquatableArray<SqlSegment> segments,
        List<string> tokens,
        List<string> arguments,
        string span,
        string state,
        bool expandsLists
    )
    {
        var stepIndent = argumentIndent + GeneratedText.Indent;
        _ = builder.Append("string.Create(\n").Append(argumentIndent);
        AppendLength(builder, segments, tokens, arguments, expandsLists);
        _ = builder.Append(",\n").Append(argumentIndent);
        if (arguments.Count == 1)
        {
            _ = builder.Append(arguments[0]);
        }
        else
        {
            _ = builder.Append('(');
            AppendList(builder, arguments, string.Empty);
            _ = builder.Append(')');
        }

        _ = builder
            .Append(",\n")
            .Append(argumentIndent)
            .Append("static (")
            .Append(span)
            .Append(", ")
            .Append(state)
            .Append(") =>\n")
            .Append(argumentIndent)
            .Append("{\n");
        AppendSteps(builder, stepIndent, segments, tokens, span, state, expandsLists);
        _ = builder.Append(argumentIndent).Append("}\n").Append(bodyIndent).Append(')');
    }

    /// <summary>
    /// The distinct token names, in the order of their first appearance, which is the order of the arguments.  A query
    /// has a handful of tokens, so a list that is searched is cheaper than a set.
    /// </summary>
    internal static List<string> GetTokens(EquatableArray<SqlSegment> segments)
    {
        var parameters = new List<string>();
        foreach (var segment in segments)
        {
            if (segment.Kind == SqlSegmentKind.Token && !parameters.Contains(segment.Text))
            {
                parameters.Add(segment.Text);
            }
        }

        return parameters;
    }

    /// <summary>
    /// The SQL as the documentation and a constant show it, with each token in its plain form and each use of a list as
    /// the SQL writes it.
    /// </summary>
    internal static string GetSql(EquatableArray<SqlSegment> segments)
    {
        // Without a token or a list the SQL is one literal segment, which needs no copy.
        if (segments.Count == 1 && segments[0].Kind == SqlSegmentKind.Literal)
        {
            return segments[0].Text;
        }

        var sql = new StringBuilder();
        foreach (var segment in segments)
        {
            _ = segment.Kind switch
            {
                SqlSegmentKind.Literal or SqlSegmentKind.List or SqlSegmentKind.ListNullTest => sql.Append(
                    segment.Text
                ),
                SqlSegmentKind.Token => sql.Append("{{").Append(segment.Text).Append("}}"),
                _ => throw UnexpectedEnumValueException.Of(segment.Kind),
            };
        }

        return sql.ToString();
    }

    // The name with the first number, from 1, that makes it differ from every parameter.
    private static string GetUnusedName(string name, List<string> parameters)
    {
        var candidate = name;
        var suffix = 0;
        while (parameters.Contains(candidate))
        {
            suffix++;
            candidate = name + suffix.ToString(CultureInfo.InvariantCulture);
        }

        return candidate;
    }

    private static void AppendList(StringBuilder builder, List<string> parameters, string prefix)
    {
        for (var index = 0; index < parameters.Count; index++)
        {
            _ = builder.Append(index > 0 ? ", " : string.Empty).Append(prefix).Append(parameters[index]);
        }
    }

    // The length of the result: the literal text as one number, then each parameter's length, times how often its
    // token appears.  A sum or a product is checked: unchecked, a total above the largest int wraps around, and one
    // that wraps to zero or a small number would give a short string and not an exception.
    private static void AppendLength(
        StringBuilder builder,
        EquatableArray<SqlSegment> segments,
        List<string> parameters,
        List<string> arguments,
        bool expandsLists
    )
    {
        var literalLength = 0;
        var uses = new int[parameters.Count];
        foreach (var segment in segments)
        {
            if (IsText(segment.Kind, expandsLists))
            {
                literalLength += segment.Text.Length;
            }
            else if (segment.Kind == SqlSegmentKind.Token)
            {
                uses[parameters.IndexOf(segment.Text)]++;
            }
            else
            {
                throw ExpansionNotWritten();
            }
        }

        // One length by itself cannot overflow.
        var isChecked = literalLength > 0 || parameters.Count > 1 || uses[0] > 1;
        if (isChecked)
        {
            _ = builder.Append("checked(");
        }

        if (literalLength > 0)
        {
            _ = builder.Append(literalLength.ToString(CultureInfo.InvariantCulture));
        }

        for (var index = 0; index < parameters.Count; index++)
        {
            _ = builder
                .Append(literalLength > 0 || index > 0 ? " + " : string.Empty)
                .Append(arguments[index])
                .Append(".Length");
            if (uses[index] > 1)
            {
                _ = builder.Append(" * ").Append(uses[index].ToString(CultureInfo.InvariantCulture));
            }
        }

        if (isChecked)
        {
            _ = builder.Append(')');
        }
    }

    // One copy for each token and for each run of text between two, and after each but the last a slice that moves
    // the span past what was copied.  A use of a list that is written as text is part of the run it stands in, so the
    // steps are those of the same SQL without a list.
    private static void AppendSteps(
        StringBuilder builder,
        string indent,
        EquatableArray<SqlSegment> segments,
        List<string> parameters,
        string span,
        string state,
        bool expandsLists
    )
    {
        var index = 0;
        while (index < segments.Count)
        {
            var segment = segments[index];
            string source;
            string length;
            if (IsText(segment.Kind, expandsLists))
            {
                var start = index;
                index++;
                while (index < segments.Count && IsText(segments[index].Kind, expandsLists))
                {
                    index++;
                }

                var text = TextOf(segments, start, index);
                source = SymbolDisplay.FormatLiteral(text, quote: true);
                length = text.Length.ToString(CultureInfo.InvariantCulture);
            }
            else if (segment.Kind == SqlSegmentKind.Token)
            {
                // A tuple of one element cannot be written, so one parameter is the state itself.
                source =
                    parameters.Count == 1
                        ? state
                        : state
                            + ".Item"
                            + (parameters.IndexOf(segment.Text) + 1).ToString(CultureInfo.InvariantCulture);
                length = source + ".Length";
                index++;
            }
            else
            {
                throw ExpansionNotWritten();
            }

            _ = builder.Append(indent).Append(source).Append(".CopyTo(").Append(span).Append(");\n");
            if (index < segments.Count)
            {
                _ = builder
                    .Append(indent)
                    .Append(span)
                    .Append(" = ")
                    .Append(span)
                    .Append(".Slice(")
                    .Append(length)
                    .Append(");\n");
            }
        }
    }

    // Whether a segment is written as its text.  A list's use is, unless the list is expanded, and a null test always
    // is: a query method sends a parameter of the list's name for it.
    private static bool IsText(SqlSegmentKind kind, bool expandsLists) =>
        kind switch
        {
            SqlSegmentKind.Literal or SqlSegmentKind.ListNullTest => true,
            SqlSegmentKind.List => !expandsLists,
            SqlSegmentKind.Token => false,
            _ => throw UnexpectedEnumValueException.Of(kind),
        };

    // The text of the segments from start up to end, one segment's own when there is one.
    private static string TextOf(EquatableArray<SqlSegment> segments, int start, int end)
    {
        if (end == start + 1)
        {
            return segments[start].Text;
        }

        var text = new StringBuilder();
        for (var index = start; index < end; index++)
        {
            _ = text.Append(segments[index].Text);
        }

        return text.ToString();
    }

    // Nothing asks for a list to be expanded yet.
    private static NotSupportedException ExpansionNotWritten() =>
        new("The expansion of a list is not written yet: every caller writes a list as its text.");
}
