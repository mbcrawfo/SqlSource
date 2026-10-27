using System;
using System.Collections.Generic;
using SqlSource.Settings.TypeOverrides;

namespace SqlSource.Settings.ColumnTypes;

/// <summary>
/// The rules of a map of column types: an array of entries sorted by key, with each key once.  Keys are compared by
/// their parts, as <see cref="ColumnTypeKey.TrySplit" /> gives them, and by the text of a part, as
/// <see cref="TypeKeyText.TextOf" /> gives it, so that <c>"users".id</c> and <c>users.id</c> are one key.  The order is
/// that of the table, then the column, then the schema, a key without one first, each ignoring case, then the same
/// exactly, so that the keys of one table and column sit together, with a schema or without, and keys that differ
/// only by case are two keys.
/// </summary>
/// <remarks>
/// The levels merge per key, as the type override's do (<see cref="TypeOverrideMap" />): a level replaces only the
/// keys it names, and an entry of <c>default</c> stays, so that it hides the key of a level merged under it later and
/// so that <see cref="Find" /> weighs it in its place like any other entry.
/// </remarks>
internal static class ColumnTypeMap
{
    // The order of the map and the key of an entry, for SortedKeyedList.  Static, so that no call allocates them.
    private static readonly Comparison<string> KeyOrder = static (a, b) => Order(new KeyParts(a), new KeyParts(b));

    private static readonly Func<ColumnType, string> KeyOf = static entry => entry.Key;

    /// <summary>
    /// <paramref name="higher" /> over <paramref name="lower" />: every key of either, with the entry of
    /// <paramref name="higher" /> for a key both have.  Null when both are, and either one itself when the other is.
    /// </summary>
    public static EquatableArray<ColumnType>? Over(
        EquatableArray<ColumnType>? higher,
        EquatableArray<ColumnType>? lower
    ) => SortedKeyedList.Over(higher, lower, KeyOrder, KeyOf);

    /// <summary>
    /// The entry for a column read from <paramref name="column" /> of <paramref name="table" /> in
    /// <paramref name="schema" />, the names of its origin, or null when no entry matches it or the one that wins is
    /// <c>default</c>.  An entry matches when each of its parts matches the origin's, a plain part ignoring case and a
    /// quoted one exactly, and a key of two parts matches any schema, a null one among them.  Of those, the entry of
    /// the most specific place wins, then one with a schema, then one whose every part is spelled as the origin's,
    /// then the first in the order of the map.
    /// </summary>
    public static ColumnType? Find(EquatableArray<ColumnType> map, string? schema, string table, string column)
    {
        var origin = new Origin(schema, table, column);
        Match best = default;
        for (var at = LowerBound(map, origin); at < map.Count; at++)
        {
            var entry = new KeyParts(map[at].Key);
            if (!entry.IsWellFormed || CompareColumn(entry, origin, StringComparison.OrdinalIgnoreCase) != 0)
            {
                break;
            }

            if (Matches(entry, origin))
            {
                var match = new Match(map[at], !entry.Schema.IsEmpty, IsSpelledAs(entry, origin));
                best = match.IsBetterThan(best) ? match : best;
            }
        }

        return best.Entry is { Type: not null } winner ? winner : null;
    }

    /// <summary>
    /// Where <paramref name="key" /> is in a list kept sorted by key, or the complement of where it would go, as
    /// <see cref="List{T}.BinarySearch(T)" /> gives it.
    /// </summary>
    public static int IndexOf(List<ColumnType> entries, string key) =>
        SortedKeyedList.IndexOf(entries, key, KeyOrder, KeyOf);

    /// <summary>
    /// Adds an entry in its place in a list kept sorted by key and gives null, or gives the list's entry of its key
    /// and leaves the list as it is.
    /// </summary>
    public static ColumnType? AddOrFind(List<ColumnType> entries, ColumnType entry) =>
        SortedKeyedList.AddOrFind(entries, entry, KeyOrder, KeyOf);

    // The order of the map: ignoring case, then exactly to break a tie.
    private static int Order(in KeyParts a, in KeyParts b)
    {
        var order = Compare(a, b, StringComparison.OrdinalIgnoreCase);
        return order != 0 ? order : Compare(a, b, StringComparison.Ordinal);
    }

    // By the table, the column, then the schema, a key without one first.  A key that is not well formed does not
    // reach the map, and would be compared as a whole.
    private static int Compare(in KeyParts a, in KeyParts b, StringComparison comparison)
    {
        if (!a.IsWellFormed || !b.IsWellFormed)
        {
            return string.Compare(a.Text, b.Text, comparison);
        }

        var order = ComparePart(a.Table, b.Table, comparison);
        order = order != 0 ? order : ComparePart(a.Column, b.Column, comparison);
        order = order != 0 ? order : b.Schema.IsEmpty.CompareTo(a.Schema.IsEmpty);
        return order != 0 || a.Schema.IsEmpty ? order : ComparePart(a.Schema, b.Schema, comparison);
    }

