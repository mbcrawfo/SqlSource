using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using SqlSource.Settings.ClrTypes;
using SqlSource.Settings.TypeOverrides;

namespace SqlSource.Settings.ColumnTypes;

/// <summary>
/// Reads the column types, in the members of an object of a settings file and in a <c>-- column:</c> marker of a
/// file's header.  The key is a column's source, which <see cref="ColumnTypeKey.TryNormalize" /> reads, and the type
/// one that <see cref="ClrTypeName.TryParse" /> reads, or <c>default</c>, ignoring case, which takes the key out of the
/// entries below.  It also reads what follows the arrow of a query's <c>-- column:</c> and of a <c>-- param:</c>, with
/// <see cref="TryReadMarkerType" />.
/// </summary>
/// <remarks>
/// A marker holds one entry, <c>key -&gt; type</c>, with white space allowed around its arrow.  A member of an object
/// is a key and a type.  Either may say that the column holds JSON, the JSON opt-in: the word <c>json</c> and white
/// space before a marker's type, or a member that the settings file writes as an object with <c>"json": true</c>.
/// The opt-in needs a type, never <c>default</c>.
/// </remarks>
internal static class ColumnTypeGrammar
{
    // The word before a marker's type that is the JSON opt-in, ignoring case.
    private const string JsonWord = "json";

    /// <summary>
    /// Reads the members of an object, as a settings file writes the column types.  A member's key and type are
    /// trimmed.  The index of a member whose key is not one, or whose type is not valid or is empty, is added to
    /// <paramref name="invalid" />.  A member whose key the entries already have, by the identity of
    /// <see cref="ColumnTypeMap" />, is left out, and added to <paramref name="repeated" /> with the index of the
    /// earlier member that put the key: of two members for one key, the first wins.  The other members apply.  In the
    /// order of <see cref="ColumnTypeMap" />, and empty when the object has no valid member.  <paramref name="json" />
    /// says, by index, which member is written with the JSON opt-in; such a member of <c>default</c> is not valid.
    /// </summary>
    public static EquatableArray<ColumnType> ReadMembers(
        IReadOnlyList<KeyValuePair<string, string>> members,
        SettingPlace place,
        List<int> invalid,
        List<(int Index, int First)> repeated,
        IReadOnlyList<bool>? json = null
    )
    {
        // Beside the entries, the index of the member that put each of them.
        var (entries, entryMembers) = (new List<ColumnType>(), new List<int>());
        for (var index = 0; index < members.Count; index++)
        {
            var isJson = json is not null && json[index];
            if (
                !TypeOverrideGrammar.TryReadType(members[index].Value.AsSpan().Trim(), out var type)
                || (isJson && type is null)
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

            entries.Insert(~at, new ColumnType(key, type, place, isJson));
            entryMembers.Insert(~at, index);
        }

        return new EquatableArray<ColumnType>(entries.ToImmutableArray());
    }

    /// <summary>
    /// Reads the value of a <c>-- column:</c> marker of a file's header, <c>key -&gt; type</c> or
    /// <c>key -&gt; default</c>, split at its first <c>-&gt;</c> outside double quotes, at the place
    /// <see cref="SettingPlace.FileHeader" />, its type read by <see cref="TryReadMarkerType" />, so that
    /// <c>key -&gt; json Type</c> is the JSON opt-in.  False, with a null <paramref name="entry" />, for a value
    /// without the arrow, a key that is not one, a nullability among them, or a type that is not valid.
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
            !TryReadMarkerType(value.Slice(arrow + 2).Trim(), out var type, out var isJson)
            || !ColumnTypeKey.TryNormalize(value.Slice(0, arrow).Trim(), out var key)
        )
        {
            return false;
        }

        entry = new ColumnType(key, type, SettingPlace.FileHeader, isJson);
        return true;
    }

    /// <summary>
    /// Reads what follows a marker's <c>-&gt;</c>, trimmed: a type as <see cref="TypeOverrideGrammar.TryReadType" />
    /// reads it, null for <c>default</c>, or the JSON opt-in, the word <c>json</c> ignoring case, white space, and a
    /// type that is not <c>default</c>.  A C# type holds no white space, so <c>json</c> alone is a type of that name.
    /// False for a type that is not valid, and for the opt-in with no type after it or with <c>default</c>.
    /// </summary>
    public static bool TryReadMarkerType(ReadOnlySpan<char> written, out string? type, out bool isJson)
    {
        isJson =
            written.Length > JsonWord.Length
            && written.StartsWith(JsonWord.AsSpan(), StringComparison.OrdinalIgnoreCase)
            && char.IsWhiteSpace(written[JsonWord.Length]);
        if (!isJson)
        {
            return TypeOverrideGrammar.TryReadType(written, out type);
        }

        return TypeOverrideGrammar.TryReadType(written.Slice(JsonWord.Length).Trim(), out type) && type is not null;
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
