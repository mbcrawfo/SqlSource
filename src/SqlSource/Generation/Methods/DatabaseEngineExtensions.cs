using SqlSource.Settings;

namespace SqlSource.Generation.Methods;

internal static class DatabaseEngineExtensions
{
    /// <summary>The flag of the engine's queries in <see cref="CodeGenEngines" />.</summary>
    public static CodeGenEngines ToCodeGenEngines(this DatabaseEngine engine) =>
        engine switch
        {
            DatabaseEngine.PostgreSql => CodeGenEngines.Postgres,
            DatabaseEngine.SqlServer => CodeGenEngines.SqlServer,
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
}
