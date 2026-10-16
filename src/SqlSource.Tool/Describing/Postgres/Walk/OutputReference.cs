using System.Text;

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

    private static string? Bare(string entry, ref int at)
    {
        var start = at;
        if (!IsLowerOrUnderscore(entry[at]))
        {
            return null;
        }

        while (at < entry.Length && (IsLowerOrUnderscore(entry[at]) || char.IsAsciiDigit(entry[at])))
        {
            at++;
        }

        return entry[start..at];
    }

    private static string? Quoted(string entry, ref int at)
    {
        var name = new StringBuilder();
        var index = at + 1;
        while (index < entry.Length)
        {
            if (entry[index] != '"')
            {
                _ = name.Append(entry[index]);
                index++;
            }
            else if (index + 1 < entry.Length && entry[index + 1] == '"')
            {
                _ = name.Append('"');
                index += 2;
            }
            else
            {
                at = index + 1;
                return name.Length > 0 ? name.ToString() : null;
            }
        }

        return null;
    }

    private static bool IsLowerOrUnderscore(char character) => char.IsAsciiLetterLower(character) || character == '_';
}
