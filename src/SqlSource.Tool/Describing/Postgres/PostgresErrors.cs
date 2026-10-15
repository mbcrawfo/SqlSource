using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using SqlSource.Diagnostics;
using SqlSource.Parsing;
using SqlSource.Snapshot;
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
    /// Built from <see cref="PostgresSession.MinimumMajorVersion" />, so that the floor is one constant.
    /// </summary>
    public static readonly string VersionNotSupportedHelp =
        "upgrade the server, or describe against a database of PostgreSQL "
        + PostgresSession.MinimumMajorVersion.ToString(CultureInfo.InvariantCulture)
        + " or later";

    public const string SessionNotKeptHelp =
        "connect directly to the server, not through a pooler in transaction mode, and without Multiplexing=true";

    public const string NotATypeName = "it is not written as a type name";

    public const string NotATypeNameHelp =
        "write the type as a column definition would: a name, a modifier in parentheses, []";

    public const string SeveralStatementsHelp = "one statement for each query; a ; is allowed at the end alone";

    public const string NotDescribableHelp =
        "give the query -- output: sql; the tool describes SELECT without INTO, INSERT, UPDATE, DELETE, MERGE, VALUES "
        + "and TABLE";

    public const string PositionalParameterHelp =
        "write the parameter as @name; generated code binds parameters by name";

    public const string NoSuchType = "the database has no such type";

    public const string NoSuchTypeHelp =
        "write a type the database has, schema-qualified if it is not on the search path";

    public const string FirstUseHelp =
        "declare the type with -- param:, or write the use that fixes the type before the one that does not: "
        + "(name = @name OR @name IS NULL)";

    public const string PseudoTypeReason = "it is a pseudo-type";

    public const string PseudoTypeHelp =
        "declare a type that a value can have: a pseudo-type such as record or anyelement stands for no value";

    public const string ColumnTypeHelp =
        "cast the column to a concrete type, or give the expression an alias and a cast";

    public const string CheckHelp =
        "a @ that one of the two reads as a parameter and the other as text; the server's lines say where; a table, a "
        + "column or a type that another session changed during the run shows the same way, so run describe again";

    public const string PlacementHelp =
        "a @ or a : that one of the two reads as a parameter and the other as text; the message says where the two "
        + "texts part";

    public const string ArrayOfDomainHelp =
        "Npgsql sends a parameter that is an array of a domain as an array of the domain's base type, so the column "
        + "it is returned as has another type; cast the column, @p::varchar[] for an array of a domain over varchar";

    public const string BindHelp =
        "Npgsql cannot send a value of this type; declare the parameter as text with -- param: and cast it to the "
        + "type where it is used; a later version may let a project declare a conversion for the type";

    public const string UnnamedHelp =
        "the type has no name that Npgsql can be given without ambiguity; rename the schema or the type, or declare "
        + "the parameter as text with -- param: and cast it to the type where it is used";

    public const string SchemaChangedHelp =
        "the database's schema changed while the query was described; run the tool again";

    private const string UndefinedObject = "42704";
    private const string UtilityStatement = "42P14";
    private const string DuplicatePreparedStatement = "42P05";
    public const string InvalidSqlStatementName = "26000";
    private const string IndeterminateDatatype = "42P18";
    private const string AmbiguousParameter = "42P08";
    private const string UndefinedFunction = "42883";
    private const string DatatypeMismatch = "42804";
    private const string AmbiguousFunction = "42725";

    /// <summary>
    /// <c>SQLSRC224</c>: no session.  Every failure of <c>open</c> is this one, whatever its SQLSTATE: a wrong
    /// password and a missing database are not the server rejecting a query.  The one exception is the tool's own
    /// refusal of <c>Multiplexing=true</c>, <c>SQLSRC236</c>.
    /// </summary>
    public static DescribeFailure Open(PostgresServerError error, string database) =>
        error.SqlState is null && error.Message == PostgresConnection.MultiplexingRefused
            ? SessionNotKept(database, null, ServerLines(error, 0))
            : new(
                ToolDiagnostics.DatabaseCannotBeOpened,
                new EquatableArray<string>([database]),
                null,
                ServerLines(error, 0),
                OpenHelp
            );

    /// <summary>
    /// <c>SQLSRC236</c>: the connection does not keep one session, so the statement the tool prepared is not there
    /// for a later call.  At <c>open</c> it is a connection string that sets <c>Multiplexing=true</c>; at a query it
    /// is a pooler in transaction mode.
    /// </summary>
    /// <param name="database">The database.</param>
    /// <param name="step">The step whose call found out, or null at <c>open</c>.</param>
    /// <param name="serverLines">What the server or the tool said, verbatim.</param>
    public static DescribeFailure SessionNotKept(
        string database,
        DescribeStep? step,
        EquatableArray<string> serverLines
    ) =>
        new(
            ToolDiagnostics.SessionNotKept,
            new EquatableArray<string>([database]),
            step,
            serverLines,
            SessionNotKeptHelp
        );

    /// <summary>
    /// <c>SQLSRC235</c>: the server is older than <see cref="PostgresSession.MinimumMajorVersion" />.  Nothing of a
    /// query is sent, and the failure holds no server line: the server said nothing.
    /// </summary>
    /// <param name="database">The database.</param>
    /// <param name="version">The server's version, as it is recorded.</param>
    public static DescribeFailure VersionNotSupported(string database, string version) =>
        new(
            ToolDiagnostics.ServerVersionNotSupported,
            new EquatableArray<string>([
                database,
                version,
                PostgresSession.MinimumMajorVersion.ToString(CultureInfo.InvariantCulture),
            ]),
            null,
            EquatableArray<string>.Empty,
            VersionNotSupportedHelp
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
    /// <c>SQLSRC233</c>: the statement is of a kind that <c>PREPARE</c> does not take.  Nothing of it is sent.
    /// </summary>
    /// <param name="query">The query.</param>
    /// <param name="kind">The kind, in upper case, as <see cref="StatementShape.RefusedKind" /> gives it.</param>
    public static DescribeFailure NotDescribable(DescribeRequest query, string kind) =>
        new(
            ToolDiagnostics.StatementCannotBeDescribed,
            new EquatableArray<string>([query.Name, kind]),
            DescribeStep.DescribeParameters,
            EquatableArray<string>.Empty,
            NotDescribableHelp
        );

    /// <summary>
    /// <c>SQLSRC234</c>: the SQL writes a positional parameter itself.  Nothing of it is sent.
    /// </summary>
    /// <param name="query">The query.</param>
    /// <param name="written">The parameter as written, such as <c>$2</c>.</param>
    public static DescribeFailure PositionalParameter(DescribeRequest query, string written) =>
        new(
            ToolDiagnostics.PositionalParameterWritten,
            new EquatableArray<string>([query.Name, written]),
            DescribeStep.DescribeParameters,
            EquatableArray<string>.Empty,
            PositionalParameterHelp
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
    /// What a failed <c>prepare</c> is reported as.  A statement that is not listed after it, and <c>42P05</c>, a
    /// statement that already exists, are a connection that does not keep a session, <c>SQLSRC236</c>, whichever
    /// the position; <c>42P14</c>, a utility statement, is <c>SQLSRC233</c>, as a backstop for what
    /// <see cref="StatementShape" /> misses; an error the server positions inside the type list is the declared
    /// type's, <c>SQLSRC228</c>; <c>42P18</c> and <c>42P08</c> name a parameter, <c>SQLSRC226</c> and
    /// <c>SQLSRC227</c>; anything else is the server rejecting the query, <c>SQLSRC225</c>.  <c>42P05</c> is
    /// matched by its SQLSTATE alone: the message may be translated.
    /// </summary>
    public static DescribeFailure Prepare(
        PostgresServerError error,
        DescribeRequest query,
        PostgresPrepareRequest prepare,
        string database
    )
    {
        // Before anything is counted against the PREPARE's text: neither is a fault of it.
        if (
            error.SqlState == DuplicatePreparedStatement
            || (error.SqlState is null && error.Message == PostgresLive.StatementNotListed)
        )
        {
            return SessionNotKept(
                database,
                DescribeStep.DescribeParameters,
                ServerLines(error with { Position = null }, 0)
            );
        }

        var head = ParameterPlaceholders.HeadLength(prepare.Statement, prepare.Types);
        var lines = ServerLines(error, head);
        if (error.SqlState == UtilityStatement)
        {
            // The server did not say which statement it was, so the kind is no word of one.
            return NotDescribable(query, "utility") with { ServerLines = lines };
        }

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

        // A parameter with no declared type takes the type of its first use, and a type error may be the fault of that.
        var undeclared = query.Parameters.Where(p => p.Type is null).ToList();
        return Rejected(
            error,
            query,
            DescribeStep.DescribeParameters,
            lines,
            error.SqlState is UndefinedFunction or DatatypeMismatch or AmbiguousFunction && undeclared.Count > 0
                ? InferredParameterHelp(undeclared)
                : null
        );
    }

    /// <summary>
    /// The help of <c>SQLSRC225</c> for a type error in a query that has parameters of no declared type: the server
    /// gave each the type of its first use.  The example is the first of them, with <c>&lt;type&gt;</c> in the place of
    /// a type: the tool cannot know the type meant, and a guess such as <c>int</c> can be what gave the error.
    /// </summary>
    /// <param name="undeclared">
    /// The parameters whose type no marker declares, in the order of the query: one at least.
    /// </param>
    /// <exception cref="ArgumentException"><paramref name="undeclared"/> is empty.</exception>
    public static string InferredParameterHelp(IReadOnlyList<SqlQueryParameter> undeclared)
    {
        if (undeclared.Count == 0)
        {
            throw new ArgumentException("The help names one parameter at least.", nameof(undeclared));
        }

        var first = Written(undeclared[0]);
        var names = undeclared.Count == 1 ? first : "each of " + string.Join(", ", undeclared.Select(Written));
        return $"the server gave {names} the type of its first use, which may not be the type meant: cast the "
            + $"parameter where it is used to the type meant, {first}::<type>, or declare its type with "
            + $"-- param: {first} <type>";
    }

    /// <summary>
    /// A parameter as the SQL writes one: the server names it <c>$1</c>, and the user wrote <c>@id</c>.
    /// </summary>
    public static string Written(SqlQueryParameter parameter) =>
        SqlDialectRules.PostgreSql.ParameterPrefix + parameter.Name;

    /// <summary>
    /// <c>SQLSRC225</c> at the step <c>resolve type</c>: the catalog could not be read.  There is no position line:
    /// a position the server gives is in the tool's own query of the catalog, and not in the user's SQL.
    /// </summary>
    public static DescribeFailure Types(PostgresServerError error, DescribeRequest query) =>
        Rejected(error, query, DescribeStep.ResolveType, ServerLines(error with { Position = null }, 0), null);

    /// <summary>
    /// <c>SQLSRC225</c> at the step <c>describe columns</c>: the prepared statement could not be described.  There
    /// is no position line: a position the server gives is in the tool's own <c>EXECUTE</c>.  <c>26000</c>, a
    /// statement that does not exist, is <c>SQLSRC236</c>: the tool's text holds only its own statement's name, and
    /// the SQLSTATE alone is matched, since the message may be translated.
    /// </summary>
    public static DescribeFailure Columns(PostgresServerError error, DescribeRequest query, string database)
    {
        var lines = ServerLines(error with { Position = null }, 0);
        return error.SqlState == InvalidSqlStatementName
            ? SessionNotKept(database, DescribeStep.DescribeColumns, lines)
            : Rejected(error, query, DescribeStep.DescribeColumns, lines, null);
    }

    /// <summary>
    /// <c>SQLSRC225</c> at the step <c>catalog</c>: the tables of the columns could not be read.  There is no
    /// position line: a position the server gives is in the tool's own query of the catalog.
    /// </summary>
    public static DescribeFailure Tables(PostgresServerError error, DescribeRequest query) =>
        Rejected(error, query, DescribeStep.Catalog, ServerLines(error with { Position = null }, 0), null);

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

    /// <summary>
    /// <c>SQLSRC229</c>: a column whose type is a pseudo-type.  The server describes such a column and says nothing,
    /// so there is no server line.
    /// </summary>
    /// <param name="query">The query.</param>
    /// <param name="column">The column's name, as the server returned it.</param>
    /// <param name="typeName">The pseudo-type, as <c>format_type</c> spells it.</param>
    public static DescribeFailure ColumnPseudoType(DescribeRequest query, string column, string typeName) =>
        new(
            ToolDiagnostics.ColumnTypeNotSupported,
            new EquatableArray<string>([column, query.Name, typeName]),
            DescribeStep.ResolveType,
            EquatableArray<string>.Empty,
            ColumnTypeHelp
        );

    /// <summary>
    /// What <c>check</c> is reported as, from the verdict, or null when the two describes agree.  A type Npgsql
    /// cannot bind is <c>SQLSRC232</c>, with Npgsql's message as its one line; an error that is no disagreement,
    /// <see cref="NpgsqlCheck.Error" />, is the server rejecting the query, <c>SQLSRC225</c>; anything else that
    /// differs is <c>SQLSRC231</c>, with the lines of the error when there is one.  There is no position line: a
    /// position the server gives is in Npgsql's own rewrite of the SQL.
    /// </summary>
    /// <param name="query">The query.</param>
    /// <param name="parameters">The parameters as the describer typed them, in the order of the query's list.</param>
    /// <param name="asked">The request of <c>check</c>.</param>
    /// <param name="answer">Its answer.</param>
    /// <param name="verdict">What <see cref="NpgsqlCheck.Read" /> made of the answer.</param>
    public static DescribeFailure? Check(
        DescribeRequest query,
        IReadOnlyList<DescribedParameter> parameters,
        PostgresCheckRequest asked,
        PostgresCheckAnswer answer,
        CheckVerdict verdict
    )
    {
        if (verdict.Result == NpgsqlCheck.Same)
        {
            return null;
        }

        if (answer.DriverError is { } unbound)
        {
            return CannotBind(query, parameters, asked, unbound);
        }

        var lines = answer.Error is { } error
            ? ServerLines(error with { Position = null }, 0)
            : EquatableArray<string>.Empty;
        if (verdict.Result == NpgsqlCheck.Error && answer.Error is { } failed)
        {
            return Rejected(failed, query, DescribeStep.CheckParameters, lines, null);
        }

        return new DescribeFailure(
            ToolDiagnostics.ParametersReadDifferently,
            new EquatableArray<string>([query.Name, verdict.Differs ?? string.Empty]),
            DescribeStep.CheckParameters,
            lines,
            HelpOf(verdict, parameters)
        );
    }

    // A difference of the texts has no server lines, and the message says where; a column that is an array of a
    // domain has another type for Npgsql, which a cast settles.
    private static string HelpOf(CheckVerdict verdict, IReadOnlyList<DescribedParameter> parameters)
    {
        if (verdict.PlacementDiffers)
        {
            return PlacementHelp;
        }

        return verdict.TypeDiffers && HasArrayOfDomain(parameters) ? ArrayOfDomainHelp : CheckHelp;
    }

    // Npgsql sends such a parameter as an array of the domain's base, and the server keeps the domain.
    private static bool HasArrayOfDomain(IReadOnlyList<DescribedParameter> parameters) =>
        parameters.Any(parameter =>
            parameter.Type is PostgresType { Element: { } element } array
            && array.TryGetKind(out var kind)
            && kind == PostgresTypeKind.Array
            && element.TryGetKind(out var elementKind)
            && elementKind == PostgresTypeKind.Domain
        );

    /// <summary>
    /// <c>SQLSRC232</c> at the step <c>check parameters</c>, for a parameter whose type has no name that Npgsql could
    /// be given (<see cref="PostgresBindNames.Of" />).  It is found before <c>check</c> is asked, so its one line is
    /// <see cref="PostgresBindNames.NoName" /> and not Npgsql's.
    /// </summary>
    /// <param name="query">The query.</param>
    /// <param name="parameter">The parameter's index in the query's list.</param>
    /// <param name="typeName">The type's <c>name</c>.</param>
    public static DescribeFailure Unnamed(DescribeRequest query, int parameter, string typeName) =>
        new(
            ToolDiagnostics.TypeCannotBeBound,
            new EquatableArray<string>([typeName, Written(query.Parameters[parameter]), query.Name]),
            DescribeStep.CheckParameters,
            new EquatableArray<string>([PostgresBindNames.NoName]),
            UnnamedHelp
        );

    // SQLSRC232.  The type is named by its name, as the SQL would spell it, and not by the name it was bound by; the
    // parameter is written as the SQL writes one.
    private static DescribeFailure CannotBind(
        DescribeRequest query,
        IReadOnlyList<DescribedParameter> parameters,
        PostgresCheckRequest asked,
        PostgresBindError unbound
    )
    {
        var index = 0;
        while (
            index < asked.Parameters.Count
            && !string.Equals(asked.Parameters[index].Name, unbound.Parameter, StringComparison.Ordinal)
        )
        {
            index++;
        }

        if (index == asked.Parameters.Count)
        {
            throw new InvalidOperationException(
                $"The answer to 'check' names the parameter '{unbound.Parameter}', which was not asked with."
            );
        }

        var type =
            parameters[index].Type?.Name
            ?? throw new InvalidOperationException($"The parameter '{unbound.Parameter}' was given no type.");
        return new DescribeFailure(
            ToolDiagnostics.TypeCannotBeBound,
            new EquatableArray<string>([type, Written(query.Parameters[index]), query.Name]),
            DescribeStep.CheckParameters,
            new EquatableArray<string>([unbound.Message]),
            BindHelp
        );
    }

    /// <summary>
    /// <c>SQLSRC225</c>: a table, a column or a type that the query uses went while it was described, since another
    /// session dropped it between two calls.  The server said nothing, so there is no server line, and the help says
    /// to run the tool again.
    /// </summary>
    /// <param name="query">The query.</param>
    /// <param name="step">The step whose call found out: <c>catalog</c> or <c>resolve type</c>.</param>
    /// <param name="what">
    /// What went, as the end of a sentence: <c>the table of the column 'id' no longer exists</c>.
    /// </param>
    public static DescribeFailure SchemaChanged(DescribeRequest query, DescribeStep step, string what) =>
        new(
            ToolDiagnostics.ServerRejectedQuery,
            new EquatableArray<string>([query.Name, what]),
            step,
            EquatableArray<string>.Empty,
            SchemaChangedHelp
        );

    // SQLSRC225.  The server's lines say what is wrong, so there is no help unless the caller knows more.
    private static DescribeFailure Rejected(
        PostgresServerError error,
        DescribeRequest query,
        DescribeStep step,
        EquatableArray<string> serverLines,
        string? help
    ) =>
        new(
            ToolDiagnostics.ServerRejectedQuery,
            new EquatableArray<string>([query.Name, error.Message]),
            step,
            serverLines,
            help
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
