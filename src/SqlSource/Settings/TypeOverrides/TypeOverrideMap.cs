using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace SqlSource.Settings.TypeOverrides;

/// <summary>
/// The rules of a map of type overrides: an array of entries sorted by key, with each key once.  Keys are compared by
/// their parts, as <see cref="TypeKeys.TrySplit" /> gives them, and by the text of a part, so that <c>"a.b".c</c> and
/// <c>a."b.c"</c> are two keys and <c>public."status"</c> and <c>public.status</c> are one.  The order is that of the
/// keys ignoring case, then that of the keys exactly, so that keys that differ only by case are two keys and sit
/// together.
/// </summary>
/// <remarks>
/// The levels merge per key, the exception to the rule that a level replaces the one below it whole: a level
/// replaces only the keys it names.  An entry of <c>default</c> stays in a merged map, so that it hides the key of
/// a level merged under it later and so that <see cref="Find" /> weighs it in its place like any other entry.
/// </remarks>
internal static class TypeOverrideMap
{
    /// <summary>
    /// <paramref name="higher" /> over <paramref name="lower" />: every key of either, with the entry of
    /// <paramref name="higher" /> for a key both have.  Null when both are, and either one itself when the other is.
    /// </summary>
    public static EquatableArray<TypeOverride>? Over(
        EquatableArray<TypeOverride>? higher,
        EquatableArray<TypeOverride>? lower
    )
    {
        if (higher is not { } over)
        {
            return lower;
        }

        if (lower is not { } under)
        {
            return over;
        }

        var merged = ImmutableArray.CreateBuilder<TypeOverride>(over.Count + under.Count);
        var (high, low) = (0, 0);
        while (high < over.Count && low < under.Count)
        {
            var order = Order(new KeyParts(over[high].Key), new KeyParts(under[low].Key));
            merged.Add(order <= 0 ? over[high] : under[low]);
            high += order <= 0 ? 1 : 0;
            low += order >= 0 ? 1 : 0;
        }

        for (; high < over.Count; high++)
        {
            merged.Add(over[high]);
        }

        for (; low < under.Count; low++)
        {
            merged.Add(under[low]);
        }

        return new EquatableArray<TypeOverride>(merged.ToImmutable());
    }

    /// <summary>
    /// The entry for <paramref name="key" />, a type's key as it is written, or null when no entry matches it or the
    /// one that wins is <c>default</c>.  It is <see cref="Winner" />'s entry when that names a C# type.
    /// </summary>
    public static TypeOverride? Find(EquatableArray<TypeOverride> map, string key) =>
        Winner(map, key) is { Type: not null } entry ? entry : null;

    /// <summary>
    /// The entry that wins for <paramref name="key" />, a type's key as it is written, an entry of <c>default</c>
    /// included, or null when no entry matches it.  An entry matches when its key is equal to it ignoring case and its
    /// quoted parts are equal to the key's exactly.  Of those, the entry of the most specific place wins, then one
    /// equal to the key exactly, then the first in the order of the map.
    /// </summary>
    public static TypeOverride? Winner(EquatableArray<TypeOverride> map, string key)
    {
        var parts = new KeyParts(key);
        return BestOf(map, parts).Entry;
    }

    /// <summary>
    /// Where <paramref name="key" /> is in a list kept sorted by key, or the complement of where it would go, as
    /// <see cref="List{T}.BinarySearch(T)" /> gives it.
    /// </summary>
    public static int IndexOf(List<TypeOverride> entries, string key)
    {
        var parts = new KeyParts(key);
        var at = LowerBound(entries, parts, ignoringCaseOnly: false);
        return at < entries.Count && Order(new KeyParts(entries[at].Key), parts) == 0 ? at : ~at;
    }

    // The best of the entries that match key: equal to it ignoring case, from the first such entry on, with its
    // quoted parts equal to key's, or none.  A key's entries sit together in the order of the map, so the walk stops
    // at the first that is not equal to it ignoring case.
    private static Match BestOf(EquatableArray<TypeOverride> map, in KeyParts key)
    {
        Match best = default;
        for (var at = LowerBound(map, key, ignoringCaseOnly: true); at < map.Count; at++)
        {
            var entry = new KeyParts(map[at].Key);
            if (Compare(entry, key, StringComparison.OrdinalIgnoreCase) != 0)
            {
                break;
            }

            if (QuotedPartsAreEqual(entry, key))
            {
                var match = new Match(map[at], Compare(entry, key, StringComparison.Ordinal) == 0);
                best = match.IsBetterThan(best) ? match : best;
            }
        }

        return best;
    }

