using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace SqlSource.Tool.Describing.SqlServer;

/// <summary>
/// What runs on a session's connection: the SQL the tool sends, and the function that asks the server each call of
/// the describer.  A function of this class is the <c>live</c> one of its call, and under a replay none runs.
/// </summary>
/// <remarks>
/// <para>
/// In each function, what the server or SqlClient can cause is the answer's error, through
/// <see cref="ConnectionErrors" />.  SqlClient reports a cancelled command as a <see cref="SqlException" />, so one
/// thrown once the token is cancelled is the cancellation, and passes through as one.  Anything else is a bug.
/// </para>
/// <para>
/// The user's SQL and the declarations reach the server only as the values of the parameters <c>@tsql</c> and
/// <c>@params</c> of <c>sys.sp_describe_undeclared_parameters</c>, which is called as a procedure, and of
/// <c>sys.dm_exec_describe_first_result_set</c>, whose text is the tool's own.  Neither runs the text it is given: it
/// is analysed, so nothing a <c>.sql</c> file holds can make the describer run a statement.
/// </para>
/// </remarks>
internal sealed class SqlServerLive(SqlServerConnection connection)
{
    // The server's version and edition, then the settings of the database and the session, in the order of the
    // settings of "open".
    private const string OpenSql = """
        SELECT
            CAST(SERVERPROPERTY('ProductVersion') AS nvarchar(128)),
            CAST(SERVERPROPERTY('EngineEdition') AS int),
            d.compatibility_level,
            CAST(SESSIONPROPERTY('ANSI_NULLS') AS int),
            CAST(SESSIONPROPERTY('QUOTED_IDENTIFIER') AS int),
            CAST(DATABASEPROPERTYEX(DB_NAME(), 'Collation') AS nvarchar(128))
        FROM sys.databases AS d
        WHERE d.database_id = DB_ID()
        """;

    /// <summary>The procedure that <c>parameters</c> and <c>probe</c> call.</summary>
    public const string DescribeUndeclaredParameters = "sys.sp_describe_undeclared_parameters";

    // The type of a declared parameter, or the errors of its declaration as rows, for "declared".  Browse mode 0: the
    // columns alone, with no key columns of tables added.
    private const string DeclaredSql = """
        SELECT column_ordinal, system_type_name, max_length, precision, scale, user_type_schema, user_type_name,
            assembly_qualified_type_name, error_number, error_type, error_message
        FROM sys.dm_exec_describe_first_result_set(@tsql, @params, 0)
        ORDER BY column_ordinal
        """;

    public Task<SqlServerOpenAnswer> OpenAsync(CancellationToken token) =>
        AnswerAsync(
            async () =>
            {
                await using var command = await connection.CommandAsync(OpenSql, token);
                await using var reader = await command.ExecuteReaderAsync(token);
                if (!await reader.ReadAsync(token))
                {
                    throw new InvalidOperationException("The server answered 'open' with no row.");
                }

                return new SqlServerOpenAnswer(
                    reader.GetString(0),
                    reader.GetInt32(1),
                    SqlServerConnection.DriverVersion,
                    [
                        new SqlServerSetting(
                            "compatibility_level",
                            reader.GetByte(2).ToString(CultureInfo.InvariantCulture)
                        ),
                        new SqlServerSetting("ANSI_NULLS", OnOrOff(reader.GetInt32(3))),
                        new SqlServerSetting("QUOTED_IDENTIFIER", OnOrOff(reader.GetInt32(4))),
                        new SqlServerSetting("collation", reader.GetString(5)),
                    ],
                    null
                );
            },
            ConnectionErrors.AtOpen,
            static error => new SqlServerOpenAnswer(null, null, null, null, error),
            token
        );

