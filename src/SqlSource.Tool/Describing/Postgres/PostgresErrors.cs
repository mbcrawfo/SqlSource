using System.Collections.Generic;
using System.Globalization;
using SqlSource.Diagnostics;
using SqlSource.Parsing;
using SqlSource.Tool.Describing.Describers;

namespace SqlSource.Tool.Describing.Postgres;

/// <summary>
/// What the server said, or what the tool refuses before it asks, as the failure a run reports at the query.  One
/// id for each cause a user mends differently.
/// </summary>
internal static class PostgresErrors
{
    public const string OpenHelp = "check that the database is running and that the connection string names it";

    public const string NotATypeName = "it is not written as a type name";

    public const string NotATypeNameHelp =
        "write the type as a column definition would: a name, a modifier in parentheses, []";

    public const string SeveralStatementsHelp = "one statement for each query; a ; is allowed at the end alone";

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

    /// <summary><c>SQLSRC230</c>: the text holds a second statement.  Nothing of it is sent.</summary>
    public static DescribeFailure SeveralStatements(DescribeRequest query) =>
        new(
            ToolDiagnostics.QueryHasSeveralStatements,
            new EquatableArray<string>([query.Name]),
            DescribeStep.DescribeParameters,
            EquatableArray<string>.Empty,
            SeveralStatementsHelp
        );

    /// <summary>
    /// <c>SQLSRC228</c> for a type that <see cref="DeclaredTypeText" /> refuses.  Nothing of the query is sent.
    /// </summary>
    public static DescribeFailure TypeRefused(DescribeRequest query, SqlQueryParameter parameter) =>
        DeclaredType(
            query,
            parameter,
            NotATypeName,
            DescribeStep.DescribeParameters,
            EquatableArray<string>.Empty,
            NotATypeNameHelp
        );

    /// <summary>
    /// A parameter as the SQL writes one: the server names it <c>$1</c>, and the user wrote <c>@id</c>.
    /// </summary>
    public static string Written(SqlQueryParameter parameter) =>
        SqlDialectRules.PostgreSql.ParameterPrefix + parameter.Name;

    // SQLSRC228, whose last argument is the reason the type cannot be used.
    private static DescribeFailure DeclaredType(
        DescribeRequest query,
        SqlQueryParameter parameter,
        string reason,
        DescribeStep step,
        EquatableArray<string> serverLines,
        string help
    ) =>
        new(
            ToolDiagnostics.DeclaredTypeNotValid,
            new EquatableArray<string>([parameter.Type ?? string.Empty, Written(parameter), query.Name, reason]),
            step,
            serverLines,
            help
        );
}
