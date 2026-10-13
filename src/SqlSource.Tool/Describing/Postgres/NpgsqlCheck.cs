using System;
using System.Collections.Generic;
using System.Globalization;

namespace SqlSource.Tool.Describing.Postgres;

/// <summary>What <c>check</c> found.</summary>
/// <param name="Result">
/// <see cref="NpgsqlCheck.Same" />, <see cref="NpgsqlCheck.Differ" /> or <see cref="NpgsqlCheck.Unbound" />.
/// </param>
/// <param name="Differs">What differs, or Npgsql's message of a type it cannot bind.  Null when nothing does.</param>
internal sealed record CheckVerdict(string Result, string? Differs);

/// <summary>
/// The check of the tool's lexer against Npgsql.  The describer prepares a query with <c>$n</c> where its lexer
/// found a parameter; generated code will send the SQL as written and let Npgsql find them.  Both describes give
/// columns, and the two saw one query when the columns are equal in count, in names and in types.
/// </summary>
/// <remarks>
/// The check is best effort.  A disagreement that leaves the columns alone, in a predicate for one, passes, and so
/// does a <c>$n</c> that the SQL writes itself.  <c>docs/tech-debt</c> records what it does not see.
/// </remarks>
internal static class NpgsqlCheck
{
    /// <summary>The values of the <c>result</c> member of the <c>check</c> event.</summary>
    public const string Same = "same";
    public const string Differ = "differ";
    public const string Unbound = "unbound";

    /// <summary>Reads the answer of <c>check</c> against the answer of <c>columns</c>.</summary>
    /// <param name="answer">What Npgsql's describe of the original SQL gave.</param>
    /// <param name="described">The columns of the prepared statement.</param>
    /// <param name="rows">The rows of the closure, by OID, for the name of a type that differs.</param>
    /// <exception cref="InvalidOperationException">The answer holds nothing.</exception>
    public static CheckVerdict Read(
        PostgresCheckAnswer answer,
        IReadOnlyList<PostgresColumnRow> described,
        IReadOnlyDictionary<long, PostgresTypeRow> rows
    )
    {
        if (answer.DriverError is { } unbound)
        {
            return new CheckVerdict(Unbound, unbound.Message);
        }

        if (answer.Error is { } error)
        {
            return new CheckVerdict(Differ, error.Message);
        }

        var seen =
            answer.Columns
            ?? throw new InvalidOperationException("The answer to 'check' holds no columns and no error.");
        return Difference(seen, described, rows) is { } differs
            ? new CheckVerdict(Differ, differs)
            : new CheckVerdict(Same, null);
    }

    // The first thing that differs.  A type is compared as the protocol reported it, which both describes see: for
    // a column of a domain that is the base.
    private static string? Difference(
        IReadOnlyList<PostgresCheckColumn> seen,
        IReadOnlyList<PostgresColumnRow> described,
        IReadOnlyDictionary<long, PostgresTypeRow> rows
    )
    {
        if (seen.Count != described.Count)
        {
            return Invariant($"the result has {seen.Count} columns for Npgsql and {described.Count} for SqlSource");
        }

        for (var index = 0; index < seen.Count; index++)
        {
            var (theirs, ours) = (seen[index], described[index]);
            if (!string.Equals(theirs.Name, ours.Name, StringComparison.Ordinal))
            {
                return Invariant(
                    $"column {index + 1} is named '{theirs.Name}' for Npgsql and '{ours.Name}' for SqlSource"
                );
            }

            if (theirs.TypeOid != ours.TypeOid)
            {
                var oursNamed = NameOf(ours.TypeOid, rows);
                return Invariant(
                    $"column {index + 1} is {theirs.DataTypeName} for Npgsql and {oursNamed} for SqlSource"
                );
            }
        }

        return null;
    }

    private static string NameOf(long oid, IReadOnlyDictionary<long, PostgresTypeRow> rows) =>
        rows.TryGetValue(oid, out var row) ? row.FormatType : oid.ToString(CultureInfo.InvariantCulture);

    private static string Invariant(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}
