namespace SqlSource.Generation.Types;

/// <summary>
/// A parameter or a column as a message names it: <c>column 'id'</c>.
/// </summary>
internal static class MessageSubject
{
    /// <summary>The owner of a query's parameter.</summary>
    public const string Parameter = "parameter";

    /// <summary>The owner of a query's column.</summary>
    public const string Column = "column";

    /// <summary>
    /// The subject of a message: <paramref name="owner" />, then <paramref name="name" /> in single quotes.
    /// </summary>
    /// <param name="owner"><see cref="Parameter" /> or <see cref="Column" />.</param>
    /// <param name="name">The parameter's or the column's name.</param>
    public static string Of(string owner, string name) => owner + " '" + name + "'";
}
