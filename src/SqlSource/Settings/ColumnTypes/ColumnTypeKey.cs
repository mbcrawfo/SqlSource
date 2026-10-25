using System;
using System.Text;
using SqlSource.Settings.TypeOverrides;

namespace SqlSource.Settings.ColumnTypes;

/// <summary>
/// The key of a column type, a column's source: <c>table.column</c>, which matches that column of a table or view of
/// that name in any schema, or <c>schema.table.column</c>, which matches that schema's alone.  Each part is read by
/// the type override's rules for a part, <see cref="TypeKeyText.TryReadPart" />: plain, letters, digits, <c>_</c> and
/// <c>$</c>, compared ignoring case, or quoted, any character with <c>""</c> for a quote, compared exactly, which may
/// be written in the escaped form <c>U&amp;"..."</c>.  Both engines read a key alike.
/// </summary>
internal static class ColumnTypeKey
{
    private const int MaxParts = 3;

    /// <summary>
    /// Reads a key of two or three parts and gives it with each plain part as written and each quoted part, the
    /// escaped form among them, in the quoted form of its text, as <see cref="TypeKeyText.Quote" /> writes it, so that
    /// <c>U&amp;"us\0065rs".id</c> is <c>"users".id</c>.  False for a key of one part or of four, an empty part, a
    /// quoted part that does not close, an escape that is not valid, and anything after the last part, <c>[]</c> or
    /// white space among it.
    /// </summary>
    public static bool TryNormalize(ReadOnlySpan<char> key, out string normalized)
    {
        normalized = string.Empty;
        var written = new StringBuilder(key.Length);
        var start = 0;
        for (var parts = 1; parts <= MaxParts; parts++)
        {
            if (!TypeKeyText.TryReadPart(key, start, out var end))
            {
                return false;
            }

            var part = key.Slice(start, end - start);
            _ = written.Append(parts > 1 ? "." : string.Empty).Append(Normalize(part));
            if (end == key.Length)
            {
                if (parts < 2)
                {
                    return false;
                }

                normalized = written.ToString();
                return true;
            }

            if (key[end] != '.')
            {
                return false;
            }

            start = end + 1;
        }

        return false;
    }

    /// <summary>
    /// Splits a key that <see cref="TryNormalize" /> gave into its parts, quotes included.  <paramref name="schema" />
    /// is empty for a key of two parts.  False for a text that is not a key.
    /// </summary>
    public static bool TrySplit(
        ReadOnlySpan<char> key,
        out ReadOnlySpan<char> schema,
        out ReadOnlySpan<char> table,
        out ReadOnlySpan<char> column
    )
    {
        schema = default;
        table = default;
        column = default;
        if (!TypeKeyText.TryReadPart(key, 0, out var first) || first >= key.Length || key[first] != '.')
        {
            return false;
        }

        var secondStart = first + 1;
        if (!TypeKeyText.TryReadPart(key, secondStart, out var second))
        {
            return false;
        }

        if (second == key.Length)
        {
            table = key.Slice(0, first);
            column = key.Slice(secondStart);
            return true;
        }

        var thirdStart = second + 1;
        if (key[second] != '.' || !TypeKeyText.TryReadPart(key, thirdStart, out var third) || third != key.Length)
        {
            return false;
        }

        schema = key.Slice(0, first);
        table = key.Slice(secondStart, second - secondStart);
        column = key.Slice(thirdStart);
        return true;
    }

    private static string Normalize(ReadOnlySpan<char> part) =>
        TypeKeyText.IsQuoted(part) ? TypeKeyText.Quote(TypeKeyText.TextOf(part).ToString()) : part.ToString();
}
