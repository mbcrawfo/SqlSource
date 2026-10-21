using System.Globalization;
using System.Linq;
using SqlSource.Diagnostics;
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

    // How SQLSRC235 names the engine.
    private const string Engine = "SQL Server";

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
}
