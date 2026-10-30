using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using SqlSource.Diagnostics;
using SqlSource.Parsing;
using SqlSource.Parsing.Quoting;
using SqlSource.Snapshot;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Describing.SqlServer.Statements;
using SqlSource.Tool.Describing.SqlServer.Types;

namespace SqlSource.Tool.Describing.SqlServer;

/// <summary>
/// What the server said, or what the tool refuses before it asks, as the failure a run reports at the query.  One id
/// for each cause a user mends differently.
/// </summary>
internal static class SqlServerErrors
{
    public const string OpenHelp =
        "check that the server is running, and that the connection string names it, a login it accepts and the "
        + "database";

    /// <summary>The release of SQL Server whose major is <see cref="SqlServerSession.MinimumMajorVersion" />.</summary>
    public const string MinimumRelease = "SQL Server 2017";

    public const string VersionNotSupportedHelp =
        "upgrade the server, or describe against a database of "
        + MinimumRelease
        + " or later, or of Azure SQL Database or Azure SQL Managed Instance";

    /// <summary>The help of <c>SQLSRC226</c>: the server could not deduce a parameter's type.</summary>
    public const string CastOrDeclareHelp =
        "add a CAST around a use of the parameter, or declare its type with -- param:";

    /// <summary>The help of <c>SQLSRC262</c>: the copies of a parameter have two types.</summary>
    public const string CopiesDisagreeHelp = "declare the parameter's type with -- param:";

    /// <summary>The help of <c>SQLSRC231</c>: the server and SqlSource read the parameters differently.</summary>
    public const string ListsDifferHelp =
        "rewrite the query so that SqlSource and SQL Server read the same parameters, or give a parameter that "
        + "SqlSource reads as a variable a -- param: marker with its type";

    /// <summary>The help of <c>SQLSRC265</c>: a temporary table in a batch of several statements.</summary>
    public const string TemporaryTableHelp =
        "make the query one statement, or use a table variable in place of the temporary table";

    /// <summary>The help of <c>SQLSRC266</c>: a column with no name.</summary>
    public const string ColumnNameHelp = "give the column a name with AS";

    /// <summary>
    /// The last error of <c>parameters</c> for a query that did not compile, which says only that it did not.  For a
    /// temporary table of a batch of several statements its cause is <c>208</c>, a missing table, where
    /// <c>columns</c> gives the cause by its type: <see cref="AfterRefusal" />.
    /// </summary>
    public const int RefusedBatch = 11501;

    public const string NoSuchTypeHelp =
        "write a type the database has, with its schema when it is not a built-in type: dbo.Email";

    /// <summary>The reason of <c>SQLSRC228</c> for a declaration the server could not read as a type.</summary>
    public const string NotAType = "it is not a type";

    public const string NotATypeHelp =
        "write the type as a column definition would: int, nvarchar(50), decimal(18,2), varchar(max) or dbo.Email";

    // How SQLSRC235 names the engine, and SQLSRC231 what reads the parameters differently from SqlSource.
    private const string Engine = "SQL Server";

    // The errors of sp_describe_undeclared_parameters for a parameter whose type the server cannot deduce: no typing
    // context, two untyped parameters in one expression, two possible types, and no type that makes the query valid.
    private const int NotDeducedInContext = 11502;
    private const int TwoUntypedParameters = 11503;
    private const int TwoPossibleTypes = 11506;
    private const int NoValidType = 11507;

    // The errors of a declaration that names a type the database lacks.
    private const int TypeNotFound = 2715;
    private const int InvalidDataType = 2724;

    // The error of an identifier longer than SQL Server allows.
    private const int IdentifierTooLong = 103;

    // The error_type of a row of sys.dm_exec_describe_first_result_set that says why it has no first result set:
    // CONFLICTING_RESULTS, DYNAMIC_SQL, UNDECLARED_PARAMETER and TEMPORARY_TABLE.
    private const int ConflictingResultsType = 3;
    private const int DynamicSqlType = 4;
    private const int UndeclaredParameterType = 8;
    private const int TemporaryTableType = 10;

    /// <summary>
    /// <c>SQLSRC224</c>: no session.  Every failure of <c>open</c> is this one, whatever its numbers: a refused login
    /// and a missing database are not the server rejecting a query.
    /// </summary>
    public static DescribeFailure Open(SqlServerServerError error, string database) =>
        DescribeFailures.Open(database, ServerLines(error), OpenHelp);

