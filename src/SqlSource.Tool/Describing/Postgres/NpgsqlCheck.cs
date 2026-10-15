using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using SqlSource.Parsing;

namespace SqlSource.Tool.Describing.Postgres;

/// <summary>What <c>check</c> found.</summary>
/// <param name="Result">
/// <see cref="NpgsqlCheck.Same" />, <see cref="NpgsqlCheck.Differ" /> or <see cref="NpgsqlCheck.Unbound" />.
/// </param>
/// <param name="Differs">What differs, or Npgsql's message of a type it cannot bind.  Null when nothing does.</param>
/// <param name="TypeDiffers">
/// True when what differs is the type of a column.  A difference of where the parameters stand is no difference of
/// a type.
/// </param>
internal sealed record CheckVerdict(string Result, string? Differs, bool TypeDiffers = false);

/// <summary>
/// The check of the tool's lexer against Npgsql.  The describer prepares a query with <c>$n</c> where its lexer
/// found a parameter; generated code will send the SQL as written and let Npgsql find them.  The two saw one query
/// when Npgsql placed each parameter where the lexer did, and both describes gave columns equal in count, in names
/// and in types.
/// </summary>
/// <remarks>
/// The places of the parameters are compared in two texts: the one Npgsql sent, and the tool's own rewrite of the
/// SQL with each parameter numbered as Npgsql numbered it.  A place one reads as a parameter and the other as text
/// differs even where the server accepts both and the columns agree: <c>tags[:id]</c> in a predicate, with a column
/// <c>id</c>, which Npgsql sends as <c>tags[$1]</c>.
/// What the check still cannot see is a <c>$n</c> that the SQL writes itself, which <c>SQLSRC234</c> refuses before.
/// An answer recorded before it held Npgsql's text is compared by its columns alone.
/// </remarks>
internal static class NpgsqlCheck
{
    /// <summary>The values of the <c>result</c> member of the <c>check</c> event.</summary>
    public const string Same = "same";
    public const string Differ = "differ";
    public const string Unbound = "unbound";

    // How many characters of each text a difference quotes, from where the two part.
    private const int Quoted = 10;

    /// <summary>
    /// Reads the answer of <c>check</c> against the tool's own rewrite of the SQL and the answer of <c>columns</c>.
    /// </summary>
    /// <param name="answer">What Npgsql's describe of the original SQL gave.</param>
    /// <param name="described">The columns of the prepared statement.</param>
    /// <param name="rows">The rows of the closure, by OID, for the name of a type that differs.</param>
    /// <param name="sql">The query's SQL, for the tool's own rewrite.</param>
    /// <param name="parameters">The query's parameters, for the tool's own rewrite.</param>
    /// <exception cref="InvalidOperationException">
    /// The answer holds nothing, or the SQL holds more than one statement, which <c>prepare</c> refuses first.
    /// </exception>
    public static CheckVerdict Read(
        PostgresCheckAnswer answer,
        IReadOnlyList<PostgresColumnRow> described,
        IReadOnlyDictionary<long, PostgresTypeRow> rows,
        string sql,
        EquatableArray<SqlQueryParameter> parameters
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
        if (answer.Sent is { } sent && Placement(sent, sql, parameters) is { } placed)
        {
            return new CheckVerdict(Differ, placed);
        }

        var (differs, isType) = Difference(seen, described, rows);
        return differs is null ? new CheckVerdict(Same, null) : new CheckVerdict(Differ, differs, isType);
    }

    // Where the two texts first part, or null.  The tool's rewrite numbers a parameter by its place in the list it is
    // given, so it is given the query's parameters in Npgsql's order: those Npgsql numbered, then the others, which
    // neither text holds.
    private static string? Placement(PostgresCheckSent sent, string sql, EquatableArray<SqlQueryParameter> parameters)
    {
        var order = new List<SqlQueryParameter>(parameters.Count);
        foreach (var placed in sent.Parameters)
        {
            var name = placed.StartsWith('@') || placed.StartsWith(':') ? placed[1..] : placed;
            var parameter = parameters.FirstOrDefault(parameter =>
                string.Equals(parameter.Name, name, StringComparison.OrdinalIgnoreCase)
            );
            if (parameter is null)
            {
                return $"Npgsql placed @{name}, which SqlSource does not read as a parameter";
            }

            if (!order.Contains(parameter))
            {
                order.Add(parameter);
            }
        }

        order.AddRange(parameters.Where(parameter => !order.Contains(parameter)));
        var ours = (
            ParameterPlaceholders.Rewrite(sql, new EquatableArray<SqlQueryParameter>([.. order]))
            ?? throw new InvalidOperationException("The SQL of a query was prepared and holds more than one statement.")
        ).TrimEnd();
        var theirs = sent.Text.TrimEnd();
        var at = 0;
        while (at < ours.Length && at < theirs.Length && ours[at] == theirs[at])
        {
            at++;
        }

        if (at == ours.Length && at == theirs.Length)
        {
            return null;
        }

        // Two numbers that differ are quoted from their "$".
        var digits = at;
        while (digits > 0 && char.IsAsciiDigit(ours[digits - 1]))
        {
            digits--;
        }

        if (digits > 0 && ours[digits - 1] == '$')
        {
            at = digits - 1;
        }

        var (npgsql, sqlSource) = (Quote(theirs, at), Quote(ours, at));
        return Invariant(
            $"the SQL with $n parts at character {at + 1}: '{npgsql}' for Npgsql and '{sqlSource}' for SqlSource"
        );
    }

    private static string Quote(string text, int from) => text.Substring(from, Math.Min(Quoted, text.Length - from));

    // The first thing that differs, and whether it is a type.  A type is compared as the protocol reported it, which
    // both describes see: for a column of a domain that is the base.
    private static (string? Differs, bool IsType) Difference(
        IReadOnlyList<PostgresCheckColumn> seen,
        IReadOnlyList<PostgresColumnRow> described,
        IReadOnlyDictionary<long, PostgresTypeRow> rows
    )
    {
        if (seen.Count != described.Count)
        {
            return (
                Invariant($"the result has {seen.Count} columns for Npgsql and {described.Count} for SqlSource"),
                false
            );
        }

        for (var index = 0; index < seen.Count; index++)
        {
            var (theirs, ours) = (seen[index], described[index]);
            if (!string.Equals(theirs.Name, ours.Name, StringComparison.Ordinal))
            {
                return (
                    Invariant(
                        $"column {index + 1} is named '{theirs.Name}' for Npgsql and '{ours.Name}' for SqlSource"
                    ),
                    false
                );
            }

            if (theirs.TypeOid != ours.TypeOid)
            {
                var oursNamed = NameOf(ours.TypeOid, rows);
                return (
                    Invariant($"column {index + 1} is {theirs.DataTypeName} for Npgsql and {oursNamed} for SqlSource"),
                    true
                );
            }
        }

        return (null, false);
    }

    private static string NameOf(long oid, IReadOnlyDictionary<long, PostgresTypeRow> rows) =>
        rows.TryGetValue(oid, out var row) ? row.FormatType : oid.ToString(CultureInfo.InvariantCulture);

    private static string Invariant(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}
