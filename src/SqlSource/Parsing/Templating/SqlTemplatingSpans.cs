using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis.Text;
using SqlSource.Parsing.Markers;
using SqlSource.Parsing.Tokens;

namespace SqlSource.Parsing.Templating;

/// <summary>
/// Builds the spans of a <see cref="SqlTemplatingPlan" /> from what the parser reads: the file's marker comments,
/// each query's tokens and parameters, and the ends of its queries.
/// </summary>
internal static class SqlTemplatingSpans
{
    /// <summary>
    /// Every line comment of the file in the form of a marker, wherever it stands: the preamble's and the name
    /// markers, which are in no block, too.
    /// </summary>
    public static List<SqlTemplatingSpan> MarkerComments(string text, EquatableArray<SqlLexeme> lexemes) =>
        lexemes
            .Where(lexeme => SqlMarkerReader.HasMarkerForm(text, lexeme))
            .Select(static lexeme => new SqlTemplatingSpan(
                SqlTemplatingSpanKind.MarkerComment,
                lexeme.Span,
                null,
                null
            ))
            .ToList();

    /// <summary>
    /// Adds the end of a query that is not the file's last, right after its last SQL, unless that ends in a
    /// semicolon: a query runs to the next name marker, so it may lack one.
    /// </summary>
    public static void AddQueryEnd(List<SqlTemplatingSpan> spans, string text, TextSpan lastContent)
    {
        if (text[lastContent.End - 1] != ';')
        {
            spans.Add(
                new SqlTemplatingSpan(SqlTemplatingSpanKind.QueryEnd, new TextSpan(lastContent.End, 0), null, null)
            );
        }
    }

    /// <summary>
    /// Adds each token of a query with the default its query resolves for its name, and each parameter of its built
    /// SQL, a parameter inside a token's default too, with whether it is used as a list.  An empty list of uses has
    /// none.
    /// </summary>
    public static void AddQuery(
        List<SqlTemplatingSpan> spans,
        string text,
        SqlBlockText sql,
        EquatableArray<SqlTokenOccurrence> occurrences,
        EquatableArray<SqlToken> tokens,
        ImmutableArray<SqlParameterUse> uses
    )
    {
        foreach (var occurrence in occurrences)
        {
            var token = tokens.First(token => string.Equals(token.Name, occurrence.Name, StringComparison.Ordinal));
            spans.Add(
                new SqlTemplatingSpan(
                    SqlTemplatingSpanKind.Token,
                    sql.ToSourceSpan(occurrence.Span),
                    occurrence.Name,
                    token.Default
                )
            );
        }

        for (var position = 0; position < sql.Parameters.Length; position++)
        {
            var span = sql.ToSourceSpan(sql.Parameters[position]);
            var name = SqlParameterName.Of(span);
            spans.Add(
                new SqlTemplatingSpan(
                    uses.Length > 0 && uses[position] is SqlParameterUse.List or SqlParameterUse.NegatedList
                        ? SqlTemplatingSpanKind.List
                        : SqlTemplatingSpanKind.Parameter,
                    span,
                    text.Substring(name.Start, name.Length),
                    null
                )
            );
        }
    }

    /// <summary>
    /// The spans in the order of the plan: by start, an empty span before others at its start, then by kind.  A
    /// marker comment inside a token is left out, since the token's span protects it.
    /// </summary>
    public static EquatableArray<SqlTemplatingSpan> Order(List<SqlTemplatingSpan> spans)
    {
        var tokens = spans.Where(static span => span.Kind == SqlTemplatingSpanKind.Token).ToList();
        return new EquatableArray<SqlTemplatingSpan>(
            spans
                .Where(span =>
                    span.Kind != SqlTemplatingSpanKind.MarkerComment
                    || !tokens.Exists(token => token.Span.Contains(span.Span))
                )
                .OrderBy(static span => span.Span.Start)
                .ThenBy(static span => span.Span.Length > 0)
                .ThenBy(static span => span.Kind)
                .ToImmutableArray()
        );
    }
}
