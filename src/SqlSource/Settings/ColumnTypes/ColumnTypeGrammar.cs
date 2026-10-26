using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using SqlSource.Settings.ClrTypes;
using SqlSource.Settings.TypeOverrides;

namespace SqlSource.Settings.ColumnTypes;

/// <summary>
/// Reads the column types, in an MSBuild value, in the members of an object of a settings file and in a
/// <c>-- column:</c> marker of a file's header.  The key is a
/// column's source, which <see cref="ColumnTypeKey.TryNormalize" /> reads, and the type one that
/// <see cref="ClrTypeName.TryParse" /> reads, or <c>default</c>, ignoring case, which takes the key out of the entries
/// below.
/// </summary>
/// <remarks>
/// An MSBuild value is a list of <c>key=type</c> entries separated by white space, by the type override's rules for
/// white space, <c>&lt;&gt;</c>, <c>[]</c> and quotes, which <see cref="TypeOverrideGrammar" /> holds: <c>=</c> and not
/// <c>-&gt;</c>, since a <c>&gt;</c> in a project file is <c>&amp;gt;</c>.  A marker holds one entry,
/// <c>key -&gt; type</c>, with white space allowed around its arrow.  A member of an object is a key and a type.
/// </remarks>
internal static class ColumnTypeGrammar
{
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

            ColumnTypeMap.Put(entries ??= [], new ColumnType(normalized, type, place));
        }

        return entries is null
            ? EquatableArray<ColumnType>.Empty
            : new EquatableArray<ColumnType>(entries.ToImmutableArray());
    }

    /// <summary>
    /// Reads the members of an object, as a settings file writes the column types.  A member's key and type are
    /// trimmed.  The index of a member whose key is not one, or whose type is not valid or is empty, is added to
    /// <paramref name="invalid" />.  A member whose key the entries already have, by the identity of
    /// <see cref="ColumnTypeMap" />, is left out, and added to <paramref name="repeated" /> with the index of the
    /// earlier member that put the key: of two members for one key, the first wins.  The other members apply.  In the
    /// order of <see cref="ColumnTypeMap" />, and empty when the object has no valid member.
    /// </summary>
    public static EquatableArray<ColumnType> ReadMembers(
        IReadOnlyList<KeyValuePair<string, string>> members,
        SettingPlace place,
        List<int> invalid,
        List<(int Index, int First)> repeated
    )
    {
        // Beside the entries, the index of the member that put each of them.
        var (entries, entryMembers) = (new List<ColumnType>(), new List<int>());
        for (var index = 0; index < members.Count; index++)
        {
            if (
                !TypeOverrideGrammar.TryReadType(members[index].Value.AsSpan().Trim(), out var type)
                || !ColumnTypeKey.TryNormalize(members[index].Key.AsSpan().Trim(), out var key)
            )
            {
                invalid.Add(index);
                continue;
            }

            var at = ColumnTypeMap.IndexOf(entries, key);
            if (at >= 0)
            {
                repeated.Add((index, entryMembers[at]));
                continue;
            }

            entries.Insert(~at, new ColumnType(key, type, place));
            entryMembers.Insert(~at, index);
        }

        return new EquatableArray<ColumnType>(entries.ToImmutableArray());
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

        if (
            !TypeOverrideGrammar.TryReadType(value.Slice(arrow + 2).Trim(), out var type)
            || !ColumnTypeKey.TryNormalize(value.Slice(0, arrow).Trim(), out var key)
        )
        {
            return false;
        }

        entry = new ColumnType(key, type, SettingPlace.FileHeader);
        return true;
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