    /// <summary>The parameters the server types in the SQL under the declarations, a row each, or its error.</summary>
    public Task<SqlServerParametersAnswer> ParametersAsync(
        SqlServerParametersRequest request,
        CancellationToken token
    ) =>
        AnswerAsync(
            async () =>
            {
                await using var command = await DescribeUndeclaredAsync(request.Sql, request.Params, token);
                await using var reader = await command.ExecuteReaderAsync(token);
                var rows = new List<SqlServerParameterRow>();
                while (await reader.ReadAsync(token))
                {
                    rows.Add(
                        new SqlServerParameterRow(
                            reader.GetString(reader.GetOrdinal("name")),
                            StringOf(reader, "suggested_system_type_name"),
                            IntOf(reader, "suggested_max_length"),
                            IntOf(reader, "suggested_precision"),
                            IntOf(reader, "suggested_scale"),
                            StringOf(reader, "suggested_user_type_schema"),
                            StringOf(reader, "suggested_user_type_name"),
                            StringOf(reader, "suggested_assembly_qualified_type_name")
                        )
                    );
                }

                await DrainAsync(reader, token);
                return new SqlServerParametersAnswer(rows, null);
            },
            ConnectionErrors.Of,
            static error => new SqlServerParametersAnswer(null, error),
            token
        );

    /// <summary>
    /// Whether the SQL binds under the declarations: true when the procedure raises nothing, false when it refuses
    /// the batch, with any error below class 20 that is no timeout; and the error of anything else.
    /// </summary>
    public Task<SqlServerProbeAnswer> ProbeAsync(SqlServerProbeRequest request, CancellationToken token) =>
        AnswerAsync(
            async () =>
            {
                await using var command = await DescribeUndeclaredAsync(request.Sql, request.Params, token);
                try
                {
                    _ = await command.ExecuteNonQueryAsync(token);
                }
                catch (SqlException exception) when (!token.IsCancellationRequested && RefusesTheBatch(exception))
                {
                    return new SqlServerProbeAnswer(false, null);
                }

                return new SqlServerProbeAnswer(true, null);
            },
            ConnectionErrors.Of,
            static error => new SqlServerProbeAnswer(null, error),
            token
        );

    /// <summary>
    /// The type the server reads a declared parameter as: the row of its column, or the error rows of the describe.
    /// </summary>
    public Task<SqlServerDeclaredAnswer> DeclaredAsync(SqlServerDeclaredRequest request, CancellationToken token) =>
        AnswerAsync(
            async () =>
            {
                await using var command = await connection.CommandAsync(DeclaredSql, token);
                _ = command.Parameters.Add(Text("@tsql", request.Sql));
                _ = command.Parameters.Add(Text("@params", request.Params));
                await using var reader = await command.ExecuteReaderAsync(token);
                var rows = new List<SqlServerDeclaredRow>();
                var errors = new List<SqlServerDescribeError>();
                while (await reader.ReadAsync(token))
                {
                    if (IntOf(reader, "error_number") is { } number)
                    {
                        errors.Add(
                            new SqlServerDescribeError(
                                number,
                                IntOf(reader, "error_type") ?? 0,
                                StringOf(reader, "error_message") ?? string.Empty
                            )
                        );
                        continue;
                    }

                    rows.Add(
                        new SqlServerDeclaredRow(
                            reader.GetInt32(reader.GetOrdinal("column_ordinal")),
                            StringOf(reader, "system_type_name"),
                            IntOf(reader, "max_length"),
                            IntOf(reader, "precision"),
                            IntOf(reader, "scale"),
                            StringOf(reader, "user_type_schema"),
                            StringOf(reader, "user_type_name"),
                            StringOf(reader, "assembly_qualified_type_name")
                        )
                    );
                }

                await DrainAsync(reader, token);
                return errors.Count > 0
                    ? new SqlServerDeclaredAnswer(null, errors, null)
                    : new SqlServerDeclaredAnswer(rows, null, null);
            },
            ConnectionErrors.Of,
            static error => new SqlServerDeclaredAnswer(null, null, error),
            token
        );

