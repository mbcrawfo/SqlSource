using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Microsoft.CodeAnalysis.CSharp;
using SqlSource.Parsing;

namespace SqlSource.Generation;

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
    private const string Indent = "    ";

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
    public static void Append(
        StringBuilder builder,
        string indent,
        string summaryXml,
        string name,
        EquatableArray<SqlSegment> segments,
        bool validate
    )
    {
        var parameters = GetParameters(segments);
        var bodyIndent = indent + Indent;
        var argumentIndent = bodyIndent + Indent;
        var stepIndent = argumentIndent + Indent;
        var span = GetUnusedName(SpanName, parameters);
        var state = GetUnusedName(StateName, parameters);

        XmlDocWriter.AppendMember(builder, indent, summaryXml, GetSql(segments));
        foreach (var parameter in parameters)
        {
            XmlDocWriter.AppendParam(builder, indent, parameter);
        }

        XmlDocWriter.AppendReturns(builder, indent);

        _ = builder.Append(indent).Append("public static string ").Append(name).Append('(');
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

        _ = builder.Append(bodyIndent).Append("return string.Create(\n").Append(argumentIndent);
        AppendLength(builder, segments, parameters);
        _ = builder.Append(",\n").Append(argumentIndent);
        if (parameters.Count == 1)
        {
            _ = builder.Append(parameters[0]);
        }
        else
        {
            _ = builder.Append('(');
            AppendList(builder, parameters, string.Empty);
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
        AppendSteps(builder, stepIndent, segments, parameters, span, state);
        _ = builder.Append(argumentIndent).Append("}\n").Append(bodyIndent).Append(");\n").Append(indent).Append("}\n");
    }

    // One parameter for each distinct token name, in the order of first appearance.  A query has a handful of tokens,
    // so a list that is searched is cheaper than a set.
    private static List<string> GetParameters(EquatableArray<SqlSegment> segments)
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

    // The SQL as the documentation shows it, with each token in its plain form.
    private static string GetSql(EquatableArray<SqlSegment> segments)
    {
        var sql = new StringBuilder();
        foreach (var segment in segments)
        {
            _ =
                segment.Kind == SqlSegmentKind.Token
                    ? sql.Append("{{").Append(segment.Text).Append("}}")
                    : sql.Append(segment.Text);
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
        List<string> parameters
    )
    {
        var literalLength = 0;
        var uses = new int[parameters.Count];
        foreach (var segment in segments)
        {
            if (segment.Kind == SqlSegmentKind.Token)
            {
                uses[parameters.IndexOf(segment.Text)]++;
            }
            else
            {
                literalLength += segment.Text.Length;
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
                .Append(parameters[index])
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

    // One copy for each segment, and after each but the last a slice that moves the span past what was copied.
    private static void AppendSteps(
        StringBuilder builder,
        string indent,
        EquatableArray<SqlSegment> segments,
        List<string> parameters,
        string span,
        string state
    )
    {
        for (var index = 0; index < segments.Count; index++)
        {
            var segment = segments[index];
            string source;
            string length;
            if (segment.Kind == SqlSegmentKind.Token)
            {
                // A tuple of one element cannot be written, so one parameter is the state itself.
                source =
                    parameters.Count == 1
                        ? state
                        : state
                            + ".Item"
                            + (parameters.IndexOf(segment.Text) + 1).ToString(CultureInfo.InvariantCulture);
                length = source + ".Length";
            }
            else
            {
                source = SymbolDisplay.FormatLiteral(segment.Text, quote: true);
                length = segment.Text.Length.ToString(CultureInfo.InvariantCulture);
            }

            _ = builder.Append(indent).Append(source).Append(".CopyTo(").Append(span).Append(");\n");
            if (index < segments.Count - 1)
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
}
