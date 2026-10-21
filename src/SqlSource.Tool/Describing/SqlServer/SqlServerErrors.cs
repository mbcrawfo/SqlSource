using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using SqlSource.Diagnostics;
using SqlSource.Parsing;
using SqlSource.Snapshot;
using SqlSource.Tool.Describing.Describers;

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
        "a parameter of a CREATE PROCEDURE is no parameter of the query, and a DECLAREd variable is none either; "
        + "rewrite the query so that both read it the same, or declare a parameter that SqlSource reads as a variable "
        + "with -- param:";

    /// <summary>The reason of <c>SQLSRC228</c> for a type the database lacks: <c>2715</c> or <c>2724</c>.</summary>
    public const string NoSuchType = "the database has no such type";

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

    /// <summary>
    /// <c>SQLSRC224</c>: no session.  Every failure of <c>open</c> is this one, whatever its numbers: a refused login
    /// and a missing database are not the server rejecting a query.
    /// </summary>
    public static DescribeFailure Open(SqlServerServerError error, string database) =>
        new(
            ToolDiagnostics.DatabaseCannotBeOpened,
            new EquatableArray<string>([database]),
            null,
            ServerLines(error),
            OpenHelp
        );

    /// <summary>
    /// <c>SQLSRC235</c>: the server's major is below <see cref="SqlServerSession.MinimumMajorVersion" /> and it is
    /// neither an Azure SQL Database nor a Managed Instance.  Nothing of a query is sent, and the failure holds no
    /// server line: the server said nothing.
    /// </summary>
    /// <param name="database">The database.</param>
    /// <param name="version">The server's <c>ProductVersion</c>, as it is recorded.</param>
    public static DescribeFailure VersionNotSupported(string database, string version) =>
        new(
            ToolDiagnostics.ServerVersionNotSupported,
            new EquatableArray<string>([
                database,
                Engine,
                version,
                SqlServerSession.MinimumMajorVersion.ToString(CultureInfo.InvariantCulture)
                    + " ("
                    + MinimumRelease
                    + ")",
            ]),
            null,
            EquatableArray<string>.Empty,
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
    /// type.  Every row is a line, in the server's order.
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
        var lines = new EquatableArray<string>([
            .. errors.Select(static row =>
                string.Create(CultureInfo.InvariantCulture, $"Msg {row.Number}: {row.Message}")
            ),
        ]);
        return errors.Any(static row => row.Number is TypeNotFound or InvalidDataType)
            ? DeclaredType(query, parameter, NoSuchType, lines, NoSuchTypeHelp)
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
    /// order of use, each named as a declaration would write it (<see cref="SqlServerTypes.Spelled" />).
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

    /// <summary>A parameter as the SQL writes one: <c>@id</c>.</summary>
    public static string Written(string parameter) => SqlDialectRules.SqlServer.ParameterPrefix + parameter;

    // SQLSRC228 at the step "resolve type", whose last argument is the reason the type cannot be used.
    private static DescribeFailure DeclaredType(
        DescribeRequest query,
        SqlQueryParameter parameter,
        string reason,
        EquatableArray<string> serverLines,
        string help
    ) =>
        new(
            ToolDiagnostics.DeclaredTypeNotValid,
            new EquatableArray<string>([parameter.Type ?? string.Empty, Written(parameter.Name), query.Name, reason]),
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
        var named = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var message in error.Errors.Select(static row => row.Message))
        {
            var at = message.IndexOf('@', StringComparison.Ordinal);
            while (at >= 0)
            {
                var end = at + 1;
                while (end < message.Length && IsNamePart(message[end]))
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

    // A character that continues a T-SQL name after its "@": a letter, a digit, "_", "@", "#" or "$".
    private static bool IsNamePart(char character) =>
        char.IsLetterOrDigit(character) || character is '_' or '@' or '#' or '$';
}
