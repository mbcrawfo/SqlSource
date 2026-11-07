using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis.Text;

namespace SqlSource.Parsing.Tokens;

/// <summary>
/// Splits the literal segments of a query's SQL at the uses of its lists, so that each use is a segment of its own.
/// </summary>
/// <remarks>
/// A use's segment holds the parameter as the SQL writes it, so the segments joined are the same text as before, and
/// the hash, which reads a use as that text, is the same.  A parameter inside a token is part of the token's segment,
/// and is never split out: a list cannot stand there.
/// </remarks>
internal static class SqlListSegments
{
    /// <summary>
    /// The segments with each use of a list, and each null test of one, as a segment of its own.  The segments
    /// themselves, and nothing allocated, when no parameter is a list.
    /// </summary>
    /// <param name="segments">The SQL as literal text and tokens, as <see cref="TokenScanner" /> gives it.</param>
    /// <param name="occurrences">Each token of <paramref name="segments" />, in order, with its place.</param>
    /// <param name="sql">
    /// The SQL that <paramref name="segments" /> were scanned from, with its parameters' places.
    /// </param>
    /// <param name="uses">
    /// What each parameter of <paramref name="sql" /> is used as, in its order, or empty when no use is a list.
    /// </param>
    /// <param name="places">
    /// Where each use stands, in the order of <paramref name="uses" />, or empty when they were not read.  A list that
    /// no <c>NOT</c> reaches is written empty as one <c>NULL</c> when they were, and every list keeps the subquery when
    /// they were not.
    /// </param>
    /// <param name="parameters">The query's parameters.</param>
    public static EquatableArray<SqlSegment> Split(
        EquatableArray<SqlSegment> segments,
        EquatableArray<SqlTokenOccurrence> occurrences,
        SqlBlockText sql,
        ImmutableArray<SqlParameterUse> uses,
        ImmutableArray<SqlListPlace> places,
        EquatableArray<SqlQueryParameter> parameters
    )
    {
        if (uses.IsEmpty || !HasList(parameters))
        {
            return segments;
        }

        // Created at the first use that is split, with the segments before it.
        ImmutableArray<SqlSegment>.Builder? split = null;

        // Where the segment starts in the SQL: a literal is its text, and a token runs to the end of its occurrence.
        var offset = 0;
        var token = 0;
        var position = 0;
        for (var index = 0; index < segments.Count; index++)
        {
            var segment = segments[index];
            switch (segment.Kind)
            {
                case SqlSegmentKind.Literal:
                    break;
                case SqlSegmentKind.Token:
                    offset = occurrences[token].Span.End;
                    token++;
                    split?.Add(segment);
                    continue;

                // A use already split is kept as it is.
                case SqlSegmentKind.List:
                case SqlSegmentKind.ListNullTest:
                    offset += segment.Text.Length;
                    split?.Add(segment);
                    continue;
                default:
                    throw UnexpectedEnumValueException.Of(segment.Kind);
            }

            var end = offset + segment.Text.Length;

            // The parameters of the tokens before the literal are passed over.
            while (position < sql.Parameters.Length && sql.Parameters[position].Start < offset)
            {
                position++;
            }

            // The start of the literal's text that is not yet in a segment.
            var rest = offset;
            for (; position < sql.Parameters.Length && sql.Parameters[position].Start < end; position++)
            {
                var span = sql.Parameters[position];
                if (KindOf(uses[position], span, sql.Text, parameters) is not { } kind)
                {
                    continue;
                }

                if (split is null)
                {
                    split = ImmutableArray.CreateBuilder<SqlSegment>(segments.Count + 2);
                    for (var before = 0; before < index; before++)
                    {
                        split.Add(segments[before]);
                    }
                }

                AddLiteral(split, sql.Text, rest, span.Start);
                var emptyIsNull =
                    kind == SqlSegmentKind.List
                    && !places.IsEmpty
                    && (places[position] & SqlListPlace.UnderNot) == SqlListPlace.None;
                split.Add(new SqlSegment(kind, sql.Text.Substring(span.Start, span.Length), emptyIsNull));
                rest = span.End;
            }

            if (rest == offset)
            {
                split?.Add(segment);
            }
            else
            {
                AddLiteral(split!, sql.Text, rest, end);
            }

            offset = end;
        }

        return split is null ? segments : new EquatableArray<SqlSegment>(split.ToImmutable());
    }

    private static bool HasList(EquatableArray<SqlQueryParameter> parameters)
    {
        foreach (var parameter in parameters)
        {
            if (parameter.IsList)
            {
                return true;
            }
        }

        return false;
    }

    // The kind of the segment of a use, or null for a use that stays in its literal: a value, or a use of a parameter
    // that is no list, such as a name the SQL declares as a local variable, which the list leaves out.
    private static SqlSegmentKind? KindOf(
        SqlParameterUse use,
        TextSpan span,
        string text,
        EquatableArray<SqlQueryParameter> parameters
    )
    {
        SqlSegmentKind kind;
        switch (use)
        {
            case SqlParameterUse.Value:
                return null;
            case SqlParameterUse.List:
            case SqlParameterUse.NegatedList:
                kind = SqlSegmentKind.List;
                break;
            case SqlParameterUse.NullTest:
                kind = SqlSegmentKind.ListNullTest;
                break;
            default:
                throw UnexpectedEnumValueException.Of(use);
        }

        var name = SqlParameterName.Of(span);
        var index = SqlParameterName.IndexOf(parameters, text.AsSpan(name.Start, name.Length));
        return index >= 0 && parameters[index].IsList ? kind : null;
    }

    private static void AddLiteral(ImmutableArray<SqlSegment>.Builder segments, string text, int start, int end)
    {
        if (end > start)
        {
            segments.Add(new SqlSegment(SqlSegmentKind.Literal, text.Substring(start, end - start)));
        }
    }
}
