using SqlSource.Settings;

namespace SqlSource.Generation.Methods;

internal static class DatabaseEngineExtensions
{
    /// <summary>The flag of the engine's queries in <see cref="MethodEngines" />.</summary>
    public static MethodEngines ToMethodEngines(this DatabaseEngine engine) =>
        engine switch
        {
            DatabaseEngine.PostgreSql => MethodEngines.Postgres,
            DatabaseEngine.SqlServer => MethodEngines.SqlServer,
            _ => throw UnexpectedEnumValueException.Of(engine),
        };

    /// <summary>The database's name as a diagnostic gives it: <c>PostgreSQL</c> or <c>SQL Server</c>.</summary>
    public static string DatabaseName(this DatabaseEngine engine) =>
        engine switch
        {
            DatabaseEngine.PostgreSql => "PostgreSQL",
            DatabaseEngine.SqlServer => "SQL Server",
            _ => throw UnexpectedEnumValueException.Of(engine),
        };

    /// <summary>
    /// The package of the engine's driver as a diagnostic names it: <c>Npgsql</c> or <c>Microsoft.Data.SqlClient</c>.
    /// </summary>
    public static string DriverName(this DatabaseEngine engine) =>
        engine switch
        {
            DatabaseEngine.PostgreSql => "Npgsql",
            DatabaseEngine.SqlServer => "Microsoft.Data.SqlClient",
            _ => throw UnexpectedEnumValueException.Of(engine),
        };

    /// <summary>
    /// The engine's driver's connection, from <c>global::</c>, which a repository's method makes from a connection
    /// string.
    /// </summary>
    public static string ConnectionTypeText(this DatabaseEngine engine) =>
        engine switch
        {
            DatabaseEngine.PostgreSql => "global::Npgsql.NpgsqlConnection",
            DatabaseEngine.SqlServer => "global::Microsoft.Data.SqlClient.SqlConnection",
            _ => throw UnexpectedEnumValueException.Of(engine),
        };
}