    /// <summary>
    /// <c>SQLSRC235</c>: the server's major is below <see cref="SqlServerSession.MinimumMajorVersion" /> and it is
    /// neither an Azure SQL Database nor a Managed Instance.  Nothing of a query is sent, and the failure holds no
    /// server line: the server said nothing.
    /// </summary>
    /// <param name="database">The database.</param>
    /// <param name="version">The server's <c>ProductVersion</c>, as it is recorded.</param>
    public static DescribeFailure VersionNotSupported(string database, string version) =>
        DescribeFailures.VersionNotSupported(
            database,
            Engine,
            version,
            SqlServerSession.MinimumMajorVersion.ToString(CultureInfo.InvariantCulture) + " (" + MinimumRelease + ")",
            VersionNotSupportedHelp
        );

    /// <summary>
    /// The <c>server:</c> lines, one for each error in its order: <c>Msg 18456, Level 14, State 1: text</c>, or the
    /// text alone for an error of the describer's own, whose number is 0.
    /// </summary>
    public static EquatableArray<string> ServerLines(SqlServerServerError error) =>
        new([
            .. error.Errors.Select(static row =>
                row.Number == 0
                    ? row.Message
                    : string.Create(
                        CultureInfo.InvariantCulture,
                        $"Msg {row.Number}, Level {row.Class}, State {row.State}: {row.Message}"
                    )
            ),
        ]);

    /// <summary>
    /// <c>SQLSRC228</c> for a type that <see cref="DeclaredTypeText" /> refuses.  Nothing of the query is sent, so
    /// there is no server line.
    /// </summary>
    /// <param name="query">The query.</param>
    /// <param name="parameter">The declared parameter.</param>
    /// <param name="reason">What <see cref="DeclaredTypeText.Read" /> gave.</param>
    public static DescribeFailure TypeRefused(DescribeRequest query, SqlQueryParameter parameter, string reason) =>
        DeclaredType(query, parameter, reason, EquatableArray<string>.Empty, NotATypeHelp);

    /// <summary>
    /// <c>SQLSRC228</c> for the error rows that <c>declared</c> answered: <c>2715</c> or <c>2724</c> among them is a
    /// type the database lacks, and anything else, such as <c>102</c>, a declaration the server does not read as a
    /// type.  A parameter whose own name is longer than SQL Server allows is no fault of its type: <c>103</c> among
    /// the rows for it is <c>SQLSRC225</c>, as it is when the parameter is not declared and <c>parameters</c> raises
    /// it.  Every row is a line, in the server's order.
    /// </summary>
    /// <param name="query">The query.</param>
    /// <param name="parameter">The declared parameter that was asked.</param>
    /// <param name="answer">The answer of <c>declared</c>, which holds error rows.</param>
    public static DescribeFailure Declared(
        DescribeRequest query,
        SqlQueryParameter parameter,
        SqlServerDeclaredAnswer answer
    )
    {
        var errors = answer.Errors ?? [];
        var lines = DescribeLines(errors);
        if (
            parameter.Name.Length > ParameterCopies.MaximumCopyLength
            && errors.Any(static row => row.Number == IdentifierTooLong)
        )
        {
            return new DescribeFailure(
                ToolDiagnostics.ServerRejectedQuery,
                new EquatableArray<string>([query.Name, errors[0].Message]),
                DescribeStep.ResolveType,
                lines,
                null
            );
        }

        return errors.Any(static row => row.Number is TypeNotFound or InvalidDataType)
            ? DeclaredType(query, parameter, DescribeFailures.NoSuchType, lines, NoSuchTypeHelp)
            : DeclaredType(query, parameter, NotAType, lines, NotATypeHelp);
    }

    /// <summary>
    /// What an error of <c>parameters</c> is reported as, by the number of its first error, the cause: one of the
    /// four errors of a type the server cannot deduce is <c>SQLSRC226</c>, and anything else, <c>11508</c> and a
    /// type the query's own text names that the database lacks included, is the server rejecting the query,
    /// <c>SQLSRC225</c>.  The message is read only to pick the parameter that <c>SQLSRC226</c> names.
    /// </summary>
    /// <param name="query">The query.</param>
    /// <param name="error">What the server said.</param>
    /// <param name="copied">
    /// The SQL that was sent, by which a copy that a message names is taken back to its parameter.
    /// </param>
    public static DescribeFailure Parameters(DescribeRequest query, SqlServerServerError error, CopiedSql copied)
    {
        if (
            error.Errors
                is [{ Number: NotDeducedInContext or TwoUntypedParameters or TwoPossibleTypes or NoValidType }, ..]
            && Undeclared(query) is [var first, ..] undeclared
        )
        {
            var named = NamedIn(error, copied);
            var parameter = undeclared.Find(named.Contains) ?? first;
            return new DescribeFailure(
                ToolDiagnostics.ParameterTypeNotDetermined,
                new EquatableArray<string>([Written(parameter), query.Name]),
                DescribeStep.DescribeParameters,
                ServerLines(error),
                CastOrDeclareHelp
            );
        }

        return Failed(query, error, DescribeStep.DescribeParameters);
    }

