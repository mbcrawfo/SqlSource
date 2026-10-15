using System;
using System.Collections.Generic;
using System.Linq;

namespace SqlSource.Tool.Describing.Postgres;

/// <summary>
/// The server's spelling of each type with each modifier that <c>spellings</c> answered, and what a type with a
/// modifier is named and carries.
/// </summary>
/// <remarks>
/// The session asks for every pair that <see cref="PostgresTypes.PairsOf" /> gives, so a pair that is missing here is
/// a bug of the describer.  A modifier of <c>-1</c> is never asked: the tool spells a type without one itself.
/// </remarks>
internal sealed class TypeSpellings
{
    private readonly IReadOnlyDictionary<PostgresTypmodPair, string> _spelled;

    private TypeSpellings(IReadOnlyDictionary<PostgresTypmodPair, string> spelled)
    {
        _spelled = spelled;
    }

    /// <summary>No spelling: for a describe that asked none.</summary>
    public static TypeSpellings None { get; } = new(new Dictionary<PostgresTypmodPair, string>());

    /// <summary>The spellings of the rows that <c>spellings</c> answered.</summary>
    public static TypeSpellings From(IReadOnlyList<PostgresSpellingRow> rows) =>
        new(
            rows.ToDictionary(static row => new PostgresTypmodPair(row.Oid, row.Modifier), static row => row.FormatType)
        );

    /// <summary>
    /// The name of a type that is no array with a modifier, and the facets the modifier sets.  A modifier of
    /// <c>-1</c> is the tool's own spelling and no facets.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The pair was not spelled, or the server's text does not start with the type's text without a modifier.
    /// </exception>
    public (string Name, PostgresFacets Facets) Of(PostgresTypeRow row, int modifier)
    {
        if (modifier == TypeModifiers.NoModifier)
        {
            return (PostgresTypeNames.Spell(row, PostgresFacets.None), PostgresFacets.None);
        }

        return _spelled.TryGetValue(new PostgresTypmodPair(row.Oid, modifier), out var spelled)
            ? (PostgresTypeNames.WithModifier(row, spelled), TypeModifiers.Decode(row.Typname, modifier))
            : throw new InvalidOperationException(
                $"The answer to 'spellings' holds no spelling of the type '{row.FormatType}' ({row.Oid}) with the "
                    + $"modifier {modifier}."
            );
    }
}
