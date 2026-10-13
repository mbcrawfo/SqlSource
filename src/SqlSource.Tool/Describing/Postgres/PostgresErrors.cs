using System;
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

    public const string NoSuchType = "the database has no such type";

    public const string NoSuchTypeHelp =
        "write a type the database has, schema-qualified if it is not on the search path";

    public const string FirstUseHelp =
        "declare the type with -- param:, or write the use that fixes the type before the one that does not: "
        + "(name = @name OR @name IS NULL)";

    public const string PseudoTypeReason = "it is a pseudo-type";

    public const string PseudoTypeHelp =
        "declare a type that a value can have: a pseudo-type such as record or anyelement stands for no value";

    private const string UndefinedObject = "42704";
    private const string IndeterminateDatatype = "42P18";
    private const string AmbiguousParameter = "42P08";

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
    /// What a failed <c>prepare</c> is reported as.  An error the server positions inside the type list is the
    /// declared type's, <c>SQLSRC228</c>; <c>42P18</c> and <c>42P08</c> name a parameter, <c>SQLSRC226</c> and
    /// <c>SQLSRC227</c>; anything else is the server rejecting the query, <c>SQLSRC225</c>.
    /// </summary>
    public static DescribeFailure Prepare(
        PostgresServerError error,
        DescribeRequest query,
        PostgresPrepareRequest prepare
    )
    {
        var head = ParameterPlaceholders.HeadLength(prepare.Statement, prepare.Types);
        var lines = ServerLines(error, head);
        if (DeclaredAt(error, query, prepare, head) is { } declared)
        {
            return error.SqlState == UndefinedObject
                ? DeclaredType(query, declared, NoSuchType, DescribeStep.DescribeParameters, lines, NoSuchTypeHelp)
                : DeclaredType(query, declared, NotATypeName, DescribeStep.DescribeParameters, lines, NotATypeNameHelp);
        }

        if (error.SqlState is IndeterminateDatatype or AmbiguousParameter && NamedIn(error.Message, query) is { } named)
        {
            var written = Written(named);
            return error.SqlState == IndeterminateDatatype
                ? new DescribeFailure(
                    ToolDiagnostics.ParameterTypeNotDetermined,
                    new EquatableArray<string>([written, query.Name]),
                    DescribeStep.DescribeParameters,
                    lines,
                    $"cast the parameter where it is used, {written}::int, or declare its type with "
                        + $"-- param: {written} int"
                )
                : new DescribeFailure(
                    ToolDiagnostics.ParameterFirstUseDoesNotFixType,
                    new EquatableArray<string>([written, query.Name]),
                    DescribeStep.DescribeParameters,
                    lines,
                    FirstUseHelp
                );
        }

        return Rejected(error, query, DescribeStep.DescribeParameters, lines);
    }

    /// <summary>
    /// A parameter as the SQL writes one: the server names it <c>$1</c>, and the user wrote <c>@id</c>.
    /// </summary>
    public static string Written(SqlQueryParameter parameter) =>
        SqlDialectRules.PostgreSql.ParameterPrefix + parameter.Name;

    /// <summary><c>SQLSRC225</c> at the step <c>resolve type</c>: the catalog could not be read.</summary>
    public static DescribeFailure Types(PostgresServerError error, DescribeRequest query) =>
        Rejected(error, query, DescribeStep.ResolveType, ServerLines(error, 0));

    /// <summary>
    /// A parameter whose type is a pseudo-type.  The server accepts one and says nothing, so there is no server
    /// line.  A declared one is <c>SQLSRC228</c>; an inferred one is <c>SQLSRC226</c>, whose help says what the
    /// server gave.
    /// </summary>
    /// <param name="query">The query.</param>
    /// <param name="parameter">The parameter.</param>
    /// <param name="typeName">The pseudo-type, as <c>format_type</c> spells it.</param>
    public static DescribeFailure PseudoType(DescribeRequest query, SqlQueryParameter parameter, string typeName)
    {
        if (parameter.Type is not null)
        {
            return DeclaredType(
                query,
                parameter,
                PseudoTypeReason,
                DescribeStep.ResolveType,
                EquatableArray<string>.Empty,
                PseudoTypeHelp
            );
        }

        var written = Written(parameter);
        return new DescribeFailure(
            ToolDiagnostics.ParameterTypeNotDetermined,
            new EquatableArray<string>([written, query.Name]),
            DescribeStep.ResolveType,
            EquatableArray<string>.Empty,
            $"the server gave it the pseudo-type '{typeName}', which no value has: cast the parameter where it is "
                + $"used, or declare its type with -- param: {written} <type>"
        );
    }

    // SQLSRC225.  The server's lines say what is wrong, so there is no help.
    private static DescribeFailure Rejected(
        PostgresServerError error,
        DescribeRequest query,
        DescribeStep step,
        EquatableArray<string> serverLines
    ) =>
        new(
            ToolDiagnostics.ServerRejectedQuery,
            new EquatableArray<string>([query.Name, error.Message]),
            step,
            serverLines,
            null
        );

    // The parameter whose declared type the server's position falls in, or null: for no position, one in the SQL,
    // and one at an item that is the tool's own "unknown".
    private static SqlQueryParameter? DeclaredAt(
        PostgresServerError error,
        DescribeRequest query,
        PostgresPrepareRequest prepare,
        int head
    )
    {
        if (error.Position is not { } position || position > head || prepare.Types.Count == 0)
        {
            return null;
        }

        var parameter = query.Parameters[ParameterPlaceholders.TypeAt(prepare.Statement, prepare.Types, position)];
        return parameter.Type is null ? null : parameter;
    }

    // The parameter of the first "$n" of a message, or null when it has none or n is no ordinal of the list.
    private static SqlQueryParameter? NamedIn(string message, DescribeRequest query)
    {
        var dollar = message.IndexOf('$', StringComparison.Ordinal);
        while (dollar >= 0)
        {
            var end = dollar + 1;
            while (end < message.Length && char.IsAsciiDigit(message[end]))
            {
                end++;
            }

            if (
                int.TryParse(
                    message.AsSpan(dollar + 1, end - dollar - 1),
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out var ordinal
                )
            )
            {
                return ordinal >= 1 && ordinal <= query.Parameters.Count ? query.Parameters[ordinal - 1] : null;
            }

            dollar = message.IndexOf('$', end);
        }

        return null;
    }

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