    /// <summary>
    /// The text of <c>declared</c> for one parameter, which the server describes under its declaration:
    /// <c>SELECT @p AS [1]</c>.
    /// </summary>
    /// <param name="parameter">The parameter's name, without its <c>@</c>.</param>
    public static string DeclaredSelect(string parameter) => "SELECT @" + parameter + " AS [1]";

    /// <summary>The declarations of parameters as <c>@params</c> takes them: <c>@a int, @b nvarchar(50)</c>.</summary>
    /// <param name="declarations">Each parameter's name, without its <c>@</c>, and its type as it is declared.</param>
    public static string Declarations(IEnumerable<(string Name, string Type)> declarations) =>
        string.Join(", ", declarations.Select(static declaration => "@" + declaration.Name + " " + declaration.Type));

    // A call of sp_describe_undeclared_parameters with the SQL and the declarations as the values of its parameters.
    private async Task<SqlCommand> DescribeUndeclaredAsync(string sql, string? declarations, CancellationToken token)
    {
        var command = await connection.ProcedureAsync(DescribeUndeclaredParameters, token);
        _ = command.Parameters.Add(Text("@tsql", sql));
        _ = command.Parameters.Add(Text("@params", declarations));
        return command;
    }

    // A parameter of nvarchar(max), DBNull for null.
    private static SqlParameter Text(string name, string? value) =>
        new(name, SqlDbType.NVarChar, -1) { Value = (object?)value ?? DBNull.Value };

    // Reads to the end of what the server answered, so that an error it raised after the rows is the call's.
    private static async Task DrainAsync(SqlDataReader reader, CancellationToken token)
    {
        while (await reader.NextResultAsync(token))
        {
            // A result set after the first is read for its errors alone.
        }
    }

    private static string? StringOf(SqlDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    // A column of smallint, tinyint or int, as an int.
    private static int? IntOf(SqlDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal)
            ? null
            : Convert.ToInt32(reader.GetValue(ordinal), CultureInfo.InvariantCulture);
    }

    // Whether the server refused the batch, which it does when the declarations do not bind: every error is below the
    // class that ends the connection, and none is SqlClient's timeout.  A probe that fails otherwise says nothing of
    // the batch.
    private static bool RefusesTheBatch(SqlException exception) =>
        exception
            .Errors.Cast<SqlError>()
            .All(static error => error.Class < ConnectionErrors.FatalClass && error.Number != ConnectionErrors.Timeout);

    private static string OnOrOff(int value) => value == 1 ? "ON" : "OFF";

    /// <summary>
    /// Runs a call and gives its answer, or the answer of the error the server, SqlClient or the connection gave.  A
    /// <see cref="SqlException" /> thrown once the token is cancelled is the cancellation.  Internal for a container
    /// test, which cancels a command that the server is running.
    /// </summary>
    /// <param name="call">What the call runs.</param>
    /// <param name="errorOf">
    /// <see cref="ConnectionErrors.AtOpen" /> for <c>open</c> and <see cref="ConnectionErrors.Of" /> for every other
    /// call.
    /// </param>
    /// <param name="failed">The call's answer that holds an error.</param>
    /// <param name="token">The token the call runs under.</param>
    internal static async Task<TAnswer> AnswerAsync<TAnswer>(
        Func<Task<TAnswer>> call,
        Func<SqlException, SqlServerServerError> errorOf,
        Func<SqlServerServerError, TAnswer> failed,
        CancellationToken token
    )
    {
        try
        {
            return await call();
        }
        catch (SqlException) when (token.IsCancellationRequested)
        {
            throw new OperationCanceledException(token);
        }
        catch (SqlException exception)
        {
            return failed(errorOf(exception));
        }
        catch (SqlServerConnection.UnusableException exception)
        {
            return failed(ConnectionErrors.Own(exception.Message));
        }
    }
}
