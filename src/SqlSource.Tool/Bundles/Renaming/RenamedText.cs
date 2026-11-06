using System;
using System.Collections.Immutable;

namespace SqlSource.Tool.Bundles.Renaming;

/// <summary>A text renamed, with where each code point of the original went.</summary>
/// <param name="Text">The renamed text.</param>
/// <param name="Offsets">
/// For each code point <c>i</c> of the original, from zero, the index of the renamed code point it became, and one
/// more for the end of the original.  Every code point of a name that was replaced goes to the replacement's start.
/// </param>
internal sealed record RenamedText(string Text, ImmutableArray<int> Offsets)
{
    /// <summary>
    /// A position of the original mapped into the renamed text.  Both count code points from one, as PostgreSQL's
    /// position of an error does, and the position just after the end maps to the one after the renamed end.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The position is not in the original.</exception>
    public int MapPosition(int position) =>
        position >= 1 && position <= Offsets.Length
            ? Offsets[position - 1] + 1
            : throw new ArgumentOutOfRangeException(
                nameof(position),
                position,
                $"A position of the original is from 1 to {Offsets.Length}."
            );
}