    private static int ComparePart(ReadOnlySpan<char> a, ReadOnlySpan<char> b, StringComparison comparison) =>
        TypeKeyText.TextOf(a).CompareTo(TypeKeyText.TextOf(b), comparison);

    // An entry's table and column against the origin's, which is the order of the map's first two parts.
    private static int CompareColumn(in KeyParts entry, in Origin origin, StringComparison comparison)
    {
        var order = TypeKeyText.TextOf(entry.Table).CompareTo(origin.Table.AsSpan(), comparison);
        return order != 0 ? order : TypeKeyText.TextOf(entry.Column).CompareTo(origin.Column.AsSpan(), comparison);
    }

    // The first entry whose table and column are not less than the origin's ignoring case.  Generic over the list, so
    // that a map, which is a struct, is searched without being boxed.
    private static int LowerBound<TList>(TList entries, in Origin origin)
        where TList : IReadOnlyList<ColumnType>
    {
        var (low, high) = (0, entries.Count);
        while (low < high)
        {
            var middle = low + ((high - low) / 2);
            var entry = new KeyParts(entries[middle].Key);
            if (entry.IsWellFormed && CompareColumn(entry, origin, StringComparison.OrdinalIgnoreCase) < 0)
            {
                low = middle + 1;
            }
            else
            {
                high = middle;
            }
        }

        return low;
    }

    // The entry's table and column are equal to the origin's ignoring case: its quoted ones must be equal exactly, and
    // its schema, when it has one, must match the origin's.
    private static bool Matches(in KeyParts entry, in Origin origin) =>
        PartMatches(entry.Table, origin.Table)
        && PartMatches(entry.Column, origin.Column)
        && (
            entry.Schema.IsEmpty
            || (
                origin.Schema is { } schema
                && TypeKeyText.TextOf(entry.Schema).Equals(schema.AsSpan(), StringComparison.OrdinalIgnoreCase)
                && PartMatches(entry.Schema, schema)
            )
        );

    private static bool PartMatches(ReadOnlySpan<char> entryPart, string name) =>
        !TypeKeyText.IsQuoted(entryPart) || TypeKeyText.TextOf(entryPart).SequenceEqual(name.AsSpan());

    private static bool IsSpelledAs(in KeyParts entry, in Origin origin) =>
        TypeKeyText.TextOf(entry.Table).SequenceEqual(origin.Table.AsSpan())
        && TypeKeyText.TextOf(entry.Column).SequenceEqual(origin.Column.AsSpan())
        && (entry.Schema.IsEmpty || TypeKeyText.TextOf(entry.Schema).SequenceEqual(origin.Schema.AsSpan()));

    // The names of a column's origin, as the database gives them.
    private readonly struct Origin(string? schema, string table, string column)
    {
        public string? Schema { get; } = schema;

        public string Table { get; } = table;

        public string Column { get; } = column;
    }

    // An entry that matches an origin, the rank of its place, whether it has a schema, and whether it is spelled as
    // the origin is.  The default is no match.
    private readonly struct Match(ColumnType entry, bool hasSchema, bool isExact)
    {
        public ColumnType? Entry { get; } = entry;

        public int Rank { get; } = SettingPlaces.Specificity(entry.Place);

        public bool HasSchema { get; } = hasSchema;

        public bool IsExact { get; } = isExact;

        // The entry of the more specific place, then one with a schema, then one spelled as the origin is.  On a tie
        // the earlier match stays, so the first in the order of the map wins.
        public bool IsBetterThan(Match other)
        {
            if (other.Entry is null || Rank != other.Rank)
            {
                return other.Entry is null || Rank > other.Rank;
            }

            return HasSchema != other.HasSchema ? HasSchema : IsExact && !other.IsExact;
        }
    }

    // A key split once into its parts, as ColumnTypeKey.TrySplit gives them.
    private readonly ref struct KeyParts
    {
        public KeyParts(string text)
        {
            Text = text;
            IsWellFormed = ColumnTypeKey.TrySplit(text.AsSpan(), out var schema, out var table, out var column);
            Schema = schema;
            Table = table;
            Column = column;
        }

        public string Text { get; }

        public bool IsWellFormed { get; }

        // Empty for a key of two parts.
        public ReadOnlySpan<char> Schema { get; }

        public ReadOnlySpan<char> Table { get; }

        public ReadOnlySpan<char> Column { get; }
    }
}
