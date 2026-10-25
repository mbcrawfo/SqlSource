using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using SqlSource.Settings.ClrTypes;
using SqlSource.Settings.TypeOverrides;

namespace SqlSource.Settings.ColumnTypes;

/// <summary>
/// Reads the column types, in an MSBuild value and in a <c>-- column:</c> marker of a file's header.  The key is a
/// column's source, which <see cref="ColumnTypeKey.TryNormalize" /> reads, and the type one that
/// <see cref="ClrTypeName.TryParse" /> reads, or <c>default</c>, ignoring case, which takes the key out of the entries
/// below.
/// </summary>
/// <remarks>
/// An MSBuild value is a list of <c>key=type</c> entries separated by white space, by the type override's rules for
/// white space, <c>&lt;&gt;</c>, <c>[]</c> and quotes, which <see cref="TypeOverrideGrammar" /> holds: <c>=</c> and not
/// <c>-&gt;</c>, since a <c>&gt;</c> in a project file is <c>&amp;gt;</c>.  A marker holds one entry,
/// <c>key -&gt; type</c>, with white space allowed around its arrow.
/// </remarks>
internal static class ColumnTypeGrammar
{
    private const string Default = "default";

    /// <summary>
    /// Reads an MSBuild value: an entry that is not one is added to <paramref name="invalid" /> as written, and the
    /// other entries apply.  Of two entries for one key, the last wins, so that a project can add to a value it was
    /// given.  In the order of <see cref="ColumnTypeMap" />, and empty when the value has no valid entry.
    /// </summary>
    public static EquatableArray<ColumnType> ReadList(string text, SettingPlace place, List<string> invalid)
    {
        List<ColumnType>? entries = null;
        var rest = text.AsSpan();
        while (TypeOverrideGrammar.NextEntry(ref rest) is { IsEmpty: false } written)
        {
            if (
                !TypeOverrideGrammar.TrySplitEntry(written, spacesAroundEquals: false, out var key, out var type)
                || !ColumnTypeKey.TryNormalize(key, out var normalized)
            )
            {
                invalid.Add(written.ToString());
                continue;
            }

            Put(entries ??= [], new ColumnType(normalized, type, place));
        }

        return entries is null
            ? EquatableArray<ColumnType>.Empty
            : new EquatableArray<ColumnType>(entries.ToImmutableArray());
    }

    /// <summary>
    /// Reads the value of a <c>-- column:</c> marker of a file's header, <c>key -&gt; type</c> or
    /// <c>key -&gt; default</c>, split at its first <c>-&gt;</c> outside double quotes, at the place
    /// <see cref="SettingPlace.FileHeader" />.  False, with a null <paramref name="entry" />, for a value without the
    /// arrow, a key that is not one, a nullability among them, or a type that is not valid.
    /// </summary>
    public static bool TryReadHeaderEntry(ReadOnlySpan<char> value, out ColumnType? entry)
    {
        entry = null;
        var arrow = IndexOfArrow(value);
        if (arrow < 0)
        {
            return false;
        }

        var written = value.Slice(arrow + 2).Trim();
        string? type = null;
        if (!written.Equals(Default.AsSpan(), StringComparison.OrdinalIgnoreCase))
        {
            if (!ClrTypeName.TryParse(written, out var name))
            {
                return false;
            }

            type = name!.Text;
        }

        if (!ColumnTypeKey.TryNormalize(value.Slice(0, arrow).Trim(), out var key))
        {
            return false;
        }

        entry = new ColumnType(key, type, SettingPlace.FileHeader);
        return true;
    }

    // Puts an entry in its place in a list kept in the order of ColumnTypeMap, over an entry of its key.
    private static void Put(List<ColumnType> entries, ColumnType entry)
    {
        var at = ColumnTypeMap.IndexOf(entries, entry.Key);
        if (at >= 0)
        {
            entries[at] = entry;
        }
        else
        {
            entries.Insert(~at, entry);
        }
    }

    // The first -> outside double quotes, or -1.  A " toggles being inside quotes, so a doubled one inside a quoted
    // part closes and opens it again.
    private static int IndexOfArrow(ReadOnlySpan<char> text)
    {
        var quoted = false;
        for (var index = 0; index < text.Length - 1; index++)
        {
            if (text[index] == '"')
            {
                quoted = !quoted;
            }
            else if (!quoted && text[index] == '-' && text[index + 1] == '>')
            {
                return index;
            }
        }

        return -1;
    }
}
