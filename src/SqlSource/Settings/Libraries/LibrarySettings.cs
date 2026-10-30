using System;
using System.Collections.Immutable;

namespace SqlSource.Settings.Libraries;

/// <summary>
/// The rules of the library types: an array of entries sorted by <see cref="SqlLibrary" />, with each library once.
/// </summary>
/// <remarks>
/// The levels merge per library, as the type override's do: a level replaces only the libraries it names, and an entry
/// of <see cref="LibraryState.Default" /> stays, so that it hides the entries of the levels below.
/// </remarks>
internal static class LibrarySettings
{
    /// <summary>
    /// <paramref name="near" /> over <paramref name="far" />: every library of either, with the entry of
    /// <paramref name="near" /> for a library both name.  Null when both are, and either one itself when the other is.
    /// </summary>
    public static EquatableArray<LibrarySetting>? Over(
        EquatableArray<LibrarySetting>? near,
        EquatableArray<LibrarySetting>? far
    )
    {
        if (near is not { } over)
        {
            return far;
        }

        if (far is not { } under)
        {
            return over;
        }

        var merged = ImmutableArray.CreateBuilder<LibrarySetting>(over.Count + under.Count);
        // Both are sorted by library, each library once: a library both name takes the nearer entry.
        var (high, low) = (0, 0);
        while (high < over.Count || low < under.Count)
        {
            if (low == under.Count || (high < over.Count && over[high].Library <= under[low].Library))
            {
                if (low < under.Count && over[high].Library == under[low].Library)
                {
                    low++;
                }

                merged.Add(over[high++]);
            }
            else
            {
                merged.Add(under[low++]);
            }
        }

        return new EquatableArray<LibrarySetting>(merged.ToImmutable());
    }

    /// <summary>The entry for <paramref name="library" />, or null when the entries do not name it.</summary>
    public static LibrarySetting? Find(EquatableArray<LibrarySetting> merged, SqlLibrary library)
    {
        foreach (var entry in merged)
        {
            if (entry.Library == library)
            {
                return entry;
            }
        }

        return null;
    }

    /// <summary>
    /// Reads <c>name=value</c>, with white space allowed around the <c>=</c>: the name and the value are each read as a
    /// choice by <see cref="SettingValue.TryReadChoice{T}" />, so a name ignores case, hyphens, spaces and tabs.  False
    /// for an entry without an <c>=</c>, or whose name or value is not one.
    /// </summary>
    public static bool TryReadEntry(ReadOnlySpan<char> entry, out SqlLibrary library, out LibraryState state)
    {
        library = default;
        state = default;
        var equals = entry.IndexOf('=');
        return equals >= 0
            && SettingValue.TryReadChoice(entry.Slice(0, equals).Trim(), out library)
            && SettingValue.TryReadChoice(entry.Slice(equals + 1).Trim(), out state);
    }
}
