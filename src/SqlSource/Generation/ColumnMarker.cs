using System;
using SqlSource.Diagnostics;

namespace SqlSource.Generation;

/// <summary>
/// What one <c>-- column:</c> marker says of a column of its query's result, with where the marker is.  It sets at
/// least one of <paramref name="IsNullable" />, <paramref name="ClrType" /> and <paramref name="ResetsType" />.
/// </summary>
/// <param name="Name">The column's name, as the database returns it, without the quotes it may be written in.</param>
/// <param name="IsNullable">True for <c>null</c>, false for <c>not null</c>, null when the marker says neither.</param>
/// <param name="ClrType">
/// The C# type after <c>-&gt;</c>, as <c>ClrTypeName.Text</c> writes it, or null when the marker gives none.
/// </param>
/// <param name="ResetsType">
/// Whether the marker says <c>-&gt; default</c>, which takes the column out of the column types of every level.
/// </param>
/// <param name="Location">The whole marker comment in the file.</param>
internal sealed record ColumnMarker(
    string Name,
    bool? IsNullable,
    string? ClrType,
    bool ResetsType,
    LocationInfo Location
)
{
    /// <summary>
    /// Whether the marker names <paramref name="column" />, a column's name as the tool wrote it: equal to it ignoring
    /// case, or to it without its <c>!</c> or <c>?</c> mark.
    /// </summary>
    public bool Names(string column)
    {
        var length = column.Length - (HasMark(column) ? 1 : 0);
        return string.Equals(column, Name, StringComparison.OrdinalIgnoreCase)
            || (
                Name.Length == length
                && string.Compare(column, 0, Name, 0, length, StringComparison.OrdinalIgnoreCase) == 0
            );
    }

    /// <summary>Whether a column's name ends in a <c>!</c> or <c>?</c> mark, which sets its nullability.</summary>
    internal static bool HasMark(string name) => name.Length > 0 && name[name.Length - 1] is '!' or '?';
}
