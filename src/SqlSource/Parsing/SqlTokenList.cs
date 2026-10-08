using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace SqlSource.Parsing;

/// <summary>
/// Builds the tokens of one query: each name once, in order of first appearance, with the default its query gives.
/// </summary>
internal static class SqlTokenList
{
    /// <summary>
    /// Two defaults for one token that differ are a conflict, reported at the one that comes second in the file.
    /// A marker for a token the query does not hold is not an error.
    /// </summary>
    public static EquatableArray<SqlToken> Create(
        string text,
        SqlBlockText sql,
        EquatableArray<SqlTokenOccurrence> occurrences,
        IReadOnlyList<SqlTokenDefault> markerDefaults,
        List<SqlParseError> errors
    )
    {
        if (occurrences.Count == 0)
        {
            return EquatableArray<SqlToken>.Empty;
        }

        // Nothing but the result is allocated for a query whose defaults agree: the distinct names are counted first,
        // so that the builder is the size of the result.
        var distinct = 0;
        for (var index = 0; index < occurrences.Count; index++)
        {
            if (!AppearedBefore(occurrences, index))
            {
                distinct++;
            }
        }

        var tokens = ImmutableArray.CreateBuilder<SqlToken>(distinct);
        foreach (var occurrence in occurrences)
        {
            var index = IndexOf(tokens, occurrence.Name);
            if (index < 0)
            {
                tokens.Add(new SqlToken(occurrence.Name, occurrence.Default));
            }
            else if (occurrence.Default is not null)
            {
                var first = tokens[index].Default;
                if (first is null)
                {
                    tokens[index] = new SqlToken(occurrence.Name, occurrence.Default);
                }
                else if (!string.Equals(first, occurrence.Default, StringComparison.Ordinal))
                {
                    errors.Add(Conflict(sql, occurrence));
                }
            }
        }

        // An index loop: the list is an interface, and an enumerator of it would be allocated for each query.
        for (var position = 0; position < markerDefaults.Count; position++)
        {
            var marker = markerDefaults[position];
            var index = IndexOf(tokens, marker.Name);
            if (index < 0)
            {
                continue;
            }

            var written = tokens[index].Default;
            if (written is null)
            {
                tokens[index] = new SqlToken(marker.Name, marker.Text);
            }
            else if (!string.Equals(written, marker.Text, StringComparison.Ordinal))
            {
                var inline = FirstWithDefault(occurrences, marker.Name);
                errors.Add(
                    marker.Marker.Span.Start > sql.ToSourceSpan(inline.Span).Start
                        ? SqlParseError.Create(
                            SqlParseErrorKind.ConflictingSettings,
                            marker.Marker.ValueSpan,
                            SqlMarkerReader.Describe(text, marker.Marker)
                        )
                        : Conflict(sql, inline)
                );
            }
        }

        return new EquatableArray<SqlToken>(tokens.MoveToImmutable());
    }

    private static bool AppearedBefore(EquatableArray<SqlTokenOccurrence> occurrences, int index)
    {
        for (var before = 0; before < index; before++)
        {
            if (string.Equals(occurrences[before].Name, occurrences[index].Name, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static int IndexOf(ImmutableArray<SqlToken>.Builder tokens, string name)
    {
        for (var index = 0; index < tokens.Count; index++)
        {
            if (string.Equals(tokens[index].Name, name, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }

    // The first occurrence of the token that has a default.  Only called for a token that has one.
    private static SqlTokenOccurrence FirstWithDefault(EquatableArray<SqlTokenOccurrence> occurrences, string name)
    {
        for (var index = 0; index < occurrences.Count; index++)
        {
            if (
                occurrences[index].Default is not null
                && string.Equals(occurrences[index].Name, name, StringComparison.Ordinal)
            )
            {
                return occurrences[index];
            }
        }

        throw new InvalidOperationException("The token has no default written in the SQL.");
    }

    private static SqlParseError Conflict(SqlBlockText sql, SqlTokenOccurrence occurrence) =>
        SqlParseError.Create(
            SqlParseErrorKind.ConflictingSettings,
            sql.ToSourceSpan(occurrence.Span),
            sql.Text.Substring(occurrence.Span.Start, occurrence.Span.Length)
        );
}