    /// <summary>
    /// What the error rows of <c>columns</c> or <c>table</c> are reported as, by their <c>error_type</c> and never by
    /// their message: the first row of type 3, 4 or 10 is <c>SQLSRC263</c>, <c>SQLSRC264</c> or <c>SQLSRC265</c>.
    /// Anything else is the server rejecting the query, <c>SQLSRC225</c>, whose message ends with the text of the first
    /// row of type 8, which cannot happen since every parameter is declared, or else of the first row, the cause.
    /// Every row is a line, in the server's order.
    /// </summary>
    /// <param name="query">The query.</param>
    /// <param name="errors">The error rows that the call answered.</param>
    /// <param name="step">The step whose call answered them: <c>describe columns</c>, or <c>table match</c>.</param>
    public static DescribeFailure Columns(
        DescribeRequest query,
        IReadOnlyList<SqlServerDescribeError> errors,
        DescribeStep step
    )
    {
        var lines = DescribeLines(errors);
        var shape = errors.FirstOrDefault(IsShapeCause);
        if (shape is not null)
        {
            return new DescribeFailure(
                shape.Type switch
                {
                    ConflictingResultsType => ToolDiagnostics.ConflictingResults,
                    DynamicSqlType => ToolDiagnostics.DynamicSql,
                    _ => ToolDiagnostics.TemporaryTableInBatch,
                },
                new EquatableArray<string>([query.Name]),
                step,
                lines,
                shape.Type == TemporaryTableType ? TemporaryTableHelp : null
            );
        }

        var cause =
            errors.FirstOrDefault(static row => row.Type == UndeclaredParameterType)
            ?? (errors.Count > 0 ? errors[0] : null);
        return new DescribeFailure(
            ToolDiagnostics.ServerRejectedQuery,
            new EquatableArray<string>([query.Name, cause?.Message ?? string.Empty]),
            step,
            lines,
            null
        );
    }

    /// <summary>
    /// What a query is reported as when <c>parameters</c> failed with <see cref="RefusedBatch" /> and <c>columns</c>
    /// was asked with the declared parameters alone: error rows of type 3, 4 or 10 give the cause, as
    /// <see cref="Columns" /> reports it at <c>describe columns</c>, and any other answer leaves the failure of
    /// <c>parameters</c>, with its lines.
    /// </summary>
    /// <param name="query">The query.</param>
    /// <param name="parameters">What the server said to <c>parameters</c>.</param>
    /// <param name="copied">The SQL that <c>parameters</c> was sent.</param>
    /// <param name="columns">The answer of <c>columns</c>.</param>
    public static DescribeFailure AfterRefusal(
        DescribeRequest query,
        SqlServerServerError parameters,
        CopiedSql copied,
        SqlServerColumnsAnswer columns
    ) =>
        columns.Errors is { } errors && errors.Any(IsShapeCause)
            ? Columns(query, errors, DescribeStep.DescribeColumns)
            : Parameters(query, parameters, copied);

    /// <summary>
    /// <c>SQLSRC266</c>: a column of the query has no name, <c>SELECT 1</c>.  The server raised nothing, so there is
    /// no server line.
    /// </summary>
    /// <param name="query">The query.</param>
    /// <param name="ordinal">The column's place, counted from one.</param>
    public static DescribeFailure NoColumnName(DescribeRequest query, int ordinal) =>
        new(
            ToolDiagnostics.ColumnHasNoName,
            new EquatableArray<string>([ordinal.ToString(CultureInfo.InvariantCulture), query.Name]),
            DescribeStep.DescribeColumns,
            EquatableArray<string>.Empty,
            ColumnNameHelp
        );

    /// <summary>
    /// <c>SQLSRC226</c> for a reused parameter whose copies give it no type: every copy was ignored, or one left has
    /// no type.  The server raised nothing, so there is no server line.
    /// </summary>
    /// <param name="query">The query.</param>
    /// <param name="parameter">The parameter's name, as the list spells it.</param>
    public static DescribeFailure NoType(DescribeRequest query, string parameter) =>
        new(
            ToolDiagnostics.ParameterTypeNotDetermined,
            new EquatableArray<string>([Written(parameter), query.Name]),
            DescribeStep.DescribeParameters,
            EquatableArray<string>.Empty,
            CastOrDeclareHelp
        );

    /// <summary>
    /// <c>SQLSRC262</c>: the copies of a reused parameter that constrain its type have two types, the first two in
    /// order of use, each named as a message names a type (<see cref="SqlServerTypes.Spelled" />).
    /// </summary>
    /// <param name="query">The query.</param>
    /// <param name="parameter">The parameter's name, as the list spells it.</param>
    /// <param name="first">The type of the first copy left.</param>
    /// <param name="second">The first type of the copies left that differs from it.</param>
    public static DescribeFailure CopiesDisagree(
        DescribeRequest query,
        string parameter,
        SqlServerType first,
        SqlServerType second
    ) =>
        new(
            ToolDiagnostics.CopiesHaveDifferentTypes,
            new EquatableArray<string>([
                Written(parameter),
                query.Name,
                SqlServerTypes.Spelled(first),
                SqlServerTypes.Spelled(second),
            ]),
            DescribeStep.DescribeParameters,
            EquatableArray<string>.Empty,
            CopiesDisagreeHelp
        );

