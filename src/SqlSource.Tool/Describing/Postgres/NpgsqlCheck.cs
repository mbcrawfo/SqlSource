using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using SqlSource.Parsing;
using SqlSource.Tool.Describing.Postgres.Statements;

namespace SqlSource.Tool.Describing.Postgres;

/// <summary>What <c>check</c> found.</summary>
/// <param name="Result">
/// <see cref="NpgsqlCheck.Same" />, <see cref="NpgsqlCheck.Differ" />, <see cref="NpgsqlCheck.Unbound" /> or
/// <see cref="NpgsqlCheck.Error" />.
/// </param>
/// <param name="Differs">
/// What differs, Npgsql's message of a type it cannot bind, or the message of an error that is no disagreement.  Null
/// when nothing does.
/// </param>
/// <param name="TypeDiffers">
/// True when what differs is the type of a column.  A difference of where the parameters stand is no difference of
/// a type.
/// </param>
/// <param name="PlacementDiffers">
/// True when what differs is where the parameters stand: Npgsql's text and the tool's own rewrite part, with no error
/// of the server.
/// </param>
internal sealed record CheckVerdict(
    string Result,
    string? Differs,
    bool TypeDiffers = false,
    bool PlacementDiffers = false
);

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
    /// <summary>
    /// The values of the <c>result</c> member of the <c>check</c> event: the two describes agree; they differ, which
    /// an error of the server can say; Npgsql cannot bind a parameter's type; or <c>check</c> failed with an error
    /// that says nothing of how Npgsql reads the SQL, such as a lost connection or a cancel.
    /// </summary>
    public const string Same = "same";
    public const string Differ = "differ";
    public const string Unbound = "unbound";
    public const string Error = "error";

    private const string SyntaxOrAccessClass = "42";

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
            return new CheckVerdict(ReadsDifferently(error, answer.TextRefused) ? Differ : Error, error.Message);
        }

        var seen =
            answer.Columns
            ?? throw new InvalidOperationException("The answer to 'check' holds no columns and no error.");
        if (answer.Sent is { } sent && Placement(sent, sql, parameters) is { } placed)
        {
            return new CheckVerdict(Differ, placed, PlacementDiffers: true);
        }

        return OfColumns(seen, described, rows);
    }

    // Whether an error of the server is what a misread @ gives: a syntax error or an access rule violation, class 42.
    // An error without a SQLSTATE is either Npgsql's refusal of the text, which a misread @ can give, or a lost
    // connection; textRefused says which, and is null in an answer recorded before it existed, whose error without a
    // SQLSTATE was the refusal.
    private static bool ReadsDifferently(PostgresServerError error, bool? textRefused) =>
        error.SqlState is { } state
            ? state.StartsWith(SyntaxOrAccessClass, StringComparison.Ordinal)
            : textRefused ?? true;

    // Where the two texts first part, or null.
    private static string? Placement(PostgresCheckSent sent, string sql, EquatableArray<SqlQueryParameter> parameters)
    {
        var order = OrderOf(sent, parameters);
        var ours = (
            ParameterPlaceholders.Rewrite(sql, order)
            ?? throw new InvalidOperationException("The SQL of a query was prepared and holds more than one statement.")
        ).TrimEnd();
        var theirs = sent.Text.TrimEnd();
        return PartAt(ours, theirs) is { } at ? Parting(ours, theirs, at, order, parameters) : null;
    }

    // The tool's rewrite numbers a parameter by its place in the list it is given, so it is given the query's
    // parameters in Npgsql's order: those Npgsql numbered, then the others, which neither text holds.  A name Npgsql
    // numbered that the query lacks keeps its number with a parameter of its own, which the tool's rewrite never
    // places, so that the parameters after it keep theirs.
    private static EquatableArray<SqlQueryParameter> OrderOf(
        PostgresCheckSent sent,
        EquatableArray<SqlQueryParameter> parameters
    )
    {
        var order = new List<SqlQueryParameter>(parameters.Count);
        foreach (var placed in sent.Parameters)
        {
            var name = placed.StartsWith('@') || placed.StartsWith(':') ? placed[1..] : placed;
            var parameter =
                parameters.FirstOrDefault(parameter =>
                    string.Equals(parameter.Name, name, StringComparison.OrdinalIgnoreCase)
                ) ?? new SqlQueryParameter(name, null, null, false);
            if (!order.Contains(parameter))
            {
                order.Add(parameter);
            }
        }

        order.AddRange(parameters.Where(parameter => !order.Contains(parameter)));
        return new EquatableArray<SqlQueryParameter>([.. order]);
    }

    // The index of the first character where the two texts part, or null when they are equal.  Two numbers that
    // differ part at their "$".
    private static int? PartAt(string ours, string theirs)
    {
        var at = 0;
        while (at < ours.Length && at < theirs.Length && ours[at] == theirs[at])
        {
            at++;
        }

        if (at == ours.Length && at == theirs.Length)
        {
            return null;
        }

        var digits = at;
        while (digits > 0 && char.IsAsciiDigit(ours[digits - 1]))
        {
            digits--;
        }

        return digits > 0 && ours[digits - 1] == '$' ? digits - 1 : at;
    }

    // Where the two texts part, and what each holds there; or, where Npgsql placed a parameter the query lacks, its
    // name.
    private static string Parting(
        string ours,
        string theirs,
        int at,
        EquatableArray<SqlQueryParameter> order,
        EquatableArray<SqlQueryParameter> parameters
    )
    {
        var where = string.Create(CultureInfo.InvariantCulture, $"the SQL with $n parts at character {at + 1}");
        if (
            PostgresIdentifiers.ParameterNumberAt(theirs, at) is { } number
            && number >= 1
            && number <= order.Count
            && !parameters.Contains(order[number - 1])
        )
        {
            var placed = order[number - 1].Name;
            return $"{where}: Npgsql placed @{placed} there, which SqlSource does not read as a parameter";
        }

        return $"{where}: '{Quote(theirs, at)}' for Npgsql and '{Quote(ours, at)}' for SqlSource";
    }

    private static string Quote(string text, int from) => text.Substring(from, Math.Min(Quoted, text.Length - from));

    // The verdict of the columns: the first thing that differs, or the same.  A type is compared as the protocol
    // reported it, which both describes see: for a column of a domain that is the base.
    private static CheckVerdict OfColumns(
        IReadOnlyList<PostgresCheckColumn> seen,
        IReadOnlyList<PostgresColumnRow> described,
        IReadOnlyDictionary<long, PostgresTypeRow> rows
    )
    {
        if (seen.Count != described.Count)
        {
            return new CheckVerdict(
                Differ,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"the result has {seen.Count} columns for Npgsql and {described.Count} for SqlSource"
                )
            );
        }

        for (var index = 0; index < seen.Count; index++)
        {
            var (theirs, ours) = (seen[index], described[index]);
            if (!string.Equals(theirs.Name, ours.Name, StringComparison.Ordinal))
            {
                return new CheckVerdict(
                    Differ,
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"column {index + 1} is named '{theirs.Name}' for Npgsql and '{ours.Name}' for SqlSource"
                    )
                );
            }

            if (theirs.TypeOid != ours.TypeOid)
            {
                var oursNamed = NameOf(ours.TypeOid, rows);
                return new CheckVerdict(
                    Differ,
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"column {index + 1} is {theirs.DataTypeName} for Npgsql and {oursNamed} for SqlSource"
                    ),
                    TypeDiffers: true
                );
            }
        }

        return new CheckVerdict(Same, null);
    }

    private static string NameOf(long oid, IReadOnlyDictionary<long, PostgresTypeRow> rows) =>
        rows.TryGetValue(oid, out var row) ? row.FormatType : oid.ToString(CultureInfo.InvariantCulture);
}
