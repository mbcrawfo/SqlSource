using System;
using System.Collections.Generic;
using System.Text;

namespace SqlSource.Tool.Describing.Postgres;

/// <summary>
/// Whether the session's <c>search_path</c> finds <c>pg_catalog</c>'s functions first, so that a function the plan
/// prints without its schema is the catalog's.
/// </summary>
/// <remarks>
/// PostgreSQL searches <c>pg_catalog</c> before the path unless the path names it, and then in the path's order.  A
/// plan prints a function bare whenever the session's path finds it under that name, so with
/// <c>shadow, pg_catalog</c> an aggregate <c>shadow.count(*)</c> prints as <c>count(*)</c>.  The path is the one
/// <c>open</c> read; a value that cannot be read is no answer.  <c>pg_temp</c>, which is never searched for a
/// function, is counted as any other schema, which costs an answer and is never wrong.
/// </remarks>
internal static class PostgresSearchPath
{
    /// <summary>Whether the path names <c>pg_catalog</c> first, or not at all.</summary>
    /// <param name="searchPath">The value of <c>search_path</c>, as <c>SHOW</c> gives it, or null.</param>
    public static bool PutsCatalogFirst(string? searchPath)
    {
        if (searchPath is null || Schemas(searchPath) is not { } schemas)
        {
            return false;
        }

        return schemas.FindIndex(IsCatalog) <= 0;
    }

    // A quoted name is as it is written, and a bare one is folded to lower case by the server.
    private static bool IsCatalog((string Name, bool Quoted) schema) =>
        string.Equals(
            schema.Name,
            SystemSchemas.Postgres,
            schema.Quoted ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase
        );

    // The schemas of the path in order, each with whether it was quoted, with "" read as one quote.  Null when a quote
    // does not close.
    private static List<(string Name, bool Quoted)>? Schemas(string searchPath)
    {
        var schemas = new List<(string Name, bool Quoted)>();
        var name = new StringBuilder();
        var quoted = false;
        var at = 0;
        while (at < searchPath.Length)
        {
            var character = searchPath[at];
            if (character == '"')
            {
                if (!PostgresIdentifiers.SkipQuoted(searchPath, at, out var end))
                {
                    return null;
                }

                _ = name.Append(PostgresIdentifiers.Unquote(searchPath.AsSpan(at, end - at)));
                quoted = true;
                at = end;
                continue;
            }

            at++;
            if (character == ',')
            {
                schemas.Add((name.ToString(), quoted));
                _ = name.Clear();
                quoted = false;
            }
            else if (!char.IsWhiteSpace(character))
            {
                _ = name.Append(character);
            }
        }

        schemas.Add((name.ToString(), quoted));
        return schemas;
    }
}
