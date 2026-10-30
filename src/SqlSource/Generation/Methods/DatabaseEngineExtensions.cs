using SqlSource.Settings;

namespace SqlSource.Generation.Methods;

internal static class DatabaseEngineExtensions
{
    /// <summary>The flag of the engine's queries in <see cref="CodeGenEngines" />.</summary>
    public static CodeGenEngines ToCodeGenEngines(this DatabaseEngine engine) =>
        engine == DatabaseEngine.SqlServer ? CodeGenEngines.SqlServer : CodeGenEngines.Postgres;

    /// <summary>The database's name as a diagnostic gives it: <c>PostgreSQL</c> or <c>SQL Server</c>.</summary>
    public static string DatabaseName(this DatabaseEngine engine) =>
        engine == DatabaseEngine.SqlServer ? "SQL Server" : "PostgreSQL";
}
