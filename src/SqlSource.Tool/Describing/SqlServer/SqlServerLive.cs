using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace SqlSource.Tool.Describing.SqlServer;

/// <summary>
/// What runs on a session's connection: the SQL the tool sends, and the function that asks the server each call of
/// the describer.  A function of this class is the <c>live</c> one of its call, and under a replay none runs.
/// </summary>
/// <remarks>
/// In each function, what the server or SqlClient can cause is the answer's error, through
/// <see cref="ConnectionErrors" />.  SqlClient reports a cancelled command as a <see cref="SqlException" />, so one
/// thrown once the token is cancelled is the cancellation, and passes through as one.  Anything else is a bug.
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

    private static string OnOrOff(int value) => value == 1 ? "ON" : "OFF";

    // Runs a call and gives its answer, or the answer of the error the server, SqlClient or the connection gave.
    // errorOf is ConnectionErrors.AtOpen for "open" and ConnectionErrors.Of for every other call.
    private static async Task<TAnswer> AnswerAsync<TAnswer>(
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
