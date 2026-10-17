using System;

namespace SqlSource.Tool.Describing.Postgres.Walk;

/// <summary>
/// An entry of a plan's <c>Output</c> that is a plain reference to a column: <c>alias.column</c>, which names the
/// relation instance, or a bare name, which a node prints for a column of its own relation.
/// </summary>
/// <remarks>
/// PostgreSQL prints a name without quotes only when it is lower-case letters, digits and underscores and does not
/// start with a digit; any other is in double quotes, with <c>""</c> for a quote.  Whatever else an entry holds, an
/// operator, a parenthesis, a cast, a space, makes it an expression, and an expression is no reference: the walk
/// knows nothing of what it can return.
/// </remarks>
/// <param name="Alias">The alias as the name is, without its quotes, or null for a bare name.</param>
/// <param name="Column">The column as the name is, without its quotes.</param>
internal sealed record OutputReference(string? Alias, string Column)
{
    /// <summary>The reference an entry is, or null for an expression.</summary>
    public static OutputReference? Parse(string entry)
    {
        var at = 0;
        if (Name(entry, ref at) is not { } first)
        {
            return null;
        }

        if (at == entry.Length)
        {
            return new OutputReference(null, first);
        }

        if (entry[at] != '.')
        {
            return null;
        }

        at++;
        return Name(entry, ref at) is { } second && at == entry.Length ? new OutputReference(first, second) : null;
    }

    // The name that starts at the index, which is moved past it, or null.
    private static string? Name(string entry, ref int at)
    {
        if (at >= entry.Length)
        {
            return null;
        }

        return entry[at] == '"' ? Quoted(entry, ref at) : Bare(entry, ref at);
    }

    // The text up to the next "." or the end, when it is a name PostgreSQL prints bare.
    private static string? Bare(string entry, ref int at)
    {
        var end = entry.IndexOf('.', at);
        end = end < 0 ? entry.Length : end;
        if (!PostgresIdentifiers.IsBareName(entry.AsSpan(at, end - at)))
        {
            return null;
        }

        var name = entry[at..end];
        at = end;
        return name;
    }

    // A quoted name, or null when its quote does not close or it is empty.
    private static string? Quoted(string entry, ref int at)
    {
        if (!PostgresIdentifiers.SkipQuoted(entry, at, out var end))
        {
            return null;
        }

        var name = PostgresIdentifiers.Unquote(entry.AsSpan(at, end - at));
        at = end;
        return name.Length > 0 ? name : null;
    }
}