    /// <summary>
    /// <c>SQLSRC231</c> at the step <c>check parameters</c>: the parameters that the server reported and those of the
    /// query's list are not the same.  The message names each, and which side lacks it.
    /// </summary>
    /// <param name="query">The query.</param>
    /// <param name="differences">What <see cref="ParameterCopies.CompareLists" /> found: one at least.</param>
    public static DescribeFailure ListsDiffer(DescribeRequest query, IReadOnlyList<ListDifference> differences) =>
        new(
            ToolDiagnostics.ParametersReadDifferently,
            new EquatableArray<string>([
                Engine,
                query.Name,
                string.Join(
                    "; ",
                    differences.Select(static difference =>
                        difference.ReadBySqlSource
                            ? $"SqlSource reads '{Written(difference.Name)}' as a parameter and {Engine} does not"
                            : $"{Engine} reads '{Written(difference.Name)}' as a parameter and SqlSource does not"
                    )
                ),
            ]),
            DescribeStep.CheckParameters,
            EquatableArray<string>.Empty,
            ListsDifferHelp
        );

    /// <summary>
    /// <c>SQLSRC225</c>: the server rejected the query, or the call failed on its connection.  The message ends with
    /// the first error's text, the cause, and every error is a <c>server:</c> line, in the server's order.
    /// </summary>
    /// <param name="query">The query.</param>
    /// <param name="error">What the server or SqlClient said.</param>
    /// <param name="step">The step whose call failed: <c>resolve type</c> for <c>declared</c>.</param>
    public static DescribeFailure Failed(DescribeRequest query, SqlServerServerError error, DescribeStep step) =>
        new(
            ToolDiagnostics.ServerRejectedQuery,
            new EquatableArray<string>([query.Name, error.Errors.Count > 0 ? error.Errors[0].Message : string.Empty]),
            step,
            ServerLines(error),
            null
        );

    // The server: lines of the error rows of sys.dm_exec_describe_first_result_set, one for each in its order:
    // "Msg 207: text", since a row has no level or state.
    private static EquatableArray<string> DescribeLines(IReadOnlyList<SqlServerDescribeError> errors) =>
        new([
            .. errors.Select(static row =>
                string.Create(CultureInfo.InvariantCulture, $"Msg {row.Number}: {row.Message}")
            ),
        ]);

    // An error row whose type says why the query has no first result set to describe, which the user mends by its
    // own diagnostic.
    private static bool IsShapeCause(SqlServerDescribeError row) =>
        row.Type is ConflictingResultsType or DynamicSqlType or TemporaryTableType;

    // A parameter as the SQL writes one: "@id".
    private static string Written(string parameter) => SqlDialectRules.SqlServer.ParameterPrefix + parameter;

    // SQLSRC228 at the step "resolve type", whose last argument is the reason the type cannot be used.
    private static DescribeFailure DeclaredType(
        DescribeRequest query,
        SqlQueryParameter parameter,
        string reason,
        EquatableArray<string> serverLines,
        string help
    ) =>
        DescribeFailures.DeclaredType(
            query,
            Written(parameter.Name),
            parameter.Type,
            reason,
            DescribeStep.ResolveType,
            serverLines,
            help
        );

    // The names of the query's list whose type the server infers, in its order: a parameter is declared when its
    // marker gives a type.
    private static List<string> Undeclared(DescribeRequest query) =>
        [
            .. query
                .Parameters.Where(static parameter => parameter.Type is null)
                .Select(static parameter => parameter.Name),
        ];

    // The parameters that the errors' messages name, each "@name" read as a whole token and a copy taken back to its
    // parameter, compared ignoring case.
    private static HashSet<string> NamedIn(SqlServerServerError error, CopiedSql copied)
    {
        var named = new HashSet<string>(SqlParameterName.Comparer);
        foreach (var message in error.Errors.Select(static row => row.Message))
        {
            var at = message.IndexOf('@', StringComparison.Ordinal);
            while (at >= 0)
            {
                var end = at + 1;
                while (end < message.Length && QuoteReader.IsNameCharacterOfSqlServer(message[end]))
                {
                    end++;
                }

                var name = message[(at + 1)..end];
                _ = named.Add(copied.ParameterOfCopy.TryGetValue(name, out var parameter) ? parameter : name);
                at = end < message.Length ? message.IndexOf('@', end) : -1;
            }
        }

        return named;
    }
}