    // Whether each part of entry that is quoted has the text of the same part of key exactly.  The two are equal
    // ignoring case, so they have the same parts.
    private static bool QuotedPartsAreEqual(in KeyParts entry, in KeyParts key) =>
        !entry.IsWellFormed
        || !key.IsWellFormed
        || (PartMatches(entry.Schema, key.Schema) && PartMatches(entry.Name, key.Name));

    private static bool PartMatches(ReadOnlySpan<char> entryPart, ReadOnlySpan<char> part) =>
        !TypeKeys.IsQuoted(entryPart) || TypeKeys.TextOf(entryPart).SequenceEqual(TypeKeys.TextOf(part));

    // The order of the map: ignoring case, then exactly to break a tie.
    private static int Order(in KeyParts a, in KeyParts b)
    {
        var order = Compare(a, b, StringComparison.OrdinalIgnoreCase);
        return order != 0 ? order : Compare(a, b, StringComparison.Ordinal);
    }

    // Compares the keys as lists of their parts, [name] or [schema, name], lexicographically by the text of each part,
    // a shorter list first, and then a key that is not an array before one that is.  A key that is not well formed
    // does not reach the map, and would be compared as a whole.
    private static int Compare(in KeyParts a, in KeyParts b, StringComparison comparison)
    {
        if (!a.IsWellFormed || !b.IsWellFormed)
        {
            return string.Compare(a.Text, b.Text, comparison);
        }

        var order = ComparePart(a.First, b.First, comparison);
        if (order == 0 && (a.Schema.IsEmpty || b.Schema.IsEmpty))
        {
            order = b.Schema.IsEmpty.CompareTo(a.Schema.IsEmpty);
        }
        else if (order == 0)
        {
            order = ComparePart(a.Name, b.Name, comparison);
        }

        return order != 0 ? order : a.IsArray.CompareTo(b.IsArray);
    }

    private static int ComparePart(ReadOnlySpan<char> a, ReadOnlySpan<char> b, StringComparison comparison) =>
        TypeKeys.TextOf(a).CompareTo(TypeKeys.TextOf(b), comparison);

    // The index of the first entry that is not less than key, in the order of the map or, with ignoringCaseOnly, in
    // the order of the keys ignoring case.  Generic over the list, so that a map, which is a struct, is searched
    // without being boxed.  It is a search of its own because it is a lower bound over a ref struct key, which
    // SortedSearch cannot take (TD-0063).
    private static int LowerBound<TList>(TList entries, in KeyParts key, bool ignoringCaseOnly)
        where TList : IReadOnlyList<TypeOverride>
    {
        var (low, high) = (0, entries.Count);
        while (low < high)
        {
            var middle = low + ((high - low) / 2);
            var entry = new KeyParts(entries[middle].Key);
            var order = ignoringCaseOnly ? Compare(entry, key, StringComparison.OrdinalIgnoreCase) : Order(entry, key);
            if (order < 0)
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

    // An entry that matches a type's key, the rank of its place, and whether it is equal to the key exactly.  The
    // default is no match.
    private readonly struct Match(TypeOverride entry, bool isExact)
    {
        public TypeOverride? Entry { get; } = entry;

        public int Rank { get; } = RankOf(entry.Place);

        public bool IsExact { get; } = isExact;

        // The entry of the more specific place, then the one equal to the key exactly.  On a tie the earlier match
        // stays, so the first in the order of the map wins.
        public bool IsBetterThan(Match other) =>
            other.Entry is null || Rank > other.Rank || (Rank == other.Rank && IsExact && !other.IsExact);

        // How specific a place is, the most specific highest.  The attribute has no level for the type override.
        private static int RankOf(SettingPlace place) =>
            place switch
            {
                SettingPlace.Query => 3,
                SettingPlace.FileHeader => 2,
                SettingPlace.Metadata => 1,
                SettingPlace.Property or SettingPlace.Attribute or _ => 0,
            };
    }

    // A key split once into its parts, as TypeKeys.TrySplit gives them, so that a search splits the key it looks for
    // once and each entry once for each comparison.
    private readonly ref struct KeyParts
    {
        public KeyParts(string text)
        {
            Text = text;
            IsWellFormed = TypeKeys.TrySplit(text.AsSpan(), out var schema, out var name, out var isArray);
            Schema = schema;
            Name = name;
            IsArray = isArray;
        }

        public string Text { get; }

        public bool IsWellFormed { get; }

        // Empty for an unqualified key.
        public ReadOnlySpan<char> Schema { get; }

        public ReadOnlySpan<char> Name { get; }

        public bool IsArray { get; }

        // The part a list of parts is compared by first.
        public ReadOnlySpan<char> First => Schema.IsEmpty ? Name : Schema;
    }
}
