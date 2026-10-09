using System.Security.Cryptography;
using System.Text;

namespace SqlSource.Parsing;

/// <summary>
/// The hash that ties a query to a description of it.
/// </summary>
/// <remarks>
/// SHA-256, in lower-case hex, of the UTF-8 bytes of: the engine's canonical name; a line feed; the query's SQL
/// without comments, with each token as <c>{{name:default}}</c>, <c>{{name:}}</c> for an empty default and
/// <c>{{name}}</c> for none; a line feed; and for each parameter that a <c>-- param:</c> marker declares, in the
/// order of the query's list, its name, a space and its type when it has one, <c> null</c> or <c> not null</c> when
/// the marker says so, and a line feed.  A comment changes no type and is not part of it; a parameter's name, a
/// default and a declaration are.  The definition is a contract with the files that hold a hash: changing it makes
/// every one of them stale.
/// </remarks>
internal static class SqlQueryHash
{
    private const string Hex = "0123456789abcdef";

    /// <summary>
    /// Computes the hash.  <paramref name="segments" /> is the comment-stripped SQL.  Nothing calls this while a
    /// file is parsed: a query that needs no description never pays for it.
    /// </summary>
    public static string Compute(
        SqlDialect dialect,
        EquatableArray<SqlSegment> segments,
        EquatableArray<SqlToken> tokens,
        EquatableArray<SqlQueryParameter> parameters
    )
    {
        var input = new StringBuilder();
        _ = input.Append(SqlDialectName.Canonical(dialect)).Append('\n');
        foreach (var segment in segments)
        {
            if (segment.Kind == SqlSegmentKind.Literal)
            {
                _ = input.Append(segment.Text);
                continue;
            }

            _ = input.Append("{{").Append(segment.Text);
            if (DefaultOf(tokens, segment.Text) is { } defaultText)
            {
                _ = input.Append(':').Append(defaultText);
            }

            _ = input.Append("}}");
        }

        _ = input.Append('\n');
        foreach (var parameter in parameters)
        {
            if (!parameter.IsDeclared)
            {
                continue;
            }

            _ = input.Append(parameter.Name);
            if (parameter.Type is not null)
            {
                _ = input.Append(' ').Append(parameter.Type);
            }

            if (parameter.Nullable is { } nullable)
            {
                _ = input.Append(nullable ? " null" : " not null");
            }

            _ = input.Append('\n');
        }

        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(input.ToString()));
        var hex = new char[hash.Length * 2];
        for (var index = 0; index < hash.Length; index++)
        {
            hex[index * 2] = Hex[hash[index] >> 4];
            hex[(index * 2) + 1] = Hex[hash[index] & 0xF];
        }

        return new string(hex);
    }

    private static string? DefaultOf(EquatableArray<SqlToken> tokens, string name)
    {
        foreach (var token in tokens)
        {
            if (token.Name == name)
            {
                return token.Default;
            }
        }

        return null;
    }
}
