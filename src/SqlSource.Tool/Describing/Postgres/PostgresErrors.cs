using System.Collections.Generic;
using System.Globalization;
using SqlSource.Diagnostics;
using SqlSource.Tool.Describing.Describers;

namespace SqlSource.Tool.Describing.Postgres;

/// <summary>
/// What the server said, or what the tool refuses before it asks, as the failure a run reports at the query.  One
/// id for each cause a user mends differently.
/// </summary>
internal static class PostgresErrors
{
    public const string OpenHelp = "check that the database is running and that the connection string names it";

    /// <summary>
    /// <c>SQLSRC224</c>: no session.  Every failure of <c>open</c> is this one, whatever its SQLSTATE: a wrong
    /// password and a missing database are not the server rejecting a query.
    /// </summary>
    public static DescribeFailure Open(PostgresServerError error, string database) =>
        new(
            ToolDiagnostics.DatabaseCannotBeOpened,
            new EquatableArray<string>([database]),
            null,
            ServerLines(error, 0),
            OpenHelp
        );

    /// <summary>
    /// The <c>server:</c> lines, verbatim and in this order: the SQLSTATE and the message, the detail, the hint, and
    /// the character the error is at.
    /// </summary>
    /// <param name="error">What the server said.</param>
    /// <param name="head">
    /// How many characters of the text that was sent stand before the user's SQL.  The position line counts from
    /// the SQL, and there is none for a position that is not in it.
    /// </param>
    public static EquatableArray<string> ServerLines(PostgresServerError error, int head)
    {
        var lines = new List<string> { error.SqlState is { } state ? $"{state}: {error.Message}" : error.Message };
        if (error.Detail is { } detail)
        {
            lines.Add($"detail: {detail}");
        }

        if (error.Hint is { } hint)
        {
            lines.Add($"hint: {hint}");
        }

        if (error.Position is { } position && position > head)
        {
            lines.Add(string.Create(CultureInfo.InvariantCulture, $"at character {position - head}"));
        }

        return new EquatableArray<string>([.. lines]);
    }
}
