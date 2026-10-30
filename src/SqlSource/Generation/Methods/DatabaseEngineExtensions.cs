using SqlSource.Settings;

namespace SqlSource.Generation.Methods;

internal static class DatabaseEngineExtensions
{
    /// <summary>The flag of the engine's queries in <see cref="CodeGenEngines" />.</summary>
    public static CodeGenEngines ToCodeGenEngines(this DatabaseEngine engine) =>
        engine == DatabaseEngine.SqlServer ? CodeGenEngines.SqlServer : CodeGenEngines.Postgres;
}
