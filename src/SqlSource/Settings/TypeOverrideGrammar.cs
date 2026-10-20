using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using SqlSource.Settings.ClrTypes;

namespace SqlSource.Settings;

/// <summary>
/// Reads the type override, in an MSBuild value and in a <c>-- type-override:</c> marker alike.  An entry is
/// <c>key=type</c>, where the key is one that <see cref="TypeKeys.TryNormalize" /> reads and the type one that
/// <see cref="ClrTypeName.TryParse" /> reads, or <c>default</c>, ignoring case, which takes the key out of the map.
/// </summary>
/// <remarks>
/// An MSBuild value is a list of entries separated by white space, line breaks included, outside <c>&lt;&gt;</c>,
/// <c>[]</c> and double quotes.  A marker holds one entry, with white space allowed around its <c>=</c>.  The
/// <c>=</c> of an entry is the first one outside double quotes.
/// </remarks>
internal static class TypeOverrideGrammar
{
    private const string Default = "default";

    /// <summary>
    /// Reads one entry.  False, with a null <paramref name="entry" />, for a text that is not one, and never an
    /// exception.
    /// </summary>
    /// <param name="text">The entry and nothing else.</param>
    /// <param name="spacesAroundEquals">
    /// Whether white space may stand on either side of the <c>=</c>, as in a marker.
    /// </param>
    /// <param name="place">Where the entry was written.</param>
    /// <param name="entry">The entry that was read.</param>
    public static bool TryReadEntry(
        ReadOnlySpan<char> text,
        bool spacesAroundEquals,
        SettingPlace place,
        out TypeOverride? entry
    )
    {
        entry = null;
        var equals = IndexOfEquals(text);
        if (equals < 0)
        {
            return false;
        }

        var key = text.Slice(0, equals);
        var type = text.Slice(equals + 1);
        if (spacesAroundEquals)
        {
            key = key.TrimEnd();
            type = type.TrimStart();
        }

        if (!TypeKeys.TryNormalize(key, out var normalized))
        {
            return false;
        }

        if (type.Equals(Default.AsSpan(), StringComparison.OrdinalIgnoreCase))
        {
            entry = new TypeOverride(normalized, null, place);
            return true;
        }

        if (!ClrTypeName.TryParse(type, out var name))
        {
            return false;
        }

        entry = new TypeOverride(normalized, name!.Text, place);
        return true;
    }

    /// <summary>
    /// Reads an MSBuild value.  Each entry that is not valid is added to <paramref name="invalid" /> as written, and
    /// the other entries apply.  Of two entries for one key, the last wins, so that a project can add to a value it
    /// was given.  The entries are sorted by key, ignoring case; empty when none is valid.
    /// </summary>
    public static EquatableArray<TypeOverride> ReadList(string text, SettingPlace place, List<string> invalid)
    {
        List<TypeOverride>? entries = null;
        var rest = text.AsSpan();
        while (NextEntry(ref rest) is { IsEmpty: false } written)
        {
            if (!TryReadEntry(written, spacesAroundEquals: false, place, out var entry))
            {
                invalid.Add(written.ToString());
                continue;
            }

            entries ??= [];
            var at = TypeOverrideMap.IndexOf(entries, entry!.Key);
            if (at >= 0)
            {
                entries[at] = entry;
            }
            else
            {
                entries.Insert(~at, entry);
            }
        }

        return entries is null
            ? EquatableArray<TypeOverride>.Empty
            : new EquatableArray<TypeOverride>(entries.ToImmutableArray());
    }

    // The first = outside double quotes, or -1.  A " toggles being inside quotes, so a doubled one inside a quoted part
    // closes and opens it again.
    private static int IndexOfEquals(ReadOnlySpan<char> text)
    {
        var quoted = false;
        for (var index = 0; index < text.Length; index++)
        {
            if (text[index] == '"')
            {
                quoted = !quoted;
            }
            else if (text[index] == '=' && !quoted)
            {
                return index;
            }
        }

        return -1;
    }

    // The next run of text outside white space, where white space inside <>, [] or double quotes belongs to the run,
    // and a bracket inside double quotes is not counted.  Only the key opens a double quote.  Empty at the end.
    private static ReadOnlySpan<char> NextEntry(ref ReadOnlySpan<char> rest)
    {
        var start = 0;
        while (start < rest.Length && char.IsWhiteSpace(rest[start]))
        {
            start++;
        }

        var end = start;
        var open = 0;
        var quoted = false;
        var inType = false;
        while (end < rest.Length && (open > 0 || quoted || !char.IsWhiteSpace(rest[end])))
        {
            // Quotes belong to the key: once its = is read, a " is a character of the type, which is invalid.
            if (rest[end] == '"' && !inType)
            {
                quoted = !quoted;
            }
            else if (!quoted && rest[end] == '=')
            {
                inType = true;
            }
            else if (!quoted && rest[end] is '<' or '[')
            {
                open++;
            }
            else if (!quoted && rest[end] is '>' or ']' && open > 0)
            {
                open--;
            }

            end++;
        }

        var entry = rest.Slice(start, end - start);
        rest = rest.Slice(end);
        return entry;
    }
}
